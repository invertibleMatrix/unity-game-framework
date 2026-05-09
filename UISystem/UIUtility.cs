using UnityEngine;

namespace AK.UISystem
{
	public static class UIUtility
	{
		public enum SlideDirection
		{
			FromTop,
			FromBottom,
			FromLeft,
			FromRight
		}

		public static Vector2 GetOffScreenPosition(RectTransform target, SlideDirection direction, Vector2 offset = default)
		{
			var rootCanvas = target.GetComponentInParent<Canvas>().rootCanvas;
			var canvasRect = rootCanvas.GetComponent<RectTransform>().rect;
			var targetRect = target.rect;

			float xPos = 0, yPos = 0;

			// ensuring the entire element is placed off-screen at edges
			switch (direction)
			{
				case SlideDirection.FromTop:
					yPos = (canvasRect.height / 2) + (targetRect.height * target.pivot.y);
					yPos += offset.y;
					break;
				case SlideDirection.FromBottom:
					yPos = -(canvasRect.height / 2) - (targetRect.height * (1 - target.pivot.y));
					yPos -= offset.y;
					break;
				case SlideDirection.FromLeft:
					xPos = -(canvasRect.width / 2) - (targetRect.width * (1 - target.pivot.x));
					xPos -= offset.x;
					break;
				case SlideDirection.FromRight:
					xPos = (canvasRect.width / 2) + (targetRect.width * target.pivot.x);
					xPos += offset.x;
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