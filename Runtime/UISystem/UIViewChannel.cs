using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Attach this component to any UIView that should manage its own Canvas and sorting order.
	/// A UIView with UIChannel is conceptually what UIScreen was in V1 — it owns a Canvas,
	/// participates in channel-based stacking, and can host child views.
	///
	/// A UIView WITHOUT UIChannel is conceptually what UIFragment was in V1 — it lives inside
	/// a parent that has a channel.
	///
	/// There is always a DefaultChannel (sort order 0) as a fallback for views that don't
	/// specify an explicit parent.
	/// </summary>
	[RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
	public class UIViewChannel : MonoBehaviour
	{
		[SerializeField, Tooltip("Base sorting order for this channel. Higher values render on top. " +
		                         "Stack depth is added on top of this at runtime.")]
		private UIChannel _sortOrder = UIChannel.HUD;

		[SerializeField, Tooltip("Render mode for the Canvas managed by this channel.")]
		private RenderMode _renderMode = RenderMode.ScreenSpaceCamera;

		/// <summary>
		/// The base sorting order of this channel.
		/// The system adds stack depth on top of this at runtime.
		/// </summary>
		public UIChannel SortOrder => _sortOrder;

		/// <summary>
		/// The render mode the Canvas should use.
		/// </summary>
		public RenderMode RenderMode => _renderMode;

		/// <summary>
		/// The Canvas managed by this channel. Set up by UIViewSystem during initialization.
		/// </summary>
		public Canvas Canvas { get; private set; }

		private UIChannel _stackChannel;
		private int       _stackDepth;
		private int?      _raisedSortingOrder;

		/// <summary>
		/// The sorting order the screen's stack gives it: the channel it was pushed on plus its
		/// depth there (the bottom screen is depth 1).
		/// </summary>
		internal int StackSortingOrder => (int)_stackChannel + _stackDepth;

		/// <summary>
		/// Sets the Canvas up when the screen is pushed: its render mode, the UI camera, and the
		/// sorting order for <paramref name="stackDepth"/> on <paramref name="channel"/> — the
		/// stack the screen was pushed on, which is <see cref="SortOrder"/> unless the show chose another.
		/// </summary>
		internal void Initialize(Camera uiCamera, UIChannel channel, int stackDepth)
		{
			Canvas = GetComponent<Canvas>();
			Canvas.renderMode = _renderMode;

			if (_renderMode == RenderMode.ScreenSpaceCamera)
			{
				Canvas.worldCamera = uiCamera;
			}

			_stackChannel = channel;
			UpdateSortingOrder(stackDepth);
		}

		/// <summary>Moves the screen to <paramref name="stackDepth"/> on its stack. A raised screen stays raised.</summary>
		internal void UpdateSortingOrder(int stackDepth)
		{
			_stackDepth = stackDepth;
			ApplySortingOrder();
		}

		/// <summary>
		/// Sorts the screen at <paramref name="sortingOrder"/> until <see cref="Lower"/>, wherever
		/// its stack moves it meanwhile. For a highlight that lifts one screen above the rest.
		/// </summary>
		internal void Raise(int sortingOrder)
		{
			_raisedSortingOrder = sortingOrder;
			ApplySortingOrder();
		}

		/// <summary>Returns a raised screen to the sorting order its stack gives it now.</summary>
		internal void Lower()
		{
			_raisedSortingOrder = null;
			ApplySortingOrder();
		}

		private void ApplySortingOrder()
		{
			if (Canvas != null)
			{
				Canvas.sortingOrder = _raisedSortingOrder ?? StackSortingOrder;
			}
		}
	}
}

