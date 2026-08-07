using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AK.Systems
{
	/// <summary>
	/// A tooltip fragment with automatic positioning and lifetime management.
	/// Use ShowFragment<>() with UITooltipContext to display tooltips.
	/// Prevents duplicate tooltips and auto-hides after a duration.
	/// </summary>
	public class UIViewTooltip : UIView<UIViewTooltipContext>
	{
	public enum TooltipPosition
	{
		Auto,
		Top,
		Bottom,
		Left,
		Right,
		TopLeft,
		TopRight,
		BottomLeft,
		BottomRight
	}

		[SerializeField] private TextMeshProUGUI _titleText;

		[SerializeField] private TextMeshProUGUI _descriptionText;

		[SerializeField] private Image _iconImage;

		[SerializeField] private TextMeshProUGUI _pageIndicatorText;

		[SerializeField] private Button _closeButton;

		[SerializeField] private TooltipPosition _preferredPosition = TooltipPosition.Auto;

		[SerializeField] private float _padding = 10f;

		[SerializeField] private float _screenEdgePadding = 20f;

		[SerializeField] private float _autoHideDelay = 3f;

		private Canvas _canvas;
		private Tween  _hideTween;

		public override void OnReset()
		{
			base.OnReset();
			_canvas = null;
		}

		public override void OnPrepareShow()
		{
			base.OnPrepareShow();

			if (Context == null)
				return;

			var canvas = GetComponentInParent<Canvas>();
			_canvas = canvas != null ? canvas.rootCanvas : null;

			if (_titleText != null)
			{
				_titleText.text = Context.Title;
				_titleText.gameObject.SetActive(!string.IsNullOrEmpty(Context.Title));
			}

			if (_descriptionText != null)
			{
				_descriptionText.text = Context.Description;
				_descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(Context.Description));
			}

			if (_iconImage != null)
			{
				_iconImage.sprite = Context.Icon;
				_iconImage.gameObject.SetActive(Context.Icon != null);
			}

			if (_pageIndicatorText != null)
			{
				_pageIndicatorText.text = Context.PageIndicator;
				_pageIndicatorText.gameObject.SetActive(!string.IsNullOrEmpty(Context.PageIndicator));
			}

			if (_closeButton != null)
			{
				_closeButton.gameObject.SetActive(Context.TapAnywhereToClose);
				if (Context.TapAnywhereToClose)
				{
					SizeCloseButtonToCanvas();
				}
			}

			// Force layout rebuild
			Canvas.ForceUpdateCanvases();
			LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform);

			// Position before showing
			if (Context.Target != null)
			{
				PositionTooltip();
			}
		}

		public override void RegisterResources()
		{
			if (_closeButton != null)
			{
				_closeButton.onClick.AddListener(OnCloseButtonClicked);
			}
		}

		public override void UnRegisterResources()
		{
			if (_closeButton != null)
			{
				_closeButton.onClick.RemoveListener(OnCloseButtonClicked);
			}
		}

		private void OnCloseButtonClicked()
		{
			Close();
		}

		public override void OnShow()
		{
			base.OnShow();
			StartAutoHide();
		}

		public override void OnHide()
		{
			base.OnHide();
			_hideTween?.Kill();
		}

		// Pause swallows the one-shot auto-hide timer (OnHide isn't called, and the
		// IsVisible guard eats the delayed call); re-arm it when the tooltip resurfaces.
		public override void OnResume()
		{
			base.OnResume();
			StartAutoHide();
		}

		private void PositionTooltip()
		{
			RectTransform target = Context?.Target;
			if (target == null || _canvas == null)
				return;

			// All math happens against the root canvas (full screen), so the tooltip
			// positions correctly under any parent — including a tutorial-mode view
			// whose own canvas has a much smaller rect.
			RectTransform canvasRect = (RectTransform)_canvas.transform;

			// Get sizes
			Vector2 targetSize = target.rect.size;
			Vector2 tooltipSize = RectTransform.rect.size;

			// Get the center of the target in world space (not just the pivot)
			// This accounts for targets with non-centered pivots
			Vector3[] targetCorners = new Vector3[4];
			target.GetWorldCorners(targetCorners);
			Vector3 targetWorldCenter = (targetCorners[0] + targetCorners[2]) * 0.5f;

			// Convert target center to canvas local space.
			// A null camera is only valid for ScreenSpaceOverlay; the framework's UIViewChannel
			// defaults to ScreenSpaceCamera, so pass the canvas's world camera explicitly.
			Camera canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
				? null
				: _canvas.worldCamera;

			RectTransformUtility.ScreenPointToLocalPointInRectangle(
				canvasRect,
				RectTransformUtility.WorldToScreenPoint(canvasCamera, targetWorldCenter),
				canvasCamera,
				out Vector2 targetLocalPos);

			// Get canvas size
			Vector2 canvasSize = canvasRect.rect.size;

		// Calculate available space in each direction from target
		float spaceRight = (canvasSize.x * 0.5f) - (targetLocalPos.x + targetSize.x * 0.5f) - _screenEdgePadding;
		float spaceLeft = (targetLocalPos.x - targetSize.x * 0.5f) - (-canvasSize.x * 0.5f) - _screenEdgePadding;
		float spaceTop = (canvasSize.y * 0.5f) - (targetLocalPos.y + targetSize.y * 0.5f) - _screenEdgePadding;
		float spaceBottom = (targetLocalPos.y - targetSize.y * 0.5f) - (-canvasSize.y * 0.5f) - _screenEdgePadding;

		// Determine position based on context override, preference, or auto
		TooltipPosition position = Context.Position ?? _preferredPosition;
		if (position == TooltipPosition.Auto)
		{
			// Priority: Right → BottomRight → Left → BottomLeft → TopRight → TopLeft → Bottom → Top.
			// Centered placements need the full tooltip on the primary axis plus vertical/horizontal
			// centering room; corner placements need the full height on their secondary axis.
			// (The previous cascade repeated the primary condition in the corner branches, which
			// made every corner branch unreachable.)
			var halfW = tooltipSize.x * 0.5f + _padding;
			var halfH = tooltipSize.y * 0.5f + _padding;

			bool fitsRight    = spaceRight  >= tooltipSize.x + _padding;
			bool fitsLeft     = spaceLeft   >= tooltipSize.x + _padding;
			bool fitsTop      = spaceTop    >= tooltipSize.y + _padding;
			bool fitsBottom   = spaceBottom >= tooltipSize.y + _padding;
			bool fitsVCenter  = spaceTop >= halfH && spaceBottom >= halfH;
			bool fitsHCenter  = spaceLeft >= halfW && spaceRight >= halfW;

			if (fitsRight && fitsVCenter)
				position = TooltipPosition.Right;
			else if (fitsRight && fitsBottom)
				position = TooltipPosition.BottomRight;
			else if (fitsLeft && fitsVCenter)
				position = TooltipPosition.Left;
			else if (fitsLeft && fitsBottom)
				position = TooltipPosition.BottomLeft;
			else if (fitsRight && fitsTop)
				position = TooltipPosition.TopRight;
			else if (fitsLeft && fitsTop)
				position = TooltipPosition.TopLeft;
			else if (fitsBottom && fitsHCenter)
				position = TooltipPosition.Bottom;
			else if (fitsTop && fitsHCenter)
				position = TooltipPosition.Top;
			else
				position = TooltipPosition.Right; // Default, will be clamped
		}

		// Calculate anchored position
		Vector2 anchoredPosition = CalculateAnchoredPosition(
			targetLocalPos, targetSize, tooltipSize, position,
			canvasSize, Context.Offset);

		SetPositionInParentSpace(anchoredPosition);
	}

	// The computed position is the tooltip's center in root-canvas space; convert it into
	// the actual parent's local space so it lands correctly under any parent and pivot.
	private void SetPositionInParentSpace(Vector2 canvasLocalCenter)
	{
		if (RectTransform.parent is not RectTransform parent)
			return;

		Vector3 worldCenter = _canvas.transform.TransformPoint(canvasLocalCenter);
		Vector2 parentLocalCenter = parent.InverseTransformPoint(worldCenter);

		Vector2 pivot = RectTransform.pivot;
		Vector2 size = RectTransform.rect.size;
		Vector2 pivotOffset = new Vector2((pivot.x - 0.5f) * size.x, (pivot.y - 0.5f) * size.y);

		RectTransform.localPosition = new Vector3(
			parentLocalCenter.x + pivotOffset.x,
			parentLocalCenter.y + pivotOffset.y,
			RectTransform.localPosition.z);
	}

	// Same generous-coverage trick as UIView.ShowBackgroundOverlay — the button must catch
	// taps across the whole screen even when the tooltip lives inside a small parent.
	private void SizeCloseButtonToCanvas()
	{
		if (_closeButton == null || _canvas == null)
			return;

		RectTransform buttonRect = (RectTransform)_closeButton.transform;
		RectTransform canvasRect = (RectTransform)_canvas.transform;

		Vector3[] canvasCorners = new Vector3[4];
		canvasRect.GetWorldCorners(canvasCorners);

		Vector2 bottomLeft = RectTransform.InverseTransformPoint(canvasCorners[0]);
		Vector2 topRight = RectTransform.InverseTransformPoint(canvasCorners[2]);

		Vector2 center = (bottomLeft + topRight) * 0.5f;
		Vector2 size = (topRight - bottomLeft) * 4f;

		buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
		buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
		buttonRect.pivot = new Vector2(0.5f, 0.5f);
		buttonRect.anchoredPosition = center;
		buttonRect.sizeDelta = size;
	}

	private Vector2 CalculateAnchoredPosition(Vector2 targetPos, Vector2 targetSize,
		Vector2 tooltipSize, TooltipPosition position, Vector2 canvasSize, Vector2 extraOffset)
	{
		Vector2 offset = Vector2.zero;

		switch (position)
		{
			// Literal directional positions (centered on that side)
			case TooltipPosition.Top:
				// Directly above, horizontally centered
				offset = new Vector2(0, targetSize.y * 0.5f + tooltipSize.y * 0.5f + _padding);
				break;
			case TooltipPosition.Bottom:
				// Directly below, horizontally centered
				offset = new Vector2(0, -(targetSize.y * 0.5f + tooltipSize.y * 0.5f + _padding));
				break;
			case TooltipPosition.Left:
				// Directly left, vertically centered
				offset = new Vector2(-(targetSize.x * 0.5f + tooltipSize.x * 0.5f + _padding), 0);
				break;
			case TooltipPosition.Right:
				// Directly right, vertically centered
				offset = new Vector2(targetSize.x * 0.5f + tooltipSize.x * 0.5f + _padding, 0);
				break;
				
			// Corner positions
			case TooltipPosition.TopLeft:
				// Top-left corner
				offset = new Vector2(
					-(targetSize.x * 0.5f + tooltipSize.x * 0.5f + _padding),
					targetSize.y * 0.5f + tooltipSize.y * 0.5f + _padding);
				break;
			case TooltipPosition.TopRight:
				// Top-right corner
				offset = new Vector2(
					targetSize.x * 0.5f + tooltipSize.x * 0.5f + _padding,
					targetSize.y * 0.5f + tooltipSize.y * 0.5f + _padding);
				break;
			case TooltipPosition.BottomLeft:
				// Bottom-left corner
				offset = new Vector2(
					-(targetSize.x * 0.5f + tooltipSize.x * 0.5f + _padding),
					-(targetSize.y * 0.5f + tooltipSize.y * 0.5f + _padding));
				break;
			case TooltipPosition.BottomRight:
				// Bottom-right corner
				offset = new Vector2(
					targetSize.x * 0.5f + tooltipSize.x * 0.5f + _padding,
					-(targetSize.y * 0.5f + tooltipSize.y * 0.5f + _padding));
				break;
		}

			Vector2 desiredPos = targetPos + offset + extraOffset;

			// Clamp to canvas bounds
			float halfCanvasWidth = canvasSize.x * 0.5f;
			float halfCanvasHeight = canvasSize.y * 0.5f;
			float halfTooltipWidth = tooltipSize.x * 0.5f;
			float halfTooltipHeight = tooltipSize.y * 0.5f;

			float minX = -halfCanvasWidth + halfTooltipWidth + _screenEdgePadding;
			float maxX = halfCanvasWidth - halfTooltipWidth - _screenEdgePadding;
			float minY = -halfCanvasHeight + halfTooltipHeight + _screenEdgePadding;
			float maxY = halfCanvasHeight - halfTooltipHeight - _screenEdgePadding;

			return new Vector2(
				Mathf.Clamp(desiredPos.x, minX, maxX),
				Mathf.Clamp(desiredPos.y, minY, maxY));
		}

	private void StartAutoHide()
	{
		_hideTween?.Kill();

		float closeTime = Context?.CloseTime ?? _autoHideDelay;
		if (closeTime > 0f)
		{
			_hideTween = DOVirtual.DelayedCall(closeTime, () =>
			{
				if (IsVisible)
				{
					Close();
				}
			}).Play();
		}
	}

		protected override void OnDestroy()
		{
			_hideTween?.Kill();
			base.OnDestroy();
		}
	}

	/// <summary>
	/// Context data for tooltip fragments.
	/// </summary>
	public class UIViewTooltipContext : UIContext
	{
	public string Title { get; set; }
	public string Description { get; set; }

	/// <summary>
	/// Optional icon shown in the tooltip. If null, the icon slot is hidden.
	/// </summary>
	public Sprite Icon { get; set; }

	/// <summary>
	/// Optional step/page text (e.g. "5/14"). If null or empty, the indicator slot is hidden.
	/// </summary>
	public string PageIndicator { get; set; }

	/// <summary>
	/// If true, a full-screen transparent button behind the tooltip closes it on any tap.
	/// </summary>
	public bool TapAnywhereToClose { get; set; }

	/// <summary>
	/// Per-show auto-close time in seconds. Null uses the prefab's auto-hide delay;
	/// zero disables auto-close; any positive value overrides the delay for this show.
	/// </summary>
	public float? CloseTime { get; set; }

	/// <summary>
	/// The target RectTransform to position the tooltip beside.
	/// If null, the tooltip will appear at its default position.
	/// </summary>
	public RectTransform Target { get; set; }

	/// <summary>
	/// Optional extra offset in canvas units applied on top of the computed position
	/// (before edge clamping). Use to push the tooltip further from its target.
	/// </summary>
	public Vector2 Offset { get; set; }

	/// <summary>
	/// Optional position override. If set, this will override the tooltip's preferred position.
	/// If null, uses the tooltip's configured preferred position.
	/// </summary>
	public UIViewTooltip.TooltipPosition? Position { get; set; }

		public UIViewTooltipContext()
		{
		}

		public UIViewTooltipContext(string title = "", string description= "", RectTransform target = null, UIViewTooltip.TooltipPosition? position = null)
		{
			Title = title;
			Description = description;
			Target = target;
			Position = position;
		}
	}
}
