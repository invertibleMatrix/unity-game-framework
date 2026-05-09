using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "PopSnapAnimation", menuName = "Gameplay/UI/Animations/Pop Snap Animation")]
    public class PopSnapAnimationStrategy : AnimationStrategy
    {
        [Title("Pop Characteristics")]
        [SerializeField] [Tooltip("Pop speed")]
        private float _popSpeed = 0.15f;
        
        [SerializeField] [Tooltip("Pop scale multiplier")]
        private float _popScaleMultiplier = 1.4f;
        
        [SerializeField] [Tooltip("Snap back intensity")]
        private float _snapBackIntensity = 0.8f;
        
        [Title("Bubble Physics")]
        [SerializeField] [Tooltip("Add bubble wobble")]
        private bool _addBubbleWobble = true;
        
        [SerializeField] [ShowIf("_addBubbleWobble")] [Tooltip("Wobble frequency")]
        private float _wobbleFrequency = 20f;
        
        [SerializeField] [Tooltip("Add surface tension")]
        private bool _addSurfaceTension = true;
        
        [SerializeField] [ShowIf("_addSurfaceTension")] [Tooltip("Tension amount")]
        private float _tensionAmount = 0.2f;
        
        [Title("Visual Effects")]
        [SerializeField] [Tooltip("Add pop flash")]
        private bool _addPopFlash = true;
        
        [SerializeField] [ShowIf("_addPopFlash")] [Tooltip("Flash intensity")]
        private float _flashIntensity = 1.5f;
        
        [SerializeField] [Tooltip("Add ripple effect")]
        private bool _addRippleEffect = false;
        
        [Title("Multiple Pops")]
        [SerializeField] [Tooltip("Pop sequence")]
        private PopSequence _popSequence = PopSequence.Single;
        
        [SerializeField] [ShowIf("_popSequence", PopSequence.Multiple)] [Tooltip("Pop count")]
        private int _popCount = 3;
        
        [SerializeField] [ShowIf("_popSequence", PopSequence.Multiple)] [Tooltip("Pop interval")]
        private float _popInterval = 0.1f;
        
        [Title("Settlement")]
        [SerializeField] [Tooltip("Settle wobble")]
        private bool _addSettleWobble = true;
        
        [SerializeField] [ShowIf("_addSettleWobble")] [Tooltip("Settle intensity")]
        private float _settleIntensity = 2f;
        
        [SerializeField] [Tooltip("Add final breathing")]
        private bool _addFinalBreathing = false;
        
        [Title("Sound Integration")]
        [SerializeField] [Tooltip("Pop sound type")]
        private PopSoundType _popSoundType = PopSoundType.Bubble;
        
        [SerializeField] [Tooltip("Add pitch variation")]
        private bool _addPitchVariation = true;
        
        public enum PopSequence
        {
            Single,      // One quick pop
            Multiple,    // Multiple rapid pops
            Delayed,     // Pop with anticipation
            Chain        // Chain reaction pops
        }
        
        public enum PopSoundType
        {
            Bubble,
            Snap,
            Click,
            Pop,
            Boing
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Start invisible
            target.localScale = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Execute pop sequence
            switch (_popSequence) {
                case PopSequence.Single:
                    ExecuteSinglePop(sequence, target, canvasGroup);
                    break;
                case PopSequence.Multiple:
                    ExecuteMultiplePops(sequence, target, canvasGroup);
                    break;
                case PopSequence.Delayed:
                    ExecuteDelayedPop(sequence, target, canvasGroup);
                    break;
                case PopSequence.Chain:
                    ExecuteChainPop(sequence, target, canvasGroup);
                    break;
            }
            
            // Settlement phase
            if (_addSettleWobble) {
                sequence.AppendCallback(() => {
                    target.DOShakeRotation(0.5f, new Vector3(0, 0, _settleIntensity), 8, 0, true);
                });
            }
            
            // Final breathing
            if (_addFinalBreathing) {
                sequence.AppendCallback(() => StartBreathing(target));
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Stop breathing
            StopBreathing(target);
            
            // Quick reverse pop
            sequence.Append(target.DOScale(Vector3.one * _popScaleMultiplier, _popSpeed * 0.5f).SetEase(Ease.OutBack));
            
            // Pop sound
            PlayPopSound();
            
            // Disappear
            sequence.Append(target.DOScale(Vector3.zero, _popSpeed * 0.5f).SetEase(Ease.InBack));
            sequence.Join(canvasGroup.DOFade(0, _popSpeed * 0.3f).SetEase(Ease.InQuad));
            
            return sequence.Play();
        }

        private void ExecuteSinglePop(Sequence sequence, RectTransform target, CanvasGroup canvasGroup)
        {
            // Anticipation
            sequence.AppendCallback(() => canvasGroup.alpha = 0.5f);
            sequence.Append(target.DOScale(Vector3.one * 0.1f, _popSpeed * 0.3f).SetEase(Ease.OutBack));
            
            // THE POP!
            sequence.AppendCallback(() => {
                canvasGroup.alpha = 1f;
                PlayPopSound();
            });
            
            sequence.Append(target.DOScale(Vector3.one * _popScaleMultiplier, _popSpeed * 0.7f).SetEase(Ease.OutBack));
            
            // Flash effect
            if (_addPopFlash) {
                sequence.Join(target.DOScale(Vector3.one * _flashIntensity, 0.1f).SetLoops(2, LoopType.Yoyo));
            }
            
            // Bubble wobble
            if (_addBubbleWobble) {
                sequence.Join(target.DOShakeRotation(_popSpeed, new Vector3(0, 0, 10), (int)_wobbleFrequency, 0, true));
            }
            
            // Surface tension effect
            if (_addSurfaceTension) {
                sequence.Append(target.DOScale(Vector3.one * (1f + _tensionAmount), _popSpeed * 0.3f).SetEase(Ease.OutBack));
                sequence.Append(target.DOScale(Vector3.one, _popSpeed * 0.3f).SetEase(Ease.InBack));
            }
            
            // Snap back
            sequence.Append(target.DOScale(Vector3.one * (1f + _snapBackIntensity), _popSpeed * 0.2f).SetEase(Ease.OutBack));
            sequence.Append(target.DOScale(Vector3.one, _popSpeed * 0.2f).SetEase(Ease.InBack));
        }

        private void ExecuteMultiplePops(Sequence sequence, RectTransform target, CanvasGroup canvasGroup)
        {
            for (int i = 0; i < _popCount; i++) {
                if (i > 0) {
                    sequence.AppendInterval(_popInterval);
                }
                
                var popScale = Vector3.one * (1f + (i * 0.1f));
                
                sequence.AppendCallback(() => {
                    if (i == 0) canvasGroup.alpha = 1f;
                    PlayPopSound(i);
                });
                
                sequence.Append(target.DOScale(popScale * _popScaleMultiplier, _popSpeed * 0.5f).SetEase(Ease.OutBack));
                
                if (_addBubbleWobble) {
                    sequence.Join(target.DOShakeRotation(_popSpeed * 0.5f, new Vector3(0, 0, 8), (int)_wobbleFrequency, 0, true));
                }
                
                sequence.Append(target.DOScale(popScale, _popSpeed * 0.5f).SetEase(Ease.InBack));
            }
            
            // Final snap to normal size
            sequence.Append(target.DOScale(Vector3.one, _popSpeed).SetEase(Ease.OutBack));
        }

        private void ExecuteDelayedPop(Sequence sequence, RectTransform target, CanvasGroup canvasGroup)
        {
            // Build tension
            sequence.AppendCallback(() => canvasGroup.alpha = 0.3f);
            sequence.Append(target.DOScale(Vector3.one * 0.2f, _popSpeed * 2f).SetEase(Ease.InOutSine));
            
            // Wobble anticipation
            if (_addBubbleWobble) {
                sequence.Join(target.DOShakeRotation(_popSpeed * 2f, new Vector3(0, 0, 5), (int)(_wobbleFrequency * 0.5f), 0, true));
            }
            
            // DELAYED POP!
            sequence.AppendInterval(0.2f);
            sequence.AppendCallback(() => {
                canvasGroup.alpha = 1f;
                PlayPopSound();
            });
            
            sequence.Append(target.DOScale(Vector3.one * _popScaleMultiplier * 1.5f, _popSpeed).SetEase(Ease.OutBack));
            
            if (_addPopFlash) {
                sequence.Join(target.DOScale(Vector3.one * _flashIntensity * 1.5f, 0.15f).SetLoops(2, LoopType.Yoyo));
            }
            
            // Settle down
            sequence.Append(target.DOScale(Vector3.one, _popSpeed).SetEase(Ease.OutBack));
        }

        private void ExecuteChainPop(Sequence sequence, RectTransform target, CanvasGroup canvasGroup)
        {
            var chainCount = 3;
            
            for (int i = 0; i < chainCount; i++) {
                if (i > 0) {
                    sequence.AppendInterval(_popInterval * 0.5f);
                }
                
                var chainScale = Vector3.one * (0.3f + (i * 0.2f));
                
                sequence.AppendCallback(() => {
                    if (i == 0) canvasGroup.alpha = 1f;
                    PlayPopSound(i);
                });
                
                sequence.Append(target.DOScale(chainScale * _popScaleMultiplier, _popSpeed * 0.4f).SetEase(Ease.OutBack));
                sequence.Append(target.DOScale(chainScale, _popSpeed * 0.4f).SetEase(Ease.InBack));
            }
            
            // Final big pop
            sequence.AppendCallback(() => PlayPopSound());
            sequence.Append(target.DOScale(Vector3.one * _popScaleMultiplier * 1.2f, _popSpeed * 0.6f).SetEase(Ease.OutBack));
            sequence.Append(target.DOScale(Vector3.one, _popSpeed * 0.4f).SetEase(Ease.InBack));
        }

        private void PlayPopSound(int variation = 0)
        {
            var pitch = _addPitchVariation ? 1f + (variation * 0.2f) : 1f;
            Debug.Log($"🫧 {_popSoundType} pop sound! Pitch: {pitch}");
        }

        private void StartBreathing(RectTransform target)
        {
            if (!_addFinalBreathing) return;
            
            var breatheScale = Vector3.one * 1.05f;
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