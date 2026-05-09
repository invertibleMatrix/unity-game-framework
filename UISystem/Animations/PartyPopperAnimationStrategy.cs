using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "PartyPopperAnimation", menuName = "Gameplay/UI/Animations/Party Popper Animation")]
    public class PartyPopperAnimationStrategy : AnimationStrategy
    {
        [Title("Explosion Settings")]
        [SerializeField] [Tooltip("Initial explosion scale")]
        private Vector3 _explosionScale = new Vector3(2.5f, 2.5f, 2.5f);
        
        [SerializeField] [Tooltip("Explosion duration")]
        private float _explosionDuration = 0.15f;
        
        [SerializeField] [Tooltip("Number of particles to simulate")]
        private int _particleCount = 12;
        
        [SerializeField] [Tooltip("Particle burst radius")]
        private float _burstRadius = 200f;
        
        [Title("Birth Animation")]
        [SerializeField] [Tooltip("Birth scale (tiny start)")]
        private Vector3 _birthScale = new Vector3(0.01f, 0.01f, 0.01f);
        
        [SerializeField] [Tooltip("Wobble intensity during birth")]
        private float _wobbleIntensity = 15f;
        
        [SerializeField] [Tooltip("Growth curve feel")]
        private GrowthFeel _growthFeel = GrowthFeel.Organic;
        
        [Title("Settlement Phase")]
        [SerializeField] [Tooltip("Settlement bounces")]
        private int _settlementBounces = 4;
        
        [SerializeField] [Tooltip("Bounce height decay")]
        private float _bounceDecay = 0.6f;
        
        [SerializeField] [Tooltip("Final wobble amount")]
        private float _finalWobble = 3f;
        
        [Title("Personality")]
        [SerializeField] [Tooltip("Add personality wobble")]
        private bool _addPersonalityWobble = true;
        
        [SerializeField] [ShowIf("_addPersonalityWobble")] [Tooltip("Wobble speed")]
        private float _wobbleSpeed = 8f;
        
        [SerializeField] [Tooltip("Add breathing effect")]
        private bool _addBreathing = true;
        
        [SerializeField] [ShowIf("_addBreathing")] [Tooltip("Breathing intensity")]
        private float _breathingIntensity = 0.05f;
        
        [Title("Visual Effects")]
        [SerializeField] [Tooltip("Add flash effect")]
        private bool _addFlash = true;
        
        [SerializeField] [ShowIf("_addFlash")] [Tooltip("Flash intensity")]
        private float _flashIntensity = 2f;
        
        [SerializeField] [Tooltip("Add color celebration")]
        private bool _addColorCelebration = false;
        
        [Title("Sound Integration")]
        [SerializeField] [Tooltip("Pop sound timing")]
        private bool _triggerPopSound = true;
        
        [SerializeField] [Tooltip("Celebration sound timing")]
        private bool _triggerCelebrationSound = false;
        
        public enum GrowthFeel
        {
            Organic,     // Natural, uneven growth
            Bouncy,      // Playful, springy growth
            Magical,     // Smooth, enchanted growth
            Explosive    // Quick, powerful growth
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Start from nothing - like before birth
            target.localScale = _birthScale;
            canvasGroup.alpha = 0f;
            
            // THE POP! - Explosive birth
            sequence.AppendCallback(() => {
                canvasGroup.alpha = 1f;
                
                // Trigger pop sound
                if (_triggerPopSound) {
                    Debug.Log("🎊 POP! Sound would play here");
                }
            });
            
            // Explosive appearance
            sequence.Append(target.DOScale(_explosionScale, _explosionDuration).SetEase(Ease.OutBack));
            
            // Flash effect
            if (_addFlash) {
                sequence.Join(target.DOScale(Vector3.one * _flashIntensity, 0.1f).SetLoops(2, LoopType.Yoyo));
            }
            
            // Chaotic wobble during explosion - like confetti settling
            sequence.AppendCallback(() => {
                target.DOShakePosition(0.5f, new Vector3(30, 30, 0), 20, 0, true);
                target.DOShakeRotation(0.5f, new Vector3(0, 0, _wobbleIntensity), 15, 0, true);
            });
            
            // Growth phase - coming to life
            var growthDuration = GetGrowthDuration();
            var growthEase = GetGrowthEase();
            
            sequence.Append(target.DOScale(Vector3.one * 1.3f, growthDuration).SetEase(growthEase));
            
            // Add organic wobble during growth
            if (_addPersonalityWobble) {
                sequence.Join(target.DOLocalRotate(new Vector3(0, 0, 10), growthDuration * 0.3f).SetEase(Ease.InOutSine).SetLoops(3, LoopType.Yoyo));
            }
            
            // Settlement bounces - like confetti coming to rest
            var currentBounceHeight = 50f;
            for (int i = 0; i < _settlementBounces; i++) {
                var bounceDuration = 0.3f / (i + 1);
                
                // Bounce up
                sequence.Append(target.DOAnchorPos(target.anchoredPosition + Vector2.up * currentBounceHeight, bounceDuration * 0.5f).SetEase(Ease.OutQuad));
                sequence.Join(target.DOScale(Vector3.one * (1f + currentBounceHeight * 0.002f), bounceDuration * 0.5f).SetEase(Ease.OutQuad));
                
                // Fall down
                sequence.Append(target.DOAnchorPos(Vector2.zero, bounceDuration * 0.5f).SetEase(Ease.InBounce));
                sequence.Join(target.DOScale(Vector3.one, bounceDuration * 0.5f).SetEase(Ease.InBounce));
                
                currentBounceHeight *= _bounceDecay;
            }
            
            // Final personality - coming alive
            if (_addPersonalityWobble) {
                sequence.Append(target.DOShakeRotation(1f, new Vector3(0, 0, _finalWobble), 10, 0, true));
            }
            
            // Breathing effect - it's alive!
            if (_addBreathing) {
                sequence.AppendCallback(() => StartBreathing(target));
            }
            
            // Celebration sound
            if (_triggerCelebrationSound) {
                sequence.AppendCallback(() => {
                    Debug.Log("🎉 Celebration sound would play here");
                });
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Stop breathing if active
            StopBreathing(target);
            
            // Final celebration burst before leaving
            sequence.Append(target.DOScale(_explosionScale * 1.2f, 0.2f).SetEase(Ease.OutBack));
            sequence.Join(target.DOShakeRotation(0.3f, new Vector3(0, 0, _wobbleIntensity * 2f), 20, 0, true));
            
            // Quick fade and shrink
            sequence.Append(target.DOScale(_birthScale, 0.3f).SetEase(Ease.InBack));
            sequence.Join(canvasGroup.DOFade(0, 0.3f).SetEase(Ease.InQuad));
            
            // Final pop sound
            if (_triggerPopSound) {
                sequence.AppendCallback(() => {
                    Debug.Log("👋 Goodbye pop! Sound would play here");
                });
            }
            
            return sequence.Play();
        }

        private float GetGrowthDuration()
        {
            return _growthFeel switch
            {
                GrowthFeel.Organic => 0.8f,
                GrowthFeel.Bouncy => 0.6f,
                GrowthFeel.Magical => 1.0f,
                GrowthFeel.Explosive => 0.4f,
                _ => 0.6f
            };
        }

        private Ease GetGrowthEase()
        {
            return _growthFeel switch
            {
                GrowthFeel.Organic => Ease.OutElastic,
                GrowthFeel.Bouncy => Ease.OutBack,
                GrowthFeel.Magical => Ease.OutCirc,
                GrowthFeel.Explosive => Ease.OutQuad,
                _ => Ease.OutBack
            };
        }

        private void StartBreathing(RectTransform target)
        {
            if (!_addBreathing) return;
            
            var breatheScale = Vector3.one * (1f + _breathingIntensity);
            target.DOScale(breatheScale, 2f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        private void StopBreathing(RectTransform target)
        {
            // Don't use DOKill() here as it's already called in PlayHideAnimation
            // Just reset the state
            target.localScale = Vector3.one;
        }
    }
}