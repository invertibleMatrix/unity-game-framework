using AK.Core.Extensions;
using AK.Kernel.Timing;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems.Animations
{
    [CreateAssetMenu(fileName = "PulseAnimation", menuName = "AK/UI/Animations/Pulse Animation")]
    public class PulseAnimationStrategy : AnimationStrategy
    {
        [SerializeField] [Tooltip("Pulse scale amount")]
        private Vector3 _pulseScale = new Vector3(1.2f, 1.2f, 1.2f);
        
        [SerializeField] [Tooltip("Pulse frequency")]
        private float _pulseFrequency = 2f;
        
        [SerializeField] [Tooltip("Number of pulses on show")]
        private int _showPulses = 3;
        
        [SerializeField] [Tooltip("Number of pulses on hide")]
        private int _hidePulses = 2;
        
        [SerializeField] [Tooltip("Pulse fade amount")]
        private float _pulseFadeAmount = 0.3f;
        
        [SerializeField] [Tooltip("Fade with scale")]
        private bool _fadeWithScale = true;
        
        [SerializeField] [Tooltip("Initial scale")]
        private Vector3 _initialScale = Vector3.zero;
        
        [SerializeField] [Tooltip("Add rotation pulse")]
        private bool _addRotationPulse = false;
        
        [SerializeField] [Tooltip("Rotation amount")]
        private Vector3 _rotationAmount = new Vector3(0, 0, 5f);
        
        [SerializeField] [Tooltip("Add position pulse")]
        private bool _addPositionPulse = false;
        
        [SerializeField] [Tooltip("Position amount")]
        private Vector2 _positionAmount = new Vector2(10, 10);
        
        [SerializeField] [Tooltip("Keep pulsing after show")]
        private bool _continuousPulse = false;
        
        [SerializeField] [Tooltip("Continuous pulse speed")]
        private float _continuousSpeed = 1f;
        
        [SerializeField] [Tooltip("Continuous pulse intensity")]
        private float _continuousIntensity = 0.1f;

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Set initial state
            target.localScale = _initialScale;
            canvasGroup.alpha = 0f;
            
            // Entry animation
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            sequence.Append(target.DOScale(Vector3.one, EntryDuration).SetEase(Ease.OutBack));
            
            // Pulse sequence
            for (int i = 0; i < _showPulses; i++)
            {
                var pulseDuration = 1f / _pulseFrequency;
                
                // Scale up
                sequence.Append(target.DOScale(_pulseScale, pulseDuration * 0.5f).SetEase(Ease.OutSine));
                
                // Fade if enabled
                if (_fadeWithScale)
                {
                    sequence.Join(canvasGroup.DOFade(1f - _pulseFadeAmount, pulseDuration * 0.5f).SetEase(Ease.OutSine));
                }
                
                // Rotation pulse
                if (_addRotationPulse)
                {
                    sequence.Join(target.DOLocalRotate(_rotationAmount, pulseDuration * 0.5f).SetEase(Ease.OutSine));
                }
                
                // Position pulse
                if (_addPositionPulse)
                {
                    var randomOffset = new Vector2(
                        Random.Range(-_positionAmount.x, _positionAmount.x),
                        Random.Range(-_positionAmount.y, _positionAmount.y)
                    );
                    sequence.Join(target.DOAnchorPos(randomOffset, pulseDuration * 0.5f).SetEase(Ease.OutSine));
                }
                
                // Scale down
                sequence.Append(target.DOScale(Vector3.one, pulseDuration * 0.5f).SetEase(Ease.InSine));
                
                // Restore fade
                if (_fadeWithScale)
                {
                    sequence.Join(canvasGroup.DOFade(1f, pulseDuration * 0.5f).SetEase(Ease.InSine));
                }
                
                // Restore rotation
                if (_addRotationPulse)
                {
                    sequence.Join(target.DOLocalRotate(Vector3.zero, pulseDuration * 0.5f).SetEase(Ease.InSine));
                }
                
                // Restore position
                if (_addPositionPulse)
                {
                    sequence.Join(target.DOAnchorPos(Vector2.zero, pulseDuration * 0.5f).SetEase(Ease.InSine));
                }
            }
            
            // Start continuous pulse if enabled
            if (_continuousPulse)
            {
                sequence.AppendCallback(() => StartContinuousPulse(target, canvasGroup, time));
            }
            
            return sequence.SetTimeDomain(time).Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time)
        {
            var sequence = DOTween.Sequence();

            // Pulse sequence before hiding
            for (int i = 0; i < _hidePulses; i++)
            {
                var pulseDuration = 1f / _pulseFrequency;
                
                // Scale up
                sequence.Append(target.DOScale(_pulseScale, pulseDuration * 0.5f).SetEase(Ease.OutSine));
                
                // Fade if enabled
                if (_fadeWithScale)
                {
                    sequence.Join(canvasGroup.DOFade(1f - _pulseFadeAmount, pulseDuration * 0.5f).SetEase(Ease.OutSine));
                }
                
                // Scale down
                sequence.Append(target.DOScale(Vector3.one, pulseDuration * 0.5f).SetEase(Ease.InSine));
                
                // Restore fade
                if (_fadeWithScale)
                {
                    sequence.Join(canvasGroup.DOFade(1f, pulseDuration * 0.5f).SetEase(Ease.InSine));
                }
            }
            
            // Final exit
            sequence.Append(target.DOScale(_initialScale, ExitDuration).SetEase(Ease.InBack));
            sequence.Join(canvasGroup.DOFade(0, ExitDuration).SetEase(Ease.InQuad));
            
            return sequence.SetTimeDomain(time).Play();
        }

        private void StartContinuousPulse(RectTransform target, CanvasGroup canvasGroup, TimeDomain time)
        {
            var pulseScale = Vector3.one * (1f + _continuousIntensity);
            var pulseDuration = 1f / _continuousSpeed;
            
            // Each view's own loop, never kept on this shared asset. It targets the content, so
            // the view's next entrance or exit stops it, and it dies with the content.
            DOTween.Sequence()
                .Append(target.DOScale(pulseScale, pulseDuration * 0.5f).SetEase(Ease.InOutSine))
                .Append(target.DOScale(Vector3.one, pulseDuration * 0.5f).SetEase(Ease.InOutSine))
                .SetLoops(-1, LoopType.Restart)
                .SetTarget(target)
                .SetTimeDomain(time)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable)
                .Play();
        }
    }
}