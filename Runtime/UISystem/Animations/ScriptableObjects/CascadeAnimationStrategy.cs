using AK.Core.Extensions;
using AK.Kernel.Timing;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems.Animations
{
    [CreateAssetMenu(fileName = "CascadeAnimation", menuName = "AK/UI/Animations/Cascade Animation")]
    public class CascadeAnimationStrategy : AnimationStrategy
    {
        [SerializeField] [Tooltip("Initial trigger delay")]
        private float _triggerDelay = 0.2f;
        
        [SerializeField] [Tooltip("Trigger animation type")]
        private TriggerType _triggerType = TriggerType.Explosion;
        
        [SerializeField] [Tooltip("Trigger intensity")]
        private float _triggerIntensity = 1.5f;
        
        [SerializeField] [Tooltip("Wave speed")]
        private float _waveSpeed = 2f;
        
        [SerializeField] [Tooltip("Wave decay")]
        private float _waveDecay = 0.8f;
        
        [SerializeField] [Tooltip("Number of cascade stages")]
        private int _cascadeStages = 5;
        
        [SerializeField] [Tooltip("Stage delay multiplier")]
        private float _stageDelayMultiplier = 0.7f;
        
        [SerializeField] [Tooltip("Add random delays")]
        private bool _addRandomDelays = true;
        
        [SerializeField] [Tooltip("Random delay range")]
        private Vector2 _randomDelayRange = new Vector2(0.1f, 0.3f);
        
        [SerializeField] [Tooltip("Element reaction type")]
        private ElementReaction _elementReaction = ElementReaction.PopAndBounce;
        
        [SerializeField] [Tooltip("Reaction intensity")]
        private float _reactionIntensity = 1f;
        
        [SerializeField] [Tooltip("Add element variation")]
        private bool _addElementVariation = true;
        
        [SerializeField] [Tooltip("Add trail effects")]
        private bool _addTrailEffects = true;
        
        [SerializeField] [Tooltip("Add glow propagation")]
        private bool _addGlowPropagation = true;
        
        public enum TriggerType
        {
            Explosion,
            Implosion,
            Ripple,
            Shockwave
        }
        
        public enum ElementReaction
        {
            PopAndBounce,
            ScaleAndFade,
            RotateAndAppear,
            ShakeAndSettle,
            FlipAndReveal
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time, Vector2 entryPos = default)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Start invisible
            target.localScale = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Initial trigger
            sequence.AppendInterval(_triggerDelay);
            ExecuteTrigger(sequence, target, canvasGroup);
            
            // Cascade stages
            for (int stage = 0; stage < _cascadeStages; stage++) {
                var stageDelay = GetStageDelay(stage);
                sequence.AppendInterval(stageDelay);
                
                // Execute cascade stage
                ExecuteCascadeStage(sequence, target, canvasGroup, stage);
            }
            
            // Final settlement
            sequence.Append(target.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
            sequence.Join(canvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad));
            
            return sequence.SetTimeDomain(time).Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Reverse cascade - elements disappear in reverse order
            for (int stage = _cascadeStages - 1; stage >= 0; stage--) {
                var stageDelay = GetStageDelay(stage) * 0.5f; // Faster reverse cascade
                
                if (stage < _cascadeStages - 1) {
                    sequence.AppendInterval(stageDelay);
                }
                
                ExecuteReverseCascadeStage(sequence, target, canvasGroup, stage);
            }
            
            // Final trigger reverse
            ExecuteReverseTrigger(sequence, target, canvasGroup);
            
            return sequence.SetTimeDomain(time).Play();
        }

        private void ExecuteTrigger(Sequence sequence, RectTransform target, CanvasGroup canvasGroup)
        {
            sequence.AppendCallback(() => canvasGroup.alpha = 0.3f);
            
            switch (_triggerType) {
                case TriggerType.Explosion:
                    sequence.Append(target.DOScale(Vector3.one * _triggerIntensity, 0.2f).SetEase(Ease.OutBack));
                    break;
                    
                case TriggerType.Implosion:
                    sequence.Append(target.DOScale(Vector3.one * _triggerIntensity * 2f, 0.15f).SetEase(Ease.InBack));
                    sequence.Append(target.DOScale(Vector3.zero, 0.15f).SetEase(Ease.OutBack));
                    break;
                    
                case TriggerType.Ripple:
                    sequence.Append(target.DOScale(Vector3.one * _triggerIntensity, 0.3f).SetEase(Ease.InOutSine));
                    sequence.Join(target.DOShakeRotation(0.3f, new Vector3(0, 0, 10), 5, 0, true));
                    break;
                    
                case TriggerType.Shockwave:
                    sequence.Append(target.DOScale(Vector3.one * _triggerIntensity * 1.5f, 0.1f).SetEase(Ease.OutBack));
                    sequence.Append(target.DOScale(Vector3.one * 0.5f, 0.2f).SetEase(Ease.InBack));
                    break;
            }
        }

        private void ExecuteCascadeStage(Sequence sequence, RectTransform target, CanvasGroup canvasGroup, int stage)
        {
            var intensity = _reactionIntensity * Mathf.Pow(_waveDecay, stage);
            var variation = _addElementVariation ? Random.Range(0.8f, 1.2f) : 1f;
            intensity *= variation;
            
            sequence.AppendCallback(() => canvasGroup.alpha = Mathf.Min(1f, 0.3f + (stage * 0.2f)));
            
            switch (_elementReaction) {
                case ElementReaction.PopAndBounce:
                    sequence.Append(target.DOScale(Vector3.one * intensity, 0.2f).SetEase(Ease.OutBack));
                    sequence.Append(target.DOScale(Vector3.one * 0.8f, 0.1f).SetEase(Ease.InBack));
                    sequence.Append(target.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutBack));
                    break;
                    
                case ElementReaction.ScaleAndFade:
                    sequence.Append(target.DOScale(Vector3.one * intensity, 0.3f).SetEase(Ease.OutQuad));
                    sequence.Join(canvasGroup.DOFade(1f, 0.3f).SetEase(Ease.OutQuad));
                    break;
                    
                case ElementReaction.RotateAndAppear:
                    sequence.Append(target.DOScale(Vector3.one * intensity, 0.2f).SetEase(Ease.OutBack));
                    sequence.Join(target.DOLocalRotate(new Vector3(0, 0, 360 * intensity), 0.3f).SetEase(Ease.InOutSine));
                    break;
                    
                case ElementReaction.ShakeAndSettle:
                    sequence.Append(target.DOShakeScale(0.3f, Vector3.one * intensity * 0.3f, 10, 0, true));
                    sequence.Append(target.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack));
                    break;
                    
                case ElementReaction.FlipAndReveal:
                    sequence.Append(target.DOScale(new Vector3(1, intensity, 1), 0.2f).SetEase(Ease.OutBack));
                    sequence.Append(target.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack));
                    break;
            }
            
            // Add trail effects
            if (_addTrailEffects && stage > 0) {
                sequence.Join(target.DOShakePosition(0.2f, new Vector2(10, 10) * intensity, 5, 0, true));
            }
            
            // Add glow propagation
            if (_addGlowPropagation) {
                var glowScale = Vector3.one * (1f + intensity * 0.2f);
                sequence.Join(target.DOScale(glowScale, 0.1f).SetLoops(2, LoopType.Yoyo));
            }
        }

        private void ExecuteReverseCascadeStage(Sequence sequence, RectTransform target, CanvasGroup canvasGroup, int stage)
        {
            var intensity = _reactionIntensity * Mathf.Pow(_waveDecay, stage);
            
            sequence.AppendCallback(() => canvasGroup.alpha = Mathf.Max(0f, 1f - (stage * 0.2f)));
            
            // Reverse reaction
            sequence.Append(target.DOScale(Vector3.one * (1f + intensity * 0.3f), 0.15f).SetEase(Ease.InBack));
            sequence.Append(target.DOScale(Vector3.one * (1f - intensity * 0.2f), 0.15f).SetEase(Ease.OutBack));
        }

        private void ExecuteReverseTrigger(Sequence sequence, RectTransform target, CanvasGroup canvasGroup)
        {
            switch (_triggerType) {
                case TriggerType.Explosion:
                    sequence.Append(target.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
                    break;
                    
                case TriggerType.Implosion:
                    sequence.Append(target.DOScale(Vector3.one * _triggerIntensity * 3f, 0.2f).SetEase(Ease.OutBack));
                    sequence.Append(target.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack));
                    break;
                    
                case TriggerType.Ripple:
                    sequence.Append(target.DOShakeRotation(0.3f, new Vector3(0, 0, 20), 10, 0, true));
                    sequence.Join(target.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
                    break;
                    
                case TriggerType.Shockwave:
                    sequence.Append(target.DOScale(Vector3.one * _triggerIntensity * 2f, 0.1f).SetEase(Ease.OutBack));
                    sequence.Append(target.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack));
                    break;
            }
            
            sequence.Join(canvasGroup.DOFade(0, 0.3f).SetEase(Ease.InQuad));
        }

        private float GetStageDelay(int stage)
        {
            var baseDelay = 1f / _waveSpeed;
            var stageDelay = baseDelay * Mathf.Pow(_stageDelayMultiplier, stage);
            
            if (_addRandomDelays) {
                stageDelay += Random.Range(_randomDelayRange.x, _randomDelayRange.y);
            }
            
            return stageDelay;
        }
    }
}