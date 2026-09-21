using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace AK.Systems
{
	/// <summary>
	/// A registered view as the system sees it: who hosts it, whether it is pre-placed or
	/// spawned, which children it has, and the per-show state the view itself must not carry.
	/// </summary>
	internal sealed class ViewRecord
	{
		private UniTaskCompletionSource _closeSettled;

		public UIView       Instance  { get; }
		public UIView       Parent    { get; }
		public bool         IsStatic  { get; }
		public bool         IsDynamic => !IsStatic;
		public List<UIView> Children  { get; } = new();

		/// <summary>Spawned from a template. Pool keys are prefab-based, so clones destroy on close.</summary>
		public bool IsClone { get; }

		/// <summary>Channel this screen was shown on when it differs from its component's sort order.</summary>
		public UIChannel? ChannelOverride;

		/// <summary>Set for the duration of a close, so a second close and every lookup skip the view.</summary>
		public bool IsClosing;

		/// <summary>Kind under which this record is indexed. Captured at registration.</summary>
		public readonly ViewKey Kind;

		/// <summary>Next record of the same kind in the registry index.</summary>
		public ViewRecord NextOfKind;

		public ViewRecord(UIView instance, UIView parent, bool isStatic, bool isClone = false)
		{
			Instance = instance;
			Parent = parent;
			IsStatic = isStatic;
			IsClone = isClone;
			Kind = ViewKey.Of(instance);
		}

		/// <summary>The GameObject has not been destroyed under us.</summary>
		public bool IsAlive => Instance != null;

		/// <summary>Dynamic, not a clone, and asks for pooling.</summary>
		public bool ShouldPool => IsDynamic && !IsClone && Instance.ReturnToPoolOnClose;

		/// <summary>The channel stack a screen was pushed onto: its override when one was supplied at show time, else the component's sort order.</summary>
		public UIChannel EffectiveChannel => ChannelOverride ?? Instance.Channel.SortOrder;

		/// <summary>
		/// Completes when the close in flight leaves its <see cref="IsClosing"/> state —
		/// however it ended. Completed immediately when nothing is closing.
		/// </summary>
		public UniTask WhenCloseSettled()
		{
			if (!IsClosing) return UniTask.CompletedTask;

			_closeSettled ??= new UniTaskCompletionSource();
			return _closeSettled.Task;
		}

		public void SignalCloseSettled()
		{
			_closeSettled?.TrySetResult();
			_closeSettled = null;
		}

		public void AddChild(UIView child)
		{
			if (child != null && !Children.Contains(child))
				Children.Add(child);
		}

		public void RemoveChild(UIView child)
		{
			Children.Remove(child);
		}
	}
}
