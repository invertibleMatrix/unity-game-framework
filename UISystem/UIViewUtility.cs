using UnityEngine;

namespace AK.UISystem
{
	public enum SlideDirection
	{
		FromTop,
		FromBottom,
		FromLeft,
		FromRight
	}

	/// <summary>
	/// Utility helpers for the V2 UI system.
	/// </summary>
	public static class UIViewUtility
	{
		public static Vector2 GetOffScreenPosition(RectTransform target, SlideDirection direction, Vector2 offset = default)
		{
			var rootCanvas = target.GetComponentInParent<Canvas>().rootCanvas;
			var canvasRect = rootCanvas.GetComponent<RectTransform>().rect;
			var targetRect = target.rect;

			float xPos = 0f, yPos = 0f;

			switch (direction)
			{
				case SlideDirection.FromTop:
					yPos = (canvasRect.height / 2f) + (targetRect.height * target.pivot.y) + offset.y;
					break;
				case SlideDirection.FromBottom:
					yPos = -(canvasRect.height / 2f) - (targetRect.height * (1f - target.pivot.y)) - offset.y;
					break;
				case SlideDirection.FromLeft:
					xPos = -(canvasRect.width / 2f) - (targetRect.width * (1f - target.pivot.x)) - offset.x;
					break;
				case SlideDirection.FromRight:
					xPos = (canvasRect.width / 2f) + (targetRect.width * target.pivot.x) + offset.x;
					break;
			}

			return new Vector2(xPos, yPos);
		}

		public static SlideDirection GetOppositeDirection(SlideDirection direction)
		{
			return direction switch
			{
				SlideDirection.FromTop    => SlideDirection.FromBottom,
				SlideDirection.FromBottom => SlideDirection.FromTop,
				SlideDirection.FromLeft   => SlideDirection.FromRight,
				SlideDirection.FromRight  => SlideDirection.FromLeft,
				_                         => direction
			};
		}
	}
}

