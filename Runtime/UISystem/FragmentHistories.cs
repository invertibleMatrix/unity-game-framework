using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace AK.Systems
{
	/// <summary>
	/// Per-parent navigation history for fragments (views without a channel), plus the
	/// per-parent show gate that serialises rapid shows on the same parent so two entrances
	/// never fight over one history stack.
	///
	/// The gate is a queue, not a slot: each serialized show links behind the one before it
	/// and is released when that one settles, so any number of shows issued in one tick run
	/// one at a time in call order. Dropping a parent's history completes every gate still
	/// queued on it, so nothing waits on a gate nobody will finish.
	/// </summary>
	internal sealed class FragmentHistories
	{
		private readonly Dictionary<UIView, ViewStack> _histories = new();
		private readonly Dictionary<UIView, Gate>      _tails     = new();

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

		/// <summary>Forgets a parent's history and releases every show still queued at its gate.</summary>
		public void Remove(UIView parent)
		{
			_histories.Remove(parent);

			if (_tails.TryGetValue(parent, out var tail))
			{
				_tails.Remove(parent);

				for (Gate gate = tail; gate != null; gate = gate.Previous)
				{
					gate.Source.TrySetResult();
				}
			}
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

		/// <summary>
		/// Joins the queue of serialized shows on <paramref name="parent"/>. The returned scope
		/// is the gate's new tail from this call on, so shows issued in one tick line up in call
		/// order. Await <see cref="GateScope.WaitForTurnAsync"/>, do the show, then Complete or
		/// Fail; Dispose drops the scope from the tail if it is still there.
		/// </summary>
		public GateScope EnterGate(UIView parent)
		{
			_tails.TryGetValue(parent, out var previous);

			var gate = new Gate(previous);
			_tails[parent] = gate;

			return new GateScope(this, parent, gate);
		}

		private void ExitGate(UIView parent, Gate gate)
		{
			if (_tails.TryGetValue(parent, out var tail) && ReferenceEquals(tail, gate))
			{
				_tails.Remove(parent);
			}
		}

		/// <summary>
		/// One place in a parent's queue. <see cref="Previous"/> is dropped once the wait is
		/// over, so the chain from the tail only ever spans the shows still queued plus the one
		/// running — not every show the parent ever hosted.
		/// </summary>
		internal sealed class Gate
		{
			public readonly UniTaskCompletionSource Source = new();
			public Gate Previous;

			public Gate(Gate previous)
			{
				Previous = previous;
			}
		}

		public readonly struct GateScope : IDisposable
		{
			private readonly FragmentHistories _owner;
			private readonly UIView            _parent;
			private readonly Gate              _gate;

			internal GateScope(FragmentHistories owner, UIView parent, Gate gate)
			{
				_owner = owner;
				_parent = parent;
				_gate = gate;
			}

			/// <summary>
			/// Waits for the show ahead of this one to settle. The wait itself is never cut
			/// short — a show that left the queue early would let the ones behind it overtake
			/// the one ahead — but <paramref name="ct"/> is honoured once the turn comes. The
			/// wait is bounded by the predecessor's presentation, or by the parent's history
			/// being dropped, which completes every gate on it.
			///
			/// The turn coming does not mean the show is still wanted: a parent closing while
			/// this show was queued has already settled the view. The caller checks the
			/// registry before presenting.
			/// </summary>
			public async UniTask WaitForTurnAsync(CancellationToken ct = default)
			{
				Gate previous = _gate.Previous;

				if (previous != null)
				{
					try
					{
						await previous.Source.Task;
					}
					catch
					{
						// sequencing only — the show ahead failing or being cancelled is not ours
					}

					_gate.Previous = null;
				}

				ct.ThrowIfCancellationRequested();
			}

			public void Complete() => _gate.Source.TrySetResult();

			public void Fail(Exception exception) => _gate.Source.TrySetException(exception);

			public void Dispose() => _owner.ExitGate(_parent, _gate);
		}
	}
}
