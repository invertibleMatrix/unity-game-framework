using AK.Kernel.Timing;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace AK.Systems
{
	/// <summary>
	/// Raises one view above everything else and dims the rest, for as long as a tutorial (or
	/// any other caller) needs the player's attention on it. Added on demand — a view knows
	/// nothing about being highlighted. <see cref="Enter()"/> and <see cref="Exit()"/> are idempotent:
	/// entering an active highlight keeps it (and cancels an exit fade in flight); exiting an
	/// inactive one does nothing. Components the view already carries (a Canvas, a
	/// GraphicRaycaster, a ViewBackgroundOverlay) are borrowed and handed back on
	/// <see cref="Restore"/>; only what the highlight added itself is destroyed. The dim fades on
	/// the view's <see cref="UIView.TimeDomain"/>.
	/// Not to be confused with <see cref="UIViewSpotlight"/>, the rounded cut-out tutorials point at a target with.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ViewHighlight : MonoBehaviour
	{
		private const float ExitFadeDuration = 0.4f;

		private ViewBackgroundOverlay _overlay;
		private bool                  _ownsOverlay;
		private Canvas                _canvas;
		private bool                  _ownsCanvas;
		private bool                  _canvasOverrideSorting;
		private int                   _canvasSortingOrder;
		private GraphicRaycaster      _raycaster;
		private bool                  _ownsRaycaster;
		private UIViewChannel         _channel;
		private bool                  _channelOverrideSorting;
		private bool                  _exiting;
		private TimeDomain            _time = TimeDomain.Unscaled;
		private bool                  _enterDeferred;
		private int                   _teardownFrame = -1;

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
			if (IsActive)
			{
				if (_exiting)
				{
					_exiting = false;
					_overlay.FadeIn(ViewBackgroundOverlay.DefaultAlpha, blockRaycasts: true, _time);
					return;
				}

				Debug.LogWarning($"'{name}' was highlighted again while already highlighted; the first highlight is kept.", this);
				return;
			}

			// Components Restore destroyed this frame are still attached until the end of it:
			// they would be borrowed as if alive and AddComponent would refuse a replacement.
			if (Application.isPlaying && _teardownFrame == Time.frameCount)
			{
				if (!_enterDeferred)
				{
					_enterDeferred = true;
					EnterNextFrame().Forget();
				}

				return;
			}

			IsActive = true;
			_time = TryGetComponent(out UIView view) ? view.TimeDomain : TimeDomain.Unscaled;

			if (!TryGetComponent(out _overlay))
			{
				_overlay = gameObject.AddComponent<ViewBackgroundOverlay>();
				_ownsOverlay = true;
			}

			_overlay.FadeIn(ViewBackgroundOverlay.DefaultAlpha, blockRaycasts: true, _time);

			if (TryGetComponent(out _channel) && _channel.Canvas != null)
			{
				_channelOverrideSorting = _channel.Canvas.overrideSorting;
				_channel.Canvas.overrideSorting = true;
				_channel.Raise((int)UIChannel.Overlay + 1);
				return;
			}

			_channel = null;

			// Sort just above the parent canvas so the dim covers the parent but stays below
			// Overlay (200) — the tooltip/spotlight canvas lives there and must render above the dim.
			Canvas parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;

			if (TryGetComponent(out _canvas))
			{
				_canvasOverrideSorting = _canvas.overrideSorting;
				_canvasSortingOrder = _canvas.sortingOrder;
			}
			else
			{
				_canvas = gameObject.AddComponent<Canvas>();
				_ownsCanvas = true;
			}

			_canvas.overrideSorting = true;
			_canvas.sortingOrder = (parentCanvas != null ? parentCanvas.sortingOrder : 0) + 1;

			if (!TryGetComponent(out _raycaster))
			{
				_raycaster = gameObject.AddComponent<GraphicRaycaster>();
				_ownsRaycaster = true;
			}
		}

		/// <summary>Fades the dim out, then restores sorting once it is gone so the re-layering is masked by the fade.</summary>
		[ContextMenu("Exit")]
		public void Exit()
		{
			_enterDeferred = false;
			if (!IsActive || _exiting) return;

			if (!Application.isPlaying || _overlay == null || !_overlay.IsBuilt)
			{
				Restore();
				return;
			}

			_exiting = true;
			_overlay.FadeOut(ExitFadeDuration, _time, OnExitFadeComplete);
		}

		/// <summary>Immediate teardown: no fade. Idempotent.</summary>
		public void Restore()
		{
			IsActive = false;
			_exiting = false;
			_enterDeferred = false;

			if (_overlay != null)
			{
				if (_ownsOverlay) DestroySafely(_overlay);
				else _overlay.Clear();
			}

			_overlay = null;
			_ownsOverlay = false;

			if (_raycaster != null && _ownsRaycaster) DestroySafely(_raycaster);
			_raycaster = null;
			_ownsRaycaster = false;

			if (_canvas != null)
			{
				if (_ownsCanvas)
				{
					DestroySafely(_canvas);
				}
				else
				{
					_canvas.overrideSorting = _canvasOverrideSorting;
					_canvas.sortingOrder = _canvasSortingOrder;
				}
			}

			_canvas = null;
			_ownsCanvas = false;

			if (_channel != null)
			{
				// The channel hands the screen back to the order its stack gives it now, which
				// may have changed while it was raised.
				if (_channel.Canvas != null) _channel.Canvas.overrideSorting = _channelOverrideSorting;
				_channel.Lower();
				_channel = null;
			}
		}

		private void OnExitFadeComplete()
		{
			if (_exiting) Restore();
		}

		private async UniTaskVoid EnterNextFrame()
		{
			await UniTask.NextFrame();
			if (this == null || !_enterDeferred) return;

			_enterDeferred = false;
			Enter();
		}

		private void OnDestroy()
		{
			Restore();
		}

		private void DestroySafely(Object o)
		{
			if (o == null) return;

			if (Application.isPlaying)
			{
				Destroy(o);
				_teardownFrame = Time.frameCount;
			}
			else
			{
				DestroyImmediate(o);
			}
		}
	}
}
