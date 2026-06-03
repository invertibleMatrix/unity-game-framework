using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "CardFlyInAnimation", menuName = "AK/UI/Animations/Card Fly In Animation")]
    public class CardFlyInAnimationStrategy : AnimationStrategy
    {
        [Title("Spawn Settings")]
        [SerializeField] [Tooltip("Direction to fly in from")]
        private FlyDirection _flyDirection = FlyDirection.FromBottom;
        
        [SerializeField] [Tooltip("Distance to fly from")]
        private float _flyDistance = 500f;
        
        [SerializeField] [Tooltip("Initial scale")]
        private Vector3 _startScale = new Vector3(0.3f, 0.3f, 0.3f);
        
        [Title("Movement Settings")]
        [SerializeField] [Tooltip("Add curve to flight path")]
        private bool _addCurve = true;
        
        [ShowIf("_addCurve")] [SerializeField] [Tooltip("Curve intensity")]
        private float _curveIntensity = 100f;
        
        [Title("Rotation Settings")]
        [SerializeField] [Tooltip("Add rotation during flight")]
        private bool _addRotation = true;
        
        [ShowIf("_addRotation")] [SerializeField] [Tooltip("Rotation amount")]
        private float _rotationAmount = 45f;
        
        [ShowIf("_addRotation")] [SerializeField] [Tooltip("Rotation direction")]
        private RotationDirection _rotationDirection = RotationDirection.Clockwise;
        
        [Title("Timing")]
        [SerializeField] [Tooltip("Flight duration")]
        private float _flightDuration = 0.5f;
        
        [SerializeField] [Tooltip("Ease type")]
        private Ease _flightEase = Ease.OutCubic;
        
        [Title("Impact")]
        [SerializeField] [Tooltip("Add impact bounce on arrival")]
        private bool _addImpactBounce = true;
        
        [ShowIf("_addImpactBounce")] [SerializeField] [Tooltip("Bounce scale")]
        private Vector3 _bounceScale = new Vector3(1.15f, 1.15f, 1.15f);
        
        [ShowIf("_addImpactBounce")] [SerializeField] [Tooltip("Bounce duration")]
        private float _bounceDuration = 0.2f;

        public enum FlyDirection
        {
            FromTop,
            FromBottom,
            FromLeft,
            FromRight,
            FromTopLeft,
            FromTopRight,
            FromBottomLeft,
            FromBottomRight
        }
        
        public enum RotationDirection
        {
            Clockwise,
            CounterClockwise
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Calculate start position using UIUtility for proper off-screen calculation
            var startPos = GetStartPosition(target);
            target.anchoredPosition = startPos;
            target.localScale = _startScale;
            target.localEulerAngles = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Fade in quickly
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Create flight path
            if (_addCurve)
            {
                var midPoint = (startPos + Vector2.zero) * 0.5f;
                var curveOffset = GetCurveOffset();
                midPoint += curveOffset;
                
                var path = new Vector3[] { startPos, midPoint, Vector2.zero };
                sequence.Append(target.DOPath(path, _flightDuration, PathType.CatmullRom).SetEase(_flightEase));
            }
            else
            {
                sequence.Append(target.DOAnchorPos(Vector2.zero, _flightDuration).SetEase(_flightEase));
            }
            
            // Scale up
            sequence.Join(target.DOScale(Vector3.one, _flightDuration).SetEase(_flightEase));
            
            // Add rotation
            if (_addRotation)
            {
                var rotation = _rotationAmount * (_rotationDirection == RotationDirection.Clockwise ? 1f : -1f);
                sequence.Join(target.DOLocalRotate(new Vector3(0, 0, rotation), _flightDuration).SetEase(_flightEase));
            }
            
            // Impact bounce
            if (_addImpactBounce)
            {
                sequence.Append(target.DOScale(_bounceScale, _bounceDuration * 0.5f).SetEase(Ease.OutBack));
                sequence.Append(target.DOScale(Vector3.one, _bounceDuration * 0.5f).SetEase(Ease.OutBack));
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            // Fly out in opposite direction
            var endPos = GetStartPosition(target) * 1.2f;
            
            sequence.Append(target.DOAnchorPos(endPos, _flightDuration * 0.6f).SetEase(Ease.InCubic));
            sequence.Join(target.DOScale(_startScale, _flightDuration * 0.6f).SetEase(Ease.InCubic));
            sequence.Join(canvasGroup.DOFade(0, _flightDuration * 0.4f).SetEase(Ease.InQuad));
            
            return sequence.Play();
        }

        private Vector2 GetStartPosition(RectTransform target)
        {
            // Use UIUtility to get proper off-screen position based on canvas size
            var direction = _flyDirection switch
            {
                FlyDirection.FromTop => UIUtility.SlideDirection.FromTop,
                FlyDirection.FromBottom => UIUtility.SlideDirection.FromBottom,
                FlyDirection.FromLeft => UIUtility.SlideDirection.FromLeft,
                FlyDirection.FromRight => UIUtility.SlideDirection.FromRight,
                FlyDirection.FromTopLeft => UIUtility.SlideDirection.FromTop,
                FlyDirection.FromTopRight => UIUtility.SlideDirection.FromTop,
                FlyDirection.FromBottomLeft => UIUtility.SlideDirection.FromBottom,
                FlyDirection.FromBottomRight => UIUtility.SlideDirection.FromBottom,
                _ => UIUtility.SlideDirection.FromBottom
            };
            
            var offset = new Vector2(_flyDistance, _flyDistance);
            return UIUtility.GetOffScreenPosition(target, direction, offset);
        }

        private Vector2 GetCurveOffset()
        {
            return _flyDirection switch
            {
                FlyDirection.FromTop or FlyDirection.FromBottom => new Vector2(_curveIntensity, 0),
                FlyDirection.FromLeft or FlyDirection.FromRight => new Vector2(0, _curveIntensity),
                FlyDirection.FromTopLeft or FlyDirection.FromBottomRight => new Vector2(_curveIntensity, -_curveIntensity),
                FlyDirection.FromTopRight or FlyDirection.FromBottomLeft => new Vector2(-_curveIntensity, -_curveIntensity),
                _ => Vector2.zero
            };
        }
    }
}