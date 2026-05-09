using DG.Tweening;
using Sirenix.OdinInspector;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AK.UISystem
{
	/// <summary>
	/// A tooltip fragment with automatic positioning and lifetime management.
	/// Use ShowFragment<>() with UITooltipContext to display tooltips.
	/// Prevents duplicate tooltips and auto-hides after a duration.
	/// </summary>
	public class UIFragTooltip : UIView<UIFragTooltipContext>
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

		[Title("UI References")]
		[SerializeField] private TextMeshProUGUI _titleText;

		[SerializeField] private TextMeshProUGUI _descriptionText;

		[Title("Positioning")]
		[SerializeField] private TooltipPosition _preferredPosition = TooltipPosition.Auto;

		[SerializeField] private float _padding = 10f;

		[SerializeField] private float _screenEdgePadding = 20f;

		[Title("Lifetime")]
		[SerializeField] private float _autoHideDelay = 3f;
		
		private RectTransform _canvasRect;
		private Tween _hideTween;

		public override void OnReset()
		{
			base.OnReset();
			_canvasRect = null;
		}

		public override void OnPrepareShow()
		{
			base.OnPrepareShow();

			if (Context == null)
				return;
			
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

			// Get canvas rect for positioning
			Canvas canvas = GetComponentInParent<Canvas>();
			if (canvas != null)
				_canvasRect = canvas.GetComponent<RectTransform>();

			// Force layout rebuild
			Canvas.ForceUpdateCanvases();
			LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform);

			// Position before showing
			if (Context.Target != null)
			{
				PositionTooltip();
			}
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

	private void PositionTooltip()
	{
		RectTransform target = Context?.Target;
		if (target == null || _canvasRect == null)
			return;

		// Get sizes
		Vector2 targetSize = target.rect.size;
		Vector2 tooltipSize = RectTransform.rect.size;

		// Get the center of the target in world space (not just the pivot)
		// This accounts for targets with non-centered pivots
		Vector3[] targetCorners = new Vector3[4];
		target.GetWorldCorners(targetCorners);
		Vector3 targetWorldCenter = (targetCorners[0] + targetCorners[2]) * 0.5f;

		// Convert target center to canvas local space
		Vector2 targetLocalPos;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			_canvasRect, 
			RectTransformUtility.WorldToScreenPoint(null, targetWorldCenter), 
			null, 
			out targetLocalPos);

		// Get canvas size
		Vector2 canvasSize = _canvasRect.rect.size;

		// Calculate available space in each direction from target
		float spaceRight = (canvasSize.x * 0.5f) - (targetLocalPos.x + targetSize.x * 0.5f) - _screenEdgePadding;
		float spaceLeft = (targetLocalPos.x - targetSize.x * 0.5f) - (-canvasSize.x * 0.5f) - _screenEdgePadding;
		float spaceTop = (canvasSize.y * 0.5f) - (targetLocalPos.y + targetSize.y * 0.5f) - _screenEdgePadding;
		float spaceBottom = (targetLocalPos.y - targetSize.y * 0.5f) - (-canvasSize.y * 0.5f) - _screenEdgePadding;

		// Determine position based on context override, preference, or auto
		TooltipPosition position = Context.Position ?? _preferredPosition;
		if (position == TooltipPosition.Auto)
		{
			// Priority: Right → BottomRight → Left → BottomLeft → TopRight → TopLeft → Bottom → Top
			
			// For centered positions, we need space for the full tooltip on that side
			// The tooltip will be clamped to canvas bounds, so we just need enough space on the primary axis
			
			// Check Right (centered) - needs horizontal space, vertical centering will be clamped if needed
			if (spaceRight >= tooltipSize.x + _padding)
				position = TooltipPosition.Right;
			// Check BottomRight corner - needs both horizontal and full vertical space below
			else if (spaceRight >= tooltipSize.x + _padding && spaceBottom >= tooltipSize.y + _padding)
				position = TooltipPosition.BottomRight;
			// Check Left (centered) - needs horizontal space, vertical centering will be clamped if needed
			else if (spaceLeft >= tooltipSize.x + _padding)
				position = TooltipPosition.Left;
			// Check BottomLeft corner - needs both horizontal and full vertical space below
			else if (spaceLeft >= tooltipSize.x + _padding && spaceBottom >= tooltipSize.y + _padding)
				position = TooltipPosition.BottomLeft;
			// Check TopRight corner - needs both horizontal and full vertical space above
			else if (spaceRight >= tooltipSize.x + _padding && spaceTop >= tooltipSize.y + _padding)
				position = TooltipPosition.TopRight;
			// Check TopLeft corner - needs both horizontal and full vertical space above
			else if (spaceLeft >= tooltipSize.x + _padding && spaceTop >= tooltipSize.y + _padding)
				position = TooltipPosition.TopLeft;
			// Check Bottom (centered) - needs vertical space below, horizontal centering will be clamped if needed
			else if (spaceBottom >= tooltipSize.y + _padding)
				position = TooltipPosition.Bottom;
			// Check Top (centered) - needs vertical space above, horizontal centering will be clamped if needed
			else if (spaceTop >= tooltipSize.y + _padding)
				position = TooltipPosition.Top;
			else
				position = TooltipPosition.Right; // Default, will be clamped
		}

		// Calculate anchored position
		Vector2 anchoredPosition = CalculateAnchoredPosition(
			targetLocalPos, targetSize, tooltipSize, position,
			canvasSize);

		RectTransform.anchoredPosition = anchoredPosition;
	}

	private Vector2 CalculateAnchoredPosition(Vector2 targetPos, Vector2 targetSize,
		Vector2 tooltipSize, TooltipPosition position, Vector2 canvasSize)
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

			Vector2 desiredPos = targetPos + offset;

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

			if (_autoHideDelay > 0)
			{
				_hideTween = DOVirtual.DelayedCall(_autoHideDelay, () =>
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
	public class UIFragTooltipContext : UIContext
	{
		public string Title { get; set; }
		public string Description { get; set; }

		/// <summary>
		/// The target RectTransform to position the tooltip beside.
		/// If null, the tooltip will appear at its default position.
		/// </summary>
		public RectTransform Target { get; set; }

		/// <summary>
		/// Optional position override. If set, this will override the tooltip's preferred position.
		/// If null, uses the tooltip's configured preferred position.
		/// </summary>
		public UIFragTooltip.TooltipPosition? Position { get; set; }

		public UIFragTooltipContext()
		{
		}

		public UIFragTooltipContext(string title, string description, RectTransform target = null, UIFragTooltip.TooltipPosition? position = null)
		{
			Title = title;
			Description = description;
			Target = target;
			Position = position;
		}
	}
}
