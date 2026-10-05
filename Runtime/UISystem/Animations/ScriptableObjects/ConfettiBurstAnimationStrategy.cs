using AK.Core.Extensions;
using AK.Kernel.Timing;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems.Animations
{
    [CreateAssetMenu(fileName = "ConfettiBurstAnimation", menuName = "AK/UI/Animations/Confetti Burst Animation")]
    public class ConfettiBurstAnimationStrategy : AnimationStrategy
    {
        [SerializeField] [Tooltip("Number of confetti pieces to simulate")]
        private int _confettiCount = 20;
        
        [SerializeField] [Tooltip("Burst explosion force")]
        private float _burstForce = 300f;
        
        [SerializeField] [Tooltip("Burst duration")]
        private float _burstDuration = 0.8f;
        
        [SerializeField] [Tooltip("Chaos intensity (0-1)")]
        private float _chaosIntensity = 0.8f;
        
        [SerializeField] [Tooltip("Random rotation speed")]
        private float _rotationSpeed = 720f;
        
        [SerializeField] [Tooltip("Add gravity effect")]
        private bool _addGravity = true;
        
        [SerializeField] [Tooltip("Gravity strength")]
        private float _gravityStrength = 200f;
        
        [SerializeField] [Tooltip("Initial pop scale")]
        private Vector3 _popScale = new Vector3(1.5f, 1.5f, 1.5f);
        
        [SerializeField] [Tooltip("Birth flash intensity")]
        private float _flashIntensity = 1.8f;
        
        [SerializeField] [Tooltip("Settlement behavior")]
        private SettlementBehavior _settlementBehavior = SettlementBehavior.Scattered;
        
        [SerializeField] [Tooltip("Final bounces")]
        private int _finalBounces = 3;
        
        [SerializeField] [Tooltip("Bounce decay")]
        private float _bounceDecay = 0.5f;
        
        [SerializeField] [Tooltip("Continuous celebration")]
        private bool _continuousCelebration = false;
        
        [SerializeField] [Tooltip("Celebration interval")]
        private float _celebrationInterval = 2f;
        
        public enum SettlementBehavior
        {
            Scattered,    // Pieces land randomly
            Clustered,    // Pieces group together
            Organized,    // Pieces arrange neatly
            Chaotic       // Pieces keep moving
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time, Vector2 entryPos = default)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Start invisible and tiny
            target.localScale = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // THE BURST! - Explosive birth
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Initial pop
            sequence.Append(target.DOScale(_popScale, 0.1f).SetEase(Ease.OutBack));
            
            // Flash effect
            sequence.Join(target.DOScale(Vector3.one * _flashIntensity, 0.15f).SetLoops(2, LoopType.Yoyo));
            
            // Confetti burst simulation - chaotic movement
            var targetPosition = RandomBurstPosition(target.anchoredPosition);
            
            // Move to random burst position with chaos
            sequence.Append(target.DOAnchorPos(targetPosition, _burstDuration * 0.6f).SetEase(Ease.OutQuad));
            
            // Add chaotic rotation
            var randomRotation = Random.Range(-_rotationSpeed, _rotationSpeed);
            sequence.Join(target.DOLocalRotate(new Vector3(0, 0, randomRotation), _burstDuration * 0.6f).SetEase(Ease.InOutSine));
            
            // Add scale chaos
            var chaosScale = Vector3.one * Random.Range(0.8f, 1.3f);
            sequence.Join(target.DOScale(chaosScale, _burstDuration * 0.4f).SetEase(Ease.InOutSine));
            
            // Gravity effect
            if (_addGravity) {
                var gravityPos = targetPosition + Vector2.down * _gravityStrength;
                sequence.Append(target.DOAnchorPos(gravityPos, _burstDuration * 0.4f).SetEase(Ease.InQuad));
            }
            
            // Settlement based on behavior
            var finalPosition = GetSettlementPosition(target.anchoredPosition, targetPosition);
            sequence.Append(target.DOAnchorPos(finalPosition, _burstDuration * 0.3f).SetEase(GetSettlementEase()));
            
            // Final bounces
            var currentBounceHeight = 30f;
            for (int i = 0; i < _finalBounces; i++) {
                var bounceDuration = 0.2f / (i + 1);
                
                sequence.Append(target.DOAnchorPos(finalPosition + Vector2.up * currentBounceHeight, bounceDuration * 0.5f).SetEase(Ease.OutQuad));
                sequence.Append(target.DOAnchorPos(finalPosition, bounceDuration * 0.5f).SetEase(Ease.InBounce));
                
                currentBounceHeight *= _bounceDecay;
            }
            
            // Final settle
            sequence.Append(target.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack));
            sequence.Join(target.DOLocalRotate(Vector3.zero, 0.3f).SetEase(Ease.OutBack));
            
            // Continuous celebration
            if (_continuousCelebration) {
                sequence.AppendCallback(() => StartContinuousCelebration(target, canvasGroup, time));
            }
            
            return sequence.SetTimeDomain(time).Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // The celebration loop targets the content, so the DOKill above stopped it.
            target.localScale = Vector3.one;
            
            // Final celebration burst before leaving
            sequence.Append(target.DOScale(_popScale * 1.2f, 0.2f).SetEase(Ease.OutBack));
            
            // One last chaotic movement
            var finalBurstPos = target.anchoredPosition + new Vector2(
                Random.Range(-100f, 100f),
                Random.Range(-50f, 100f)
            );
            sequence.Append(target.DOAnchorPos(finalBurstPos, 0.3f).SetEase(Ease.OutQuad));
            sequence.Join(target.DOLocalRotate(new Vector3(0, 0, _rotationSpeed * 2f), 0.3f).SetEase(Ease.InOutSine));
            
            // Quick fade and disappear
            sequence.Append(target.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
            sequence.Join(canvasGroup.DOFade(0, 0.3f).SetEase(Ease.InQuad));

            return sequence.SetTimeDomain(time).Play();
        }

        /// <summary>Where one of <see cref="_confettiCount"/> pieces bursting evenly around <paramref name="center"/> lands, picked at random.</summary>
        private Vector2 RandomBurstPosition(Vector2 center)
        {
            int count = Mathf.Max(1, _confettiCount);
            var angle = (float)Random.Range(0, count) / count * 2f * Mathf.PI;
            var distance = _burstForce * Random.Range(0.5f, 1f);

            // Add chaos
            if (_chaosIntensity > 0) {
                distance += Random.Range(-_burstForce * _chaosIntensity, _burstForce * _chaosIntensity);
            }

            return center + new Vector2(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance
            );
        }

        private Vector2 GetSettlementPosition(Vector2 original, Vector2 burstPos)
        {
            return _settlementBehavior switch
            {
                SettlementBehavior.Scattered => burstPos + new Vector2(
                    Random.Range(-50f, 50f),
                    Random.Range(-30f, 30f)
                ),
                SettlementBehavior.Clustered => original + new Vector2(
                    Random.Range(-30f, 30f),
                    Random.Range(-20f, 20f)
                ),
                SettlementBehavior.Organized => original,
                SettlementBehavior.Chaotic => burstPos + new Vector2(
                    Random.Range(-100f, 100f),
                    Random.Range(-100f, 100f)
                ),
                _ => original
            };
        }

        private Ease GetSettlementEase()
        {
            return _settlementBehavior switch
            {
                SettlementBehavior.Scattered => Ease.OutBounce,
                SettlementBehavior.Clustered => Ease.OutBack,
                SettlementBehavior.Organized => Ease.OutQuad,
                SettlementBehavior.Chaotic => Ease.InOutSine,
                _ => Ease.OutBounce
            };
        }

        private void StartContinuousCelebration(RectTransform target, CanvasGroup canvasGroup, TimeDomain time)
        {
            if (!_continuousCelebration) return;
            
            DOTween.Sequence()
                .AppendCallback(() => {
                    target.DOShakePosition(0.5f, new Vector2(20, 20), 10, 0, true)
                        .SetTimeDomain(time)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
                    target.DOShakeRotation(0.5f, new Vector3(0, 0, 30), 8, 0, true)
                        .SetTimeDomain(time)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
                })
                .AppendInterval(_celebrationInterval)
                .SetLoops(-1)
                // Targets the content, so the view's next entrance or exit stops it.
                .SetTarget(target)
                .SetTimeDomain(time)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable)
                .Play();
        }
    }
}