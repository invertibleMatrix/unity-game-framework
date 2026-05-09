using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem
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
		[Title("Channel Settings")]
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

		/// <summary>
		/// Initializes the channel's Canvas with the correct settings.
		/// Called by UIViewSystem when the view is being set up.
		/// </summary>
		internal void Initialize(Camera uiCamera, int stackDepth)
		{
			Canvas = GetComponent<Canvas>();
			Canvas.renderMode = _renderMode;

			if (_renderMode == RenderMode.ScreenSpaceCamera)
			{
				Canvas.worldCamera = uiCamera;
			}

			Canvas.sortingOrder = (int)(_sortOrder + stackDepth);
		}

		/// <summary>
		/// Updates the sorting order based on current stack depth.
		/// </summary>
		internal void UpdateSortingOrder(int stackDepth)
		{
			if (Canvas != null)
			{
				Canvas.sortingOrder = (int)(_sortOrder + stackDepth);
			}
		}
	}
}

