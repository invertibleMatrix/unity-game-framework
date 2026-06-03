using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Systems.Animations
{
    [CreateAssetMenu(fileName = "RewardAnimation", menuName = "AK/UI/Animations/Reward Animation")]
    public class RewardAnimationStrategy : AnimationStrategy
    {
        [Title("Spawn Settings")]
        [SerializeField] [Tooltip("Spawn position relative to screen center. Y is negative for below center.")]
        private Vector2 _spawnOffset = new Vector2(0, -200);
        
        [SerializeField] [Tooltip("Target position where the reward will end up")]
        private Vector2 _targetPosition = Vector2.zero;
        
        [Title("Movement Settings")]
        [SerializeField] [Tooltip("Height of the arc movement")]
        private float _arcHeight = 150f;
        
        [SerializeField] [Tooltip("Horizontal sway amount during movement")]
        private float _swayAmount = 50f;
        
        [Title("Rotation Settings")]
        [SerializeField] [Tooltip("Total rotation during show animation")]
        private Vector3 _showRotation = new Vector3(0, 0, 360);
        
        [SerializeField] [Tooltip("Rotation speed variation")]
        private float _rotationSpeedVariation = 0.3f;
        
        [Title("Scale Settings")]
        [SerializeField] [Tooltip("Initial scale when spawning")]
        private Vector3 _initialScale = new Vector3(0.3f, 0.3f, 0.3f);
        
        [SerializeField] [Tooltip("Scale bounce amount at the end")]
        private Vector3 _bounceScale = new Vector3(1.2f, 1.2f, 1.2f);
        
        [Title("Timing Settings")]
        [SerializeField] [Tooltip("Duration of the initial spawn movement")]
        private float _spawnDuration = 0.8f;
        
        [SerializeField] [Tooltip("Duration of the final bounce settle")]
        private float _settleDuration = 0.4f;
        
        [Title("Effects")]
        [SerializeField] [Tooltip("Add a glow/pulse effect at the end")]
        private bool _addGlowEffect = true;
        
        [SerializeField] [ShowIf("_addGlowEffect")] [Tooltip("Glow pulse intensity")]
        private float _glowIntensity = 1.5f;
        
        [SerializeField] [ShowIf("_addGlowEffect")] [Tooltip("Glow pulse speed")]
        private float _glowSpeed = 2f;

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Set initial state
            target.anchoredPosition = _spawnOffset;
            target.localScale = _initialScale;
            target.localEulerAngles = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Phase 1: Spawn and arc movement with rotation
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Create arc path using multiple position tweens
            var midPoint = new Vector2(
                _spawnOffset.x + _swayAmount,
                _spawnOffset.y + _arcHeight
            );
            
            sequence.Append(target.DOAnchorPos(midPoint, _spawnDuration * 0.6f).SetEase(Ease.OutQuad));
            sequence.Join(target.DOScale(Vector3.one * 0.5f, _spawnDuration * 0.6f).SetEase(Ease.OutBack));
            sequence.Join(target.DOLocalRotate(_showRotation * 0.7f, _spawnDuration * 0.6f).SetEase(Ease.OutQuad));
            
            // Phase 2: Continue to target with rotation
            sequence.Append(target.DOAnchorPos(_targetPosition + new Vector2(-_swayAmount * 0.5f, 50), _spawnDuration * 0.4f).SetEase(Ease.InOutSine));
            sequence.Join(target.DOLocalRotate(_showRotation, _spawnDuration * 0.4f).SetEase(Ease.InOutSine));
            sequence.Join(target.DOScale(_bounceScale, _spawnDuration * 0.4f).SetEase(Ease.OutBack));
            
            // Phase 3: Settle into final position
            sequence.Append(target.DOAnchorPos(_targetPosition, _settleDuration).SetEase(Ease.OutBack));
            sequence.Join(target.DOScale(Vector3.one, _settleDuration).SetEase(Ease.OutElastic));
            sequence.Join(target.DOLocalRotate(Vector3.zero, _settleDuration).SetEase(Ease.OutBack));
            
            // Phase 4: Optional glow effect
            if (_addGlowEffect)
            {
                sequence.AppendCallback(() =>
                {
                    target.DOScale(Vector3.one * _glowIntensity, 0.2f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.InOutSine);
                });
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Phase 1: Quick spin and shrink
            sequence.Append(target.DOLocalRotate(new Vector3(0, 0, -720), ExitDuration * 0.5f).SetEase(Ease.InBack));
            sequence.Join(target.DOScale(Vector3.zero, ExitDuration * 0.5f).SetEase(Ease.InBack));
            sequence.Join(canvasGroup.DOFade(0, ExitDuration * 0.5f).SetEase(Ease.InQuad));
            
            // Phase 2: Fall off screen
            var fallPosition = new Vector2(target.anchoredPosition.x, _spawnOffset.y - 300);
            sequence.Append(target.DOAnchorPos(fallPosition, ExitDuration * 0.5f).SetEase(Ease.InCubic));
            
            return sequence.Play();
        }
    }
}