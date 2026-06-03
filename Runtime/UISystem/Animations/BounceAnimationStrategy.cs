using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Systems.Animations
{
    [CreateAssetMenu(fileName = "BounceAnimation", menuName = "AK/UI/Animations/Bounce Animation")]
    public class BounceAnimationStrategy : AnimationStrategy
    {
        [Title("Bounce Settings")]
        [SerializeField] [Tooltip("Number of bounces")]
        private int _bounceCount = 3;
        
        [SerializeField] [Tooltip("Height of each bounce relative to the previous")]
        private float _bounceHeightMultiplier = 0.6f;
        
        [SerializeField] [Tooltip("Initial bounce height")]
        private float _initialBounceHeight = 100f;
        
        [Title("Scale Settings")]
        [SerializeField] [Tooltip("Scale squash amount on landing")]
        private Vector3 _squashScale = new Vector3(1.2f, 0.8f, 1f);
        
        [SerializeField] [Tooltip("Scale stretch amount at bounce peak")]
        private Vector3 _stretchScale = new Vector3(0.9f, 1.1f, 1f);
        
        [Title("Rotation Settings")]
        [SerializeField] [Tooltip("Add rotation during bounce")]
        private bool _addRotation = true;
        
        [SerializeField] [ShowIf("_addRotation")] [Tooltip("Rotation amount per bounce")]
        private Vector3 _rotationPerBounce = new Vector3(0, 0, 15f);
        
        [Title("Direction Settings")]
        [SerializeField] [Tooltip("Bounce direction")]
        private BounceDirection _direction = BounceDirection.Vertical;
        
        [SerializeField] [Tooltip("Random horizontal movement during bounce")]
        private bool _addRandomHorizontal = false;
        
        [SerializeField] [ShowIf("_addRandomHorizontal")] [Tooltip("Max horizontal movement")]
        private float _maxHorizontalMovement = 50f;
        
        public enum BounceDirection
        {
            Vertical,
            Horizontal,
            Both
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Set initial state
            target.localScale = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Initial pop in
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            sequence.Append(target.DOScale(Vector3.one * 1.2f, EntryDuration * 0.2f).SetEase(Ease.OutBack));
            
            // Start bounce sequence
            var currentHeight = _initialBounceHeight;
            var currentPosition = Vector2.zero;
            
            for (int i = 0; i < _bounceCount; i++)
            {
                var bounceDuration = EntryDuration * 0.3f / (i + 1); // Each bounce is faster
                
                // Move up
                var bounceTarget = currentPosition;
                if (_direction == BounceDirection.Vertical || _direction == BounceDirection.Both)
                    bounceTarget.y += currentHeight;
                if (_direction == BounceDirection.Horizontal || _direction == BounceDirection.Both)
                    bounceTarget.x += currentHeight * 0.5f;
                
                if (_addRandomHorizontal)
                {
                    bounceTarget.x += Random.Range(-_maxHorizontalMovement, _maxHorizontalMovement);
                }
                
                sequence.Append(target.DOAnchorPos(bounceTarget, bounceDuration).SetEase(Ease.OutQuad));
                
                // Stretch at peak
                sequence.Join(target.DOScale(_stretchScale, bounceDuration * 0.5f).SetEase(Ease.OutQuad));
                sequence.Append(target.DOScale(Vector3.one, bounceDuration * 0.5f).SetEase(Ease.InQuad));
                
                // Add rotation
                if (_addRotation)
                {
                    sequence.Join(target.DOLocalRotate(_rotationPerBounce * (i + 1), bounceDuration).SetEase(Ease.InOutSine));
                }
                
                // Move down with squash
                sequence.Append(target.DOAnchorPos(currentPosition, bounceDuration).SetEase(Ease.InBounce));
                sequence.Join(target.DOScale(_squashScale, bounceDuration * 0.3f).SetEase(Ease.OutQuad));
                sequence.Append(target.DOScale(Vector3.one, bounceDuration * 0.7f).SetEase(Ease.OutBack));
                
                currentHeight *= _bounceHeightMultiplier;
            }
            
            // Final settle
            sequence.Append(target.DOScale(Vector3.one * 1.05f, 0.1f).SetEase(Ease.OutBack));
            sequence.Append(target.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutBack));
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            // Final big bounce before disappearing
            sequence.Append(target.DOAnchorPos(target.anchoredPosition + Vector2.up * _initialBounceHeight, ExitDuration * 0.3f).SetEase(Ease.OutQuad));
            sequence.Join(target.DOScale(_stretchScale, ExitDuration * 0.3f).SetEase(Ease.OutQuad));
            
            // Squash and disappear
            sequence.Append(target.DOAnchorPos(target.anchoredPosition, ExitDuration * 0.3f).SetEase(Ease.InBounce));
            sequence.Join(target.DOScale(_squashScale, ExitDuration * 0.3f).SetEase(Ease.OutQuad));
            sequence.Join(canvasGroup.DOFade(0, ExitDuration * 0.3f).SetEase(Ease.InQuad));
            
            // Final shrink
            sequence.Append(target.DOScale(Vector3.zero, ExitDuration * 0.4f).SetEase(Ease.InBack));
            
            return sequence.Play();
        }
    }
}