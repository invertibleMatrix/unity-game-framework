using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Systems.Animations
{
    [CreateAssetMenu(fileName = "OrganicGrowthAnimation", menuName = "AK/UI/Animations/Organic Growth Animation")]
    public class OrganicGrowthAnimationStrategy : AnimationStrategy
    {
        [Title("Seed Stage")]
        [SerializeField] [Tooltip("Initial seed size")]
        private Vector3 _seedScale = new Vector3(0.05f, 0.05f, 0.05f);
        
        [SerializeField] [Tooltip("Seed wobble amount")]
        private float _seedWobble = 2f;
        
        [SerializeField] [Tooltip("Germination delay")]
        private float _germinationDelay = 0.3f;
        
        [Title("Growth Pattern")]
        [SerializeField] [Tooltip("Growth type")]
        private GrowthType _growthType = GrowthType.Natural;
        
        [SerializeField] [Tooltip("Growth stages")]
        private int _growthStages = 4;
        
        [SerializeField] [Tooltip("Growth speed variation")]
        private float _growthVariation = 0.3f;
        
        [Title("Organic Movement")]
        [SerializeField] [Tooltip("Add swaying movement")]
        private bool _addSwaying = true;
        
        [SerializeField] [ShowIf("_addSwaying")] [Tooltip("Sway intensity")]
        private float _swayIntensity = 5f;
        
        [SerializeField] [ShowIf("_addSwaying")] [Tooltip("Sway speed")]
        private float _swaySpeed = 2f;
        
        [Title("Imperfection")]
        [SerializeField] [Tooltip("Add growth imperfections")]
        private bool _addImperfections = true;
        
        [SerializeField] [ShowIf("_addImperfections")] [Tooltip("Stutter frequency")]
        private float _stutterFrequency = 0.1f;
        
        [SerializeField] [ShowIf("_addImperfections")] [Tooltip("Uneven growth")]
        private Vector3 _unevenGrowth = new Vector3(0.1f, 0.15f, 0.1f);
        
        [Title("Maturation")]
        [SerializeField] [Tooltip("Maturation wobble")]
        private float _maturationWobble = 3f;
        
        [SerializeField] [Tooltip("Final breathing")]
        private bool _addFinalBreathing = true;
        
        [SerializeField] [ShowIf("_addFinalBreathing")] [Tooltip("Breathing gentleness")]
        private float _breathingGentleness = 0.02f;
        
        [Title("Life Feeling")]
        [SerializeField] [Tooltip("Add personality twitches")]
        private bool _addPersonalityTwitches = true;
        
        [SerializeField] [ShowIf("_addPersonalityTwitches")] [Tooltip("Twitch frequency")]
        private float _twitchFrequency = 3f;
        
        [SerializeField] [Tooltip("Add subtle rotation")]
        private bool _addSubtleRotation = true;
        
        [SerializeField] [ShowIf("_addSubtleRotation")] [Tooltip("Rotation amount")]
        private float _subtleRotation = 2f;
        
        public enum GrowthType
        {
            Natural,     // Like a plant growing
            Crystalline, // Like crystal formation
            Fungal,      // Like mushroom growth
            Cellular     // Like cell division
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Start as a tiny seed
            target.localScale = _seedScale;
            canvasGroup.alpha = 0f;
            
            // Seed stage - barely alive
            sequence.AppendCallback(() => canvasGroup.alpha = 0.3f);
            sequence.Append(target.DOShakeScale(_germinationDelay, _seedScale * 0.5f, 5, 0, true));
            
            // Germination - coming to life
            sequence.AppendCallback(() => canvasGroup.alpha = 0.6f);
            sequence.Append(target.DOScale(_seedScale * 2f, 0.2f).SetEase(Ease.OutBack));
            
            // Growth stages - like a plant growing
            var currentScale = _seedScale * 2f;
            for (int stage = 0; stage < _growthStages; stage++)
            {
                var stageDuration = GetStageDuration(stage);
                var targetScale = GetStageScale(stage);
                
                // Growth with imperfections
                if (_addImperfections && Random.value < _stutterFrequency)
                {
                    // Small stutter in growth
                    sequence.Append(target.DOScale(currentScale * 0.9f, 0.05f).SetEase(Ease.InOutSine));
                    sequence.Append(target.DOScale(currentScale, 0.05f).SetEase(Ease.InOutSine));
                }
                
                // Main growth
                sequence.Append(target.DOScale(targetScale, stageDuration).SetEase(GetGrowthEase(stage)));
                
                // Add uneven growth
                if (_addImperfections)
                {
                    var unevenScale = targetScale + Vector3.Scale(_unevenGrowth, new Vector3(
                        Random.Range(-1f, 1f),
                        Random.Range(-1f, 1f),
                        Random.Range(-1f, 1f)
                    ));
                    sequence.Join(target.DOScale(unevenScale, stageDuration * 0.3f).SetEase(Ease.InOutSine).SetLoops(2, LoopType.Yoyo));
                }
                
                // Add swaying during growth
                if (_addSwaying)
                {
                    sequence.Join(target.DOShakeRotation(stageDuration, new Vector3(0, 0, _swayIntensity), 3, 0, true));
                }
                
                currentScale = targetScale;
            }
            
            // Maturation - settling into final form
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            sequence.Append(target.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
            
            // Maturation wobble - like finding balance
            sequence.Append(target.DOShakeRotation(0.5f, new Vector3(0, 0, _maturationWobble), 8, 0, true));
            
            // Final breathing - it's alive!
            if (_addFinalBreathing)
            {
                sequence.AppendCallback(() => StartBreathing(target));
            }
            
            // Add personality
            if (_addPersonalityTwitches)
            {
                sequence.AppendCallback(() => StartPersonalityTwitches(target));
            }
            
            // Subtle rotation. Must NOT be appended into the sequence: an infinite-loop child
            // makes the sequence never complete (the show pipeline would await it forever).
            // Start it as a standalone linked tween when the sequence reaches this point.
            if (_addSubtleRotation)
            {
                sequence.AppendCallback(() =>
                    target.DOLocalRotate(new Vector3(0, 0, _subtleRotation), 1f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable));
            }

            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Stop life effects
            StopBreathing(target);
            StopPersonalityTwitches(target);
            
            // Withering - reverse growth
            sequence.Append(target.DOScale(Vector3.one * 1.1f, 0.2f).SetEase(Ease.OutBack));
            
            // Rapid decay
            for (int stage = _growthStages - 1; stage >= 0; stage--)
            {
                var stageScale = GetStageScale(stage);
                var stageDuration = GetStageDuration(stage) * 0.5f; // Faster decay
                
                sequence.Append(target.DOScale(stageScale, stageDuration).SetEase(Ease.InBack));
                
                if (_addImperfections)
                {
                    sequence.Join(target.DOShakeRotation(stageDuration * 0.5f, new Vector3(0, 0, _swayIntensity), 5, 0, true));
                }
            }
            
            // Return to seed
            sequence.Append(target.DOScale(_seedScale, 0.3f).SetEase(Ease.InBack));
            sequence.Join(canvasGroup.DOFade(0, 0.3f).SetEase(Ease.InQuad));
            
            // Final disappearance
            sequence.Append(target.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack));
            
            return sequence.Play();
        }

        private float GetStageDuration(int stage)
        {
            var baseDuration = 0.4f;
            var variation = Random.Range(-_growthVariation, _growthVariation);
            return baseDuration + variation + (stage * 0.1f);
        }

        private Vector3 GetStageScale(int stage)
        {
            var progress = (float)(stage + 1) / _growthStages;
            
            return _growthType switch
            {
                GrowthType.Natural => Vector3.one * Mathf.Lerp(0.1f, 1f, progress),
                GrowthType.Crystalline => Vector3.one * Mathf.Pow(progress, 0.7f), // Faster initial growth
                GrowthType.Fungal => Vector3.one * Mathf.Lerp(0.1f, 1.2f, progress), // Overshoot
                GrowthType.Cellular => Vector3.one * (1f + Mathf.Sin(progress * Mathf.PI) * 0.2f), // Pulsing growth
                _ => Vector3.one * progress
            };
        }

        private Ease GetGrowthEase(int stage)
        {
            return _growthType switch
            {
                GrowthType.Natural => stage % 2 == 0 ? Ease.OutBack : Ease.OutElastic,
                GrowthType.Crystalline => Ease.OutQuad,
                GrowthType.Fungal => Ease.OutElastic,
                GrowthType.Cellular => Ease.InOutSine,
                _ => Ease.OutBack
            };
        }

        private void StartBreathing(RectTransform target)
        {
            if (!_addFinalBreathing) return;
            
            var breatheScale = Vector3.one * (1f + _breathingGentleness);
            target.DOScale(breatheScale, 3f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
        }

        private void StopBreathing(RectTransform target)
        {
            // Don't use DOKill() here as it's already called in PlayHideAnimation
            // Just reset the state
            target.localScale = Vector3.one;
        }

        private void StartPersonalityTwitches(RectTransform target)
        {
            if (!_addPersonalityTwitches) return;
            
            DOTween.Sequence()
                .Append(target.DOLocalRotate(new Vector3(0, 0, 1), 0.1f).SetEase(Ease.InOutSine))
                .Append(target.DOLocalRotate(Vector3.zero, 0.1f).SetEase(Ease.InOutSine))
                .SetLoops(-1, LoopType.Restart)
                .SetDelay(Random.Range(1f, 3f))
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable)
                .Play();
        }

        private void StopPersonalityTwitches(RectTransform target)
        {
            // Twitches will stop when DOKill() is called in StopBreathing
        }
    }
}