using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace AK.Systems
{
	/// <summary>
	/// Per-parent navigation history for fragments (views without a channel), plus the
	/// per-parent show gate that serialises rapid shows on the same parent so two entrances
	/// never fight over one history stack.
	/// </summary>
	internal sealed class FragmentHistories
	{
		private readonly Dictionary<UIView, ViewStack>                _histories = new();
		private readonly Dictionary<UIView, UniTaskCompletionSource> _gates     = new();

		public IReadOnlyDictionary<UIView, ViewStack> Stacks => _histories;

		public ViewStack GetOrCreate(UIView parent)
		{
			if (!_histories.TryGetValue(parent, out var history))
			{
				history = new ViewStack();
				_histories[parent] = history;
			}

			return history;
		}

		public bool TryGet(UIView parent, out ViewStack history) => _histories.TryGetValue(parent, out history);

		/// <summary>Forgets a parent's history and any show waiting at its gate.</summary>
		public void Remove(UIView parent)
		{
			_histories.Remove(parent);
			_gates.Remove(parent);
		}

		/// <summary>Takes <paramref name="child"/> out of its parent's history, if either exists.</summary>
		public void Remove(UIView parent, UIView child)
		{
			if (parent != null && _histories.TryGetValue(parent, out var history))
			{
				history.Remove(child);
			}
		}

		public void RemoveEverywhere(UIView view)
		{
			foreach (var history in _histories.Values)
			{
				history.Remove(view);
			}
		}

		/// <summary>Waits for the show in flight on <paramref name="parent"/>, if any. Only the ordering matters; its failure is not ours.</summary>
		public async UniTask WaitForPendingAsync(UIView parent)
		{
			if (!_gates.TryGetValue(parent, out var pending)) return;

			try
			{
				await pending.Task;
			}
			catch
			{
				// sequencing only
			}
		}

		/// <summary>Takes the gate for one show. Complete or Fail it, then Dispose releases the gate if it is still ours.</summary>
		public GateScope EnterGate(UIView parent)
		{
			var source = new UniTaskCompletionSource();
			_gates[parent] = source;
			return new GateScope(this, parent, source);
		}

		private void ExitGate(UIView parent, UniTaskCompletionSource ours)
		{
			if (_gates.TryGetValue(parent, out var current) && ReferenceEquals(current, ours))
			{
				_gates.Remove(parent);
			}
		}

		public readonly struct GateScope : IDisposable
		{
			private readonly FragmentHistories        _owner;
			private readonly UIView                   _parent;
			private readonly UniTaskCompletionSource _source;

			public GateScope(FragmentHistories owner, UIView parent, UniTaskCompletionSource source)
			{
				_owner = owner;
				_parent = parent;
				_source = source;
			}

			public void Complete() => _source.TrySetResult();

			public void Fail(Exception exception) => _source.TrySetException(exception);

			public void Dispose() => _owner.ExitGate(_parent, _source);
		}
	}
}
