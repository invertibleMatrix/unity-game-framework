using UnityEngine;
using UnityEngine.UI;

namespace AK.Systems
{
	/// <summary>
	/// Raises one view above everything else and dims the rest, for as long as a tutorial (or
	/// any other caller) needs the player's attention on it. Added on demand — a view knows
	/// nothing about being highlighted. <see cref="Enter()"/> and <see cref="Exit()"/> are idempotent.
	/// Not to be confused with <see cref="UIViewSpotlight"/>, the rounded cut-out tutorials point at a target with.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ViewHighlight : MonoBehaviour
	{
		private const float ExitFadeDuration = 0.4f;

		private ViewBackgroundOverlay _overlay;
		private bool                  _ownsOverlay;
		private Canvas                _canvas;
		private GraphicRaycaster      _raycaster;
		private UIViewChannel         _channel;

		public bool IsActive { get; private set; }

		/// <summary>Highlights <paramref name="view"/>, adding the component if it has none.</summary>
		public static ViewHighlight Enter(UIView view)
		{
			if (!view.TryGetComponent(out ViewHighlight highlight))
			{
				highlight = view.gameObject.AddComponent<ViewHighlight>();
			}

			highlight.Enter();
			return highlight;
		}

		/// <summary>Ends the highlight on <paramref name="view"/> if it has one.</summary>
		public static void Exit(UIView view)
		{
			if (view != null && view.TryGetComponent(out ViewHighlight highlight))
			{
				highlight.Exit();
			}
		}

		[ContextMenu("Enter")]
		public void Enter()
		{
			Restore();
			IsActive = true;

			if (!TryGetComponent(out _overlay))
			{
				_overlay = gameObject.AddComponent<ViewBackgroundOverlay>();
				_ownsOverlay = true;
			}

			_overlay.FadeIn(ViewBackgroundOverlay.DefaultAlpha, blockRaycasts: true);

			if (TryGetComponent(out _channel))
			{
				_channel.Canvas.overrideSorting = true;
				_channel.Canvas.sortingOrder = (int)UIChannel.Overlay + 1;
				return;
			}

			// Sort just above the parent canvas so the dim covers the parent but stays below
			// Overlay (200) — the tooltip/spotlight canvas lives there and must render above the dim.
			Canvas parentCanvas = GetComponentInParent<Canvas>();
			_canvas = gameObject.AddComponent<Canvas>();
			_canvas.overrideSorting = true;
			_canvas.sortingOrder = (parentCanvas != null ? parentCanvas.sortingOrder : 0) + 1;
			_raycaster = gameObject.AddComponent<GraphicRaycaster>();
		}

		/// <summary>Fades the dim out, then restores sorting once it is gone so the re-layering is masked by the fade.</summary>
		[ContextMenu("Exit")]
		public void Exit()
		{
			if (!IsActive) return;

			if (!Application.isPlaying || _overlay == null || !_overlay.IsBuilt)
			{
				Restore();
				return;
			}

			_overlay.FadeOut(ExitFadeDuration, Restore);
		}

		/// <summary>Immediate teardown: no fade. Idempotent.</summary>
		public void Restore()
		{
			IsActive = false;

			if (_overlay != null)
			{
				if (_ownsOverlay) DestroyObject(_overlay);
				else _overlay.Clear();
			}

			_overlay = null;
			_ownsOverlay = false;

			DestroyObject(_raycaster);
			_raycaster = null;

			DestroyObject(_canvas);
			_canvas = null;

			if (_channel != null)
			{
				// Only the override flag. The UISystem owns sortingOrder (channel + stack depth);
				// resetting it here would re-layer this screen under screens it should sit above.
				_channel.Canvas.overrideSorting = false;
				_channel = null;
			}
		}

		private void OnDestroy()
		{
			Restore();
		}

		private static void DestroyObject(Object o)
		{
			if (o == null) return;
			if (Application.isPlaying) Destroy(o);
			else DestroyImmediate(o);
		}
	}
}
