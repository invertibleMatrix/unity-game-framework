using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "CardArcAnimation", menuName = "Gameplay/UI/Animations/Card Arc Animation")]
    public class CardArcAnimationStrategy : AnimationStrategy
    {
        [Title("Arc Settings")]
        [SerializeField] [Tooltip("Start direction")]
        private UIUtility.SlideDirection _startDirection = UIUtility.SlideDirection.FromTop;
        
        [SerializeField] [Tooltip("Arc height")]
        private float _arcHeight = 150f;
        
        [SerializeField] [Tooltip("Arc direction")]
        private ArcDirection _arcDirection = ArcDirection.Up;
        
        [SerializeField] [Tooltip("Start offset")]
        private Vector2 _startOffset = new Vector2(250, 250);
        
        [Title("Scale Settings")]
        [SerializeField] [Tooltip("Start scale")]
        private Vector3 _startScale = new Vector3(0.2f, 0.2f, 0.2f);
        
        [SerializeField] [Tooltip("Mid arc scale (peak)")]
        private Vector3 _midScale = new Vector3(1.2f, 1.2f, 1.2f);
        
        [Title("Rotation Settings")]
        [SerializeField] [Tooltip("Add rotation during arc")]
        private bool _addRotation = true;
        
        [ShowIf("_addRotation")] [SerializeField] [Tooltip("Total rotation")]
        private float _totalRotation = 180f;
        
        [ShowIf("_addRotation")] [SerializeField] [Tooltip("Rotation direction")]
        private RotationDirection _rotationDirection = RotationDirection.Clockwise;
        
        [Title("Timing")]
        [SerializeField] [Tooltip("Arc duration")]
        private float _arcDuration = 0.6f;
        
        [SerializeField] [Tooltip("Ease type")]
        private Ease _arcEase = Ease.InOutCubic;
        
        [Title("Impact")]
        [SerializeField] [Tooltip("Add impact bounce at end")]
        private bool _addImpactBounce = true;
        
        [ShowIf("_addImpactBounce")] [SerializeField] [Tooltip("Bounce intensity")]
        private float _bounceIntensity = 0.08f;

        public enum ArcDirection
        {
            Up,
            Down
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
            var startPos = UIUtility.GetOffScreenPosition(target, _startDirection, _startOffset);
            target.anchoredPosition = startPos;
            target.localScale = _startScale;
            target.localEulerAngles = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Fade in
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Calculate arc path - target is Vector2.zero (current position)
            var arcPath = CalculateArcPath(target, startPos);
            
            // Animate along arc path
            sequence.Append(target.DOPath(arcPath, _arcDuration, PathType.CatmullRom).SetEase(_arcEase));
            
            // Scale animation - grow to mid scale then shrink to normal
            sequence.Join(target.DOScale(_midScale, _arcDuration * 0.5f).SetEase(Ease.OutBack));
            sequence.Append(target.DOScale(Vector3.one, _arcDuration * 0.5f).SetEase(Ease.InBack));
            
            // Add rotation
            if (_addRotation)
            {
                var rotation = _totalRotation * (_rotationDirection == RotationDirection.Clockwise ? 1f : -1f);
                sequence.Join(target.DOLocalRotate(new Vector3(0, 0, rotation), _arcDuration).SetEase(_arcEase));
            }
            
            // Impact bounce
            if (_addImpactBounce)
            {
                sequence.Append(target.DOScale(Vector3.one * (1f + _bounceIntensity), 0.15f).SetEase(Ease.OutBack));
                sequence.Append(target.DOScale(Vector3.one, 0.15f).SetEase(Ease.OutBack));
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            // Calculate start position for exit
            var startPos = UIUtility.GetOffScreenPosition(target, _startDirection, _startOffset);
            
            // Reverse arc
            var reversePath = CalculateArcPath(target, startPos);
            System.Array.Reverse(reversePath);
            
            sequence.Append(target.DOPath(reversePath, _arcDuration * 0.6f, PathType.CatmullRom).SetEase(Ease.InCubic));
            sequence.Join(target.DOScale(_startScale, _arcDuration * 0.6f).SetEase(Ease.InCubic));
            sequence.Join(canvasGroup.DOFade(0, _arcDuration * 0.4f).SetEase(Ease.InQuad));
            
            return sequence.Play();
        }

        private Vector3[] CalculateArcPath(RectTransform target, Vector2 startPos)
        {
            var points = new Vector3[5];
            points[0] = startPos;
            points[4] = Vector2.zero; // Target is current position
            
            // Calculate control points for arc
            var midPoint = (startPos + Vector2.zero) * 0.5f;
            var arcOffset = _arcDirection == ArcDirection.Up ? Vector2.up : Vector2.down;
            midPoint += arcOffset * _arcHeight;
            
            // Create smooth arc path
            points[1] = Vector2.Lerp(startPos, midPoint, 0.33f);
            points[2] = midPoint;
            points[3] = Vector2.Lerp(midPoint, Vector2.zero, 0.33f);
            
            return points;
        }
    }
}