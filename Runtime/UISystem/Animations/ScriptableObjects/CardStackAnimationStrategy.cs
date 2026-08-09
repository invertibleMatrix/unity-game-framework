using DG.Tweening;
using UnityEngine;

namespace AK.Systems.Animations
{
    [CreateAssetMenu(fileName = "CardStackAnimation", menuName = "AK/UI/Animations/Card Stack Animation")]
    public class CardStackAnimationStrategy : AnimationStrategy
    {
        [SerializeField] [Tooltip("Index of this card in the stack (0 = bottom card)")]
        private int _stackIndex = 0;
        
        [SerializeField] [Tooltip("Total number of cards in the stack")]
        private int _totalCards = 5;
        
        [SerializeField] [Tooltip("Stack direction")]
        private StackDirection _stackDirection = StackDirection.Up;
        
        [SerializeField] [Tooltip("Spawn offset")]
        private Vector2 _spawnOffset = new Vector2(100, 100);
        
        [SerializeField] [Tooltip("Spacing between cards")]
        private float _cardSpacing = 20f;
        
        [SerializeField] [Tooltip("Randomize spacing slightly")]
        private bool _randomizeSpacing = true;
        
        [SerializeField] [Tooltip("Random spacing amount")]
        private float _randomSpacingAmount = 5f;
        
        [SerializeField] [Tooltip("Add rotation to stack")]
        private bool _addRotation = true;
        
        [SerializeField] [Tooltip("Max rotation per card")]
        private float _maxRotation = 5f;
        
        [SerializeField] [Tooltip("Randomize rotation")]
        private bool _randomizeRotation = true;
        
        [SerializeField] [Tooltip("Initial scale")]
        private Vector3 _startScale = Vector3.zero;
        
        [SerializeField] [Tooltip("Stack duration")]
        private float _stackDuration = 0.4f;
        
        [SerializeField] [Tooltip("Stagger delay per card")]
        private float _staggerDelay = 0.08f;
        
        [SerializeField] [Tooltip("Add settle bounce")]
        private bool _addSettleBounce = true;
        
        [SerializeField] [Tooltip("Bounce intensity")]
        private float _bounceIntensity = 0.02f;

        public enum StackDirection
        {
            Up,
            Down,
            Left,
            Right
        }
        
        private UIUtility.SlideDirection GetStackDirection()
        {
            return _stackDirection switch
            {
                StackDirection.Up => UIUtility.SlideDirection.FromTop,
                StackDirection.Down => UIUtility.SlideDirection.FromBottom,
                StackDirection.Left => UIUtility.SlideDirection.FromLeft,
                StackDirection.Right => UIUtility.SlideDirection.FromRight,
                _ => UIUtility.SlideDirection.FromTop
            };
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Calculate stack position and rotation (relative to Vector2.zero)
            var (stackPos, stackRotation) = CalculateStackPosition();
            
            // Set initial state - spawn from off-screen position
            var startPos = UIUtility.GetOffScreenPosition(target, GetStackDirection(), _spawnOffset);
            target.anchoredPosition = startPos;
            target.localScale = _startScale;
            target.localEulerAngles = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Add stagger delay based on stack index
            var delay = _stackIndex * _staggerDelay;
            sequence.AppendInterval(delay);
            
            // Fade in
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Move to stack position (offset from center)
            sequence.Append(target.DOAnchorPos(stackPos, _stackDuration).SetEase(Ease.OutCubic));
            
            // Scale up
            sequence.Join(target.DOScale(Vector3.one, _stackDuration).SetEase(Ease.OutBack));
            
            // Add rotation
            if (_addRotation)
            {
                sequence.Join(target.DOLocalRotate(new Vector3(0, 0, stackRotation), _stackDuration).SetEase(Ease.OutCubic));
            }
            
            // Settle bounce
            if (_addSettleBounce)
            {
                sequence.Append(target.DOScale(Vector3.one * (1f + _bounceIntensity), 0.1f).SetEase(Ease.OutBack));
                sequence.Append(target.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutBack));
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            // Reverse stagger - top cards leave first
            var reverseDelay = (_totalCards - 1 - _stackIndex) * _staggerDelay * 0.5f;
            sequence.AppendInterval(reverseDelay);
            
            // Fly out to off-screen position
            var exitPos = UIUtility.GetOffScreenPosition(target, GetStackDirection(), _spawnOffset * 1.2f);
            sequence.Append(target.DOAnchorPos(exitPos, _stackDuration * 0.5f).SetEase(Ease.InCubic));
            sequence.Join(target.DOScale(_startScale, _stackDuration * 0.5f).SetEase(Ease.InCubic));
            sequence.Join(canvasGroup.DOFade(0, _stackDuration * 0.3f).SetEase(Ease.InQuad));
            
            return sequence.Play();
        }

        private (Vector2 position, float rotation) CalculateStackPosition()
        {
            Vector2 position = Vector2.zero;
            float rotation = 0f;
            
            // Calculate spacing with optional randomization
            var spacing = _cardSpacing;
            if (_randomizeSpacing)
            {
                spacing += Random.Range(-_randomSpacingAmount, _randomSpacingAmount);
            }
            
            // Calculate position based on stack direction
            switch (_stackDirection)
            {
                case StackDirection.Up:
                    position = new Vector2(0, _stackIndex * spacing);
                    break;
                case StackDirection.Down:
                    position = new Vector2(0, -_stackIndex * spacing);
                    break;
                case StackDirection.Left:
                    position = new Vector2(-_stackIndex * spacing, 0);
                    break;
                case StackDirection.Right:
                    position = new Vector2(_stackIndex * spacing, 0);
                    break;
            }
            
            // Calculate rotation
            if (_addRotation)
            {
                if (_randomizeRotation)
                {
                    rotation = Random.Range(-_maxRotation, _maxRotation);
                }
                else
                {
                    // Alternating rotation pattern
                    rotation = (_stackIndex % 2 == 0) ? _maxRotation : -_maxRotation;
                }
            }
            
            return (position, rotation);
        }
    }
}