using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Systems.Animations
{
    [CreateAssetMenu(fileName = "DropBounceAnimation", menuName = "AK/UI/Animations/Drop Bounce Animation")]
    public class DropBounceAnimationStrategy : AnimationStrategy
    {
        [Title("Drop Settings")]
        [SerializeField] [Tooltip("Drop height above final position")]
        private float _dropHeight = 300f;
        
        [SerializeField] [Tooltip("Drop duration")]
        private float _dropDuration = 0.8f;
        
        [SerializeField] [Tooltip("Gravity multiplier")]
        private float _gravityMultiplier = 1.5f;
        
        [Title("Bounce Settings")]
        [SerializeField] [Tooltip("Number of bounces")]
        private int _bounceCount = 3;
        
        [SerializeField] [Tooltip("Bounce height multiplier")]
        private float _bounceHeightMultiplier = 0.6f;
        
        [SerializeField] [Tooltip("Bounce duration multiplier")]
        private float _bounceDurationMultiplier = 0.7f;
        
        [Title("Squash and Stretch")]
        [SerializeField] [Tooltip("Enable squash and stretch")]
        private bool _enableSquashStretch = true;
        
        [SerializeField] [ShowIf("_enableSquashStretch")] [Tooltip("Squash amount on impact")]
        private Vector3 _squashAmount = new Vector3(1.3f, 0.7f, 1f);
        
        [SerializeField] [ShowIf("_enableSquashStretch")] [Tooltip("Stretch amount in air")]
        private Vector3 _stretchAmount = new Vector3(0.9f, 1.1f, 1f);
        
        [Title("Rotation")]
        [SerializeField] [Tooltip("Add rotation during fall")]
        private bool _addRotation = false;
        
        [SerializeField] [ShowIf("_addRotation")] [Tooltip("Rotation amount")]
        private Vector3 _rotationAmount = new Vector3(0, 0, 180f);
        
        [SerializeField] [ShowIf("_addRotation")] [Tooltip("Random rotation direction")]
        private bool _randomRotationDirection = true;
        
        [Title("Effects")]
        [SerializeField] [Tooltip("Add impact effect")]
        private bool _addImpactEffect = true;
        
        [SerializeField] [ShowIf("_addImpactEffect")] [Tooltip("Impact scale")]
        private Vector3 _impactScale = new Vector3(1.5f, 1.5f, 1.5f);
        
        [SerializeField] [Tooltip("Add dust particles (conceptual)")]
        private bool _addDustEffect = false;
        
        [Title("Exit")]
        [SerializeField] [Tooltip("Drop through floor on exit")]
        private bool _dropThroughFloor = true;
        
        [SerializeField] [ShowIf("_dropThroughFloor")] [Tooltip("Fall distance")]
        private float _fallDistance = 500f;
        
        [SerializeField] [ShowIf("_dropThroughFloor")] [Tooltip("Fall duration")]
        private float _fallDuration = 0.6f;

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Set initial state
            var startPosition = target.anchoredPosition + Vector2.up * _dropHeight;
            target.anchoredPosition = startPosition;
            target.localScale = _enableSquashStretch ? _stretchAmount : Vector3.one;
            canvasGroup.alpha = 0f;
            
            // Set initial rotation
            if (_addRotation)
            {
                var rotation = _rotationAmount;
                if (_randomRotationDirection)
                {
                    rotation *= Random.value > 0.5f ? 1f : -1f;
                }
                target.localEulerAngles = rotation;
            }
            
            // Fade in
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Drop animation with gravity
            var dropEase = GetGravityEase();
            sequence.Append(target.DOAnchorPos(target.anchoredPosition - Vector2.up * _dropHeight, _dropDuration).SetEase(dropEase));
            
            // Stretch during fall
            if (_enableSquashStretch)
            {
                sequence.Join(target.DOScale(_stretchAmount, _dropDuration * 0.8f).SetEase(Ease.InQuad));
            }
            
            // Rotation during fall
            if (_addRotation)
            {
                var targetRotation = _randomRotationDirection ? 
                    target.localEulerAngles + _rotationAmount * 2f : 
                    Vector3.zero;
                sequence.Join(target.DOLocalRotate(targetRotation, _dropDuration).SetEase(Ease.InQuad));
            }
            
            // Impact squash
            if (_enableSquashStretch)
            {
                sequence.Append(target.DOScale(_squashAmount, 0.1f).SetEase(Ease.OutBounce));
                
                // Impact effect
                if (_addImpactEffect)
                {
                    sequence.AppendCallback(() =>
                    {
                        // Create impact effect (would need to instantiate a prefab)
                        target.DOScale(_impactScale, 0.1f).SetEase(Ease.OutBack);
                    });
                    sequence.Append(target.DOScale(_squashAmount, 0.1f).SetEase(Ease.InBack));
                }
                
                // Recover from squash
                sequence.Append(target.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack));
            }
            
            // Bounce sequence
            var currentBounceHeight = _dropHeight * _bounceHeightMultiplier;
            var currentBounceDuration = _dropDuration * _bounceDurationMultiplier;
            
            for (int i = 0; i < _bounceCount; i++)
            {
                // Bounce up
                sequence.Append(target.DOAnchorPos(target.anchoredPosition + Vector2.up * currentBounceHeight, currentBounceDuration * 0.5f).SetEase(Ease.OutQuad));
                
                // Stretch at peak
                if (_enableSquashStretch)
                {
                    sequence.Join(target.DOScale(_stretchAmount, currentBounceDuration * 0.3f).SetEase(Ease.OutQuad));
                }
                
                // Bounce down
                sequence.Append(target.DOAnchorPos(target.anchoredPosition, currentBounceDuration * 0.5f).SetEase(Ease.InBounce));
                
                // Squash on impact
                if (_enableSquashStretch)
                {
                    sequence.Join(target.DOScale(_squashAmount, currentBounceDuration * 0.2f).SetEase(Ease.OutBounce));
                    sequence.Append(target.DOScale(Vector3.one, currentBounceDuration * 0.3f).SetEase(Ease.OutBack));
                }
                
                currentBounceHeight *= _bounceHeightMultiplier;
                currentBounceDuration *= _bounceDurationMultiplier;
            }
            
            // Final settle
            sequence.Append(target.DOScale(Vector3.one * 1.05f, 0.1f).SetEase(Ease.OutBack));
            sequence.Append(target.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutBack));
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            if (_dropThroughFloor)
            {
                // Quick bounce before falling
                sequence.Append(target.DOAnchorPos(target.anchoredPosition + Vector2.up * 50f, 0.2f).SetEase(Ease.OutQuad));
                sequence.Append(target.DOAnchorPos(target.anchoredPosition, 0.2f).SetEase(Ease.InBounce));
                
                // Squash on final impact
                if (_enableSquashStretch)
                {
                    sequence.Join(target.DOScale(_squashAmount, 0.1f).SetEase(Ease.OutBounce));
                }
                
                // Fall through floor
                sequence.Append(target.DOAnchorPos(target.anchoredPosition - Vector2.up * _fallDistance, _fallDuration).SetEase(Ease.InQuad));
                
                // Shrink while falling
                sequence.Join(target.DOScale(Vector3.zero, _fallDuration * 0.8f).SetEase(Ease.InBack));
                sequence.Join(canvasGroup.DOFade(0, _fallDuration * 0.5f).SetEase(Ease.InQuad));
            }
            else
            {
                // Regular bounce and disappear
                sequence.Append(target.DOAnchorPos(target.anchoredPosition + Vector2.up * _dropHeight * 0.5f, 0.3f).SetEase(Ease.OutQuad));
                sequence.Append(target.DOAnchorPos(target.anchoredPosition, 0.3f).SetEase(Ease.InBounce));
                
                if (_enableSquashStretch)
                {
                    sequence.Join(target.DOScale(_squashAmount, 0.1f).SetEase(Ease.OutBounce));
                }
                
                sequence.Append(target.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
                sequence.Join(canvasGroup.DOFade(0, 0.3f).SetEase(Ease.InQuad));
            }
            
            return sequence.Play();
        }

        private Ease GetGravityEase()
        {
            // Simulate gravity acceleration
            return _gravityMultiplier switch
            {
                <= 0.5f => Ease.InSine,
                <= 1.0f => Ease.InQuad,
                <= 1.5f => Ease.InCubic,
                <= 2.0f => Ease.InQuart,
                _ => Ease.InQuint
            };
        }
    }
}