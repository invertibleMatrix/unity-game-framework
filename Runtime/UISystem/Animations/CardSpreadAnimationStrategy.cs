using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "CardSpreadAnimation", menuName = "Gameplay/UI/Animations/Card Spread Animation")]
    public class CardSpreadAnimationStrategy : AnimationStrategy
    {
        [Title("Spread Settings")]
        [SerializeField] [Tooltip("Spread direction")]
        private SpreadDirection _spreadDirection = SpreadDirection.Horizontal;
        
        [SerializeField] [Tooltip("Spread distance")]
        private float _spreadDistance = 80f;
        
        [Title("Card Index")]
        [SerializeField] [Tooltip("Index of this card in the spread (0 = first card)")]
        private int _cardIndex = 0;
        
        [SerializeField] [Tooltip("Total number of cards in the spread")]
        private int _totalCards = 5;
        
        [Title("Movement Settings")]
        [SerializeField] [Tooltip("Initial scale")]
        private Vector3 _startScale = Vector3.zero;
        
        [SerializeField] [Tooltip("Add curve to spread")]
        private bool _addCurve = true;
        
        [ShowIf("_addCurve")] [SerializeField] [Tooltip("Curve intensity")]
        private float _curveIntensity = 50f;
        
        [Title("Rotation Settings")]
        [SerializeField] [Tooltip("Add rotation during spread")]
        private bool _addRotation = true;
        
        [ShowIf("_addRotation")] [SerializeField] [Tooltip("Max rotation")]
        private float _maxRotation = 10f;
        
        [ShowIf("_addRotation")] [SerializeField] [Tooltip("Rotation pattern")]
        private RotationPattern _rotationPattern = RotationPattern.Alternating;
        
        [Title("Timing")]
        [SerializeField] [Tooltip("Spread duration")]
        private float _spreadDuration = 0.5f;
        
        [SerializeField] [Tooltip("Stagger delay per card")]
        private float _staggerDelay = 0.06f;
        
        [Title("Settle")]
        [SerializeField] [Tooltip("Add settle bounce")]
        private bool _addSettleBounce = true;
        
        [ShowIf("_addSettleBounce")] [SerializeField] [Tooltip("Bounce intensity")]
        private float _bounceIntensity = 0.03f;

        public enum SpreadDirection
        {
            Horizontal,
            Vertical,
            Diagonal
        }
        
        public enum RotationPattern
        {
            Alternating,
            Progressive,
            Random
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Calculate spread rotation (cards stay in place, just rotate)
            var spreadRotation = CalculateSpreadRotation();
            
            // Set initial state - cards stay at their current position
            target.localScale = _startScale;
            target.localEulerAngles = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Add stagger delay based on card index
            var delay = _cardIndex * _staggerDelay;
            sequence.AppendInterval(delay);
            
            // Fade in
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Scale up with curve effect (using scale instead of position)
            if (_addCurve)
            {
                // Scale up with overshoot
                sequence.Append(target.DOScale(Vector3.one * 1.1f, _spreadDuration * 0.6f).SetEase(Ease.OutBack));
                sequence.Append(target.DOScale(Vector3.one, _spreadDuration * 0.4f).SetEase(Ease.InBack));
            }
            else
            {
                sequence.Append(target.DOScale(Vector3.one, _spreadDuration).SetEase(Ease.OutBack));
            }
            
            // Add rotation
            if (_addRotation)
            {
                sequence.Join(target.DOLocalRotate(new Vector3(0, 0, spreadRotation), _spreadDuration).SetEase(Ease.OutCubic));
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
            
            // Reverse stagger - outer cards leave first
            var reverseDelay = Mathf.Abs(_cardIndex - (_totalCards - 1) / 2f) * _staggerDelay * 0.5f;
            sequence.AppendInterval(reverseDelay);
            
            // Collapse back to center
            sequence.Append(target.DOScale(_startScale, _spreadDuration * 0.5f).SetEase(Ease.InCubic));
            sequence.Join(target.DOLocalRotate(Vector3.zero, _spreadDuration * 0.5f).SetEase(Ease.InCubic));
            sequence.Join(canvasGroup.DOFade(0, _spreadDuration * 0.3f).SetEase(Ease.InQuad));
            
            return sequence.Play();
        }

        private float CalculateSpreadRotation()
        {
            if (_totalCards <= 1)
            {
                return 0f;
            }
            
            // Calculate normalized position (-1 to 1)
            var normalizedPos = _totalCards > 1 
                ? (float)_cardIndex / (_totalCards - 1) * 2f - 1f 
                : 0f;
            
            return CalculateRotation(normalizedPos);
        }

        private float CalculateRotation(float normalizedPos)
        {
            switch (_rotationPattern)
            {
                case RotationPattern.Alternating:
                    return (_cardIndex % 2 == 0) ? _maxRotation : -_maxRotation;
                    
                case RotationPattern.Progressive:
                    return normalizedPos * _maxRotation;
                    
                case RotationPattern.Random:
                    return Random.Range(-_maxRotation, _maxRotation);
                    
                default:
                    return 0f;
            }
        }
    }
}