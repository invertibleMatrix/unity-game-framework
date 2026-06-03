using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "CardFanAnimation", menuName = "AK/UI/Animations/Card Fan Animation")]
    public class CardFanAnimationStrategy : AnimationStrategy
    {
        [Title("Fan Settings")]
        [SerializeField] [Tooltip("Direction to fan from")]
        private FanDirection _fanDirection = FanDirection.FromCenter;
        
        [SerializeField] [Tooltip("Fan spread angle")]
        private float _fanAngle = 30f;
        
        [SerializeField] [Tooltip("Fan radius")]
        private float _fanRadius = 80f;
        
        [Title("Card Index")]
        [SerializeField] [Tooltip("Index of this card in the fan (0 = first card)")]
        private int _cardIndex = 0;
        
        [SerializeField] [Tooltip("Total number of cards in the fan")]
        private int _totalCards = 5;
        
        [Title("Movement Settings")]
        [SerializeField] [Tooltip("Initial scale")]
        private Vector3 _startScale = Vector3.zero;
        
        [SerializeField] [Tooltip("Add arc to fan movement")]
        private bool _addArc = true;
        
        [ShowIf("_addArc")] [SerializeField] [Tooltip("Arc height")]
        private float _arcHeight = 50f;
        
        [Title("Rotation Settings")]
        [SerializeField] [Tooltip("Add rotation during fan")]
        private bool _addRotation = true;
        
        [ShowIf("_addRotation")] [SerializeField] [Tooltip("Extra rotation per card")]
        private float _extraRotationPerCard = 5f;
        
        [Title("Timing")]
        [SerializeField] [Tooltip("Fan duration")]
        private float _fanDuration = 0.5f;
        
        [SerializeField] [Tooltip("Stagger delay per card")]
        private float _staggerDelay = 0.05f;
        
        [Title("Settle")]
        [SerializeField] [Tooltip("Add settle bounce")]
        private bool _addSettleBounce = true;
        
        [ShowIf("_addSettleBounce")] [SerializeField] [Tooltip("Bounce intensity")]
        private float _bounceIntensity = 0.03f;

        public enum FanDirection
        {
            FromCenter,
            FromLeft,
            FromRight,
            FromTop,
            FromBottom
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Calculate fan rotation (cards stay in place, just rotate)
            var fanRotation = CalculateFanRotation();
            
            // Set initial state - cards stay at their current position
            target.localScale = _startScale;
            target.localEulerAngles = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Add stagger delay based on card index
            var delay = _cardIndex * _staggerDelay;
            sequence.AppendInterval(delay);
            
            // Fade in
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Scale up with arc effect (using scale instead of position)
            if (_addArc)
            {
                // Scale up with overshoot
                sequence.Append(target.DOScale(Vector3.one * 1.1f, _fanDuration * 0.6f).SetEase(Ease.OutBack));
                sequence.Append(target.DOScale(Vector3.one, _fanDuration * 0.4f).SetEase(Ease.InBack));
            }
            else
            {
                sequence.Append(target.DOScale(Vector3.one, _fanDuration).SetEase(Ease.OutBack));
            }
            
            // Add rotation
            if (_addRotation)
            {
                var totalRotation = fanRotation + (_cardIndex * _extraRotationPerCard);
                sequence.Join(target.DOLocalRotate(new Vector3(0, 0, totalRotation), _fanDuration).SetEase(Ease.OutCubic));
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
            
            // Collapse back to center
            sequence.Append(target.DOScale(_startScale, _fanDuration * 0.5f).SetEase(Ease.InCubic));
            sequence.Join(target.DOLocalRotate(Vector3.zero, _fanDuration * 0.5f).SetEase(Ease.InCubic));
            sequence.Join(canvasGroup.DOFade(0, _fanDuration * 0.3f).SetEase(Ease.InQuad));
            
            return sequence.Play();
        }

        private float CalculateFanRotation()
        {
            if (_totalCards <= 1)
            {
                return 0f;
            }
            
            // Calculate normalized position (-1 to 1)
            var normalizedPos = _totalCards > 1 
                ? (float)_cardIndex / (_totalCards - 1) * 2f - 1f 
                : 0f;
            
            float rotation;
            
            switch (_fanDirection)
            {
                case FanDirection.FromCenter:
                    rotation = normalizedPos * _fanAngle;
                    break;
                    
                case FanDirection.FromLeft:
                    rotation = normalizedPos * _fanAngle * 0.5f;
                    break;
                    
                case FanDirection.FromRight:
                    rotation = -normalizedPos * _fanAngle * 0.5f;
                    break;
                    
                case FanDirection.FromTop:
                    rotation = normalizedPos * _fanAngle * 0.5f;
                    break;
                    
                case FanDirection.FromBottom:
                    rotation = -normalizedPos * _fanAngle * 0.5f;
                    break;
                    
                default:
                    rotation = 0f;
                    break;
            }
            
            return rotation;
        }
    }
}