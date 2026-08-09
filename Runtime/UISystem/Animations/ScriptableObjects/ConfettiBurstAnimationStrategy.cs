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
        
        [SerializeField] [Tooltip("Add screen shake")]
        private bool _addScreenShake = true;
        
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
        
        [SerializeField] [Tooltip("Add color rainbow effect")]
        private bool _addRainbowEffect = false;
        
        [SerializeField] [Tooltip("Trigger burst sound")]
        private bool _triggerBurstSound = true;
        
        [SerializeField] [Tooltip("Trigger celebration sounds")]
        private bool _triggerCelebrationSounds = false;
        
        public enum SettlementBehavior
        {
            Scattered,    // Pieces land randomly
            Clustered,    // Pieces group together
            Organized,    // Pieces arrange neatly
            Chaotic       // Pieces keep moving
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Start invisible and tiny
            target.localScale = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // THE BURST! - Explosive birth
            sequence.AppendCallback(() => {
                canvasGroup.alpha = 1f;
                
                if (_triggerBurstSound) {
                    Debug.Log("🎊 BURST! Sound would play here");
                }
            });
            
            // Initial pop
            sequence.Append(target.DOScale(_popScale, 0.1f).SetEase(Ease.OutBack));
            
            // Flash effect
            sequence.Join(target.DOScale(Vector3.one * _flashIntensity, 0.15f).SetLoops(2, LoopType.Yoyo));
            
            // Screen shake
            if (_addScreenShake) {
                sequence.AppendCallback(() => {
                    Debug.Log("📳 Screen shake would happen here");
                });
            }
            
            // Confetti burst simulation - chaotic movement
            var burstPositions = GenerateBurstPositions(target.anchoredPosition);
            var targetPosition = burstPositions[Random.Range(0, burstPositions.Length)];
            
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
            
            // Rainbow effect
            if (_addRainbowEffect) {
                sequence.AppendCallback(() => StartRainbowEffect(target));
            }
            
            // Continuous celebration
            if (_continuousCelebration) {
                sequence.AppendCallback(() => StartContinuousCelebration(target, canvasGroup));
            }
            
            // Celebration sounds
            if (_triggerCelebrationSounds) {
                sequence.AppendCallback(() => {
                    Debug.Log("🎉 Celebration sounds would play here");
                });
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Stop continuous effects
            StopContinuousCelebration(target);
            StopRainbowEffect(target);
            
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
            
            // Final sound
            if (_triggerBurstSound) {
                sequence.AppendCallback(() => {
                    Debug.Log("👋 Goodbye burst! Sound would play here");
                });
            }
            
            return sequence.Play();
        }

        private Vector2[] GenerateBurstPositions(Vector2 center)
        {
            var positions = new Vector2[_confettiCount];
            
            for (int i = 0; i < _confettiCount; i++) {
                var angle = (float)i / _confettiCount * 2f * Mathf.PI;
                var distance = _burstForce * Random.Range(0.5f, 1f);
                
                // Add chaos
                if (_chaosIntensity > 0) {
                    distance += Random.Range(-_burstForce * _chaosIntensity, _burstForce * _chaosIntensity);
                }
                
                positions[i] = center + new Vector2(
                    Mathf.Cos(angle) * distance,
                    Mathf.Sin(angle) * distance
                );
            }
            
            return positions;
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

        private void StartRainbowEffect(RectTransform target)
        {
            if (!_addRainbowEffect) return;
            
            // This would cycle through colors
            Debug.Log("🌈 Rainbow effect would start here");
        }

        private void StopRainbowEffect(RectTransform target)
        {
            Debug.Log("🌈 Rainbow effect would stop here");
        }

        private void StartContinuousCelebration(RectTransform target, CanvasGroup canvasGroup)
        {
            if (!_continuousCelebration) return;
            
            DOTween.Sequence()
                .AppendCallback(() => {
                    target.DOShakePosition(0.5f, new Vector2(20, 20), 10, 0, true)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
                    target.DOShakeRotation(0.5f, new Vector3(0, 0, 30), 8, 0, true)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
                })
                .AppendInterval(_celebrationInterval)
                .SetLoops(-1)
                // Anonymous sequences have no target, so target.DOKill() can never kill them.
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable)
                .Play();
        }

        private void StopContinuousCelebration(RectTransform target)
        {
            // Don't use DOKill() here as it's already called in PlayHideAnimation
            // Just reset the state
            target.localScale = Vector3.one;
        }
    }
}