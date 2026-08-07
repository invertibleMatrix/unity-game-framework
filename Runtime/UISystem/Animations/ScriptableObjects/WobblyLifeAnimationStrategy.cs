using DG.Tweening;
using UnityEngine;

namespace AK.Systems.Animations
{
    [CreateAssetMenu(fileName = "WobblyLifeAnimation", menuName = "AK/UI/Animations/Wobbly Life Animation")]
    public class WobblyLifeAnimationStrategy : AnimationStrategy
    {
        [SerializeField] [Tooltip("Initial wobble intensity")]
        private float _birthWobbleIntensity = 20f;
        
        [SerializeField] [Tooltip("Birth wobble speed")]
        private float _birthWobbleSpeed = 15f;
        
        [SerializeField] [Tooltip("Struggle duration")]
        private float _struggleDuration = 1.2f;
        
        [SerializeField] [Tooltip("Life form type")]
        private LifeFormType _lifeFormType = LifeFormType.Playful;
        
        [SerializeField] [Tooltip("Base wobble amount")]
        private float _baseWobble = 3f;
        
        [SerializeField] [Tooltip("Breathing intensity")]
        private float _breathingIntensity = 0.08f;
        
        [SerializeField] [Tooltip("Heartbeat rate")]
        private float _heartbeatRate = 1.2f;
        
        [SerializeField] [Tooltip("Add nervous twitches")]
        private bool _addNervousTwitches = true;
        
        [SerializeField] [Tooltip("Twitch frequency")]
        private float _twitchFrequency = 2.5f;
        
        [SerializeField] [Tooltip("Add idle dancing")]
        private bool _addIdleDancing = true;
        
        [SerializeField] [Tooltip("Dance style")]
        private DanceStyle _danceStyle = DanceStyle.GentleSway;
        
        [SerializeField] [Tooltip("Add emotional reactions")]
        private bool _addEmotionalReactions = true;
        
        [SerializeField] [Tooltip("Excitement level")]
        private float _excitementLevel = 0.7f;
        
        [SerializeField] [Tooltip("Add attention seeking")]
        private bool _addAttentionSeeking = true;
        
        [SerializeField] [Tooltip("Jiggle physics")]
        private bool _addJigglePhysics = true;
        
        [SerializeField] [Tooltip("Jiggle amount")]
        private float _jiggleAmount = 5f;
        
        [SerializeField] [Tooltip("Add stretch and squash")]
        private bool _addStretchSquash = true;
        
        [SerializeField] [Tooltip("Squash sensitivity")]
        private float _squashSensitivity = 0.3f;
        
        [SerializeField] [Tooltip("Add life sounds")]
        private bool _addLifeSounds = false;
        
        [SerializeField] [Tooltip("Sound frequency")]
        private float _soundFrequency = 3f;
        
        private Tween _lifeTween;
        private Tween _breathingTween;
        private Tween _heartbeatTween;
        private Tween _personalityTween;
        
        public enum LifeFormType
        {
            Playful,     // Bouncy and energetic
            Sleepy,      // Slow and gentle
            Nervous,     // Twitchy and anxious
            Confident,   // Smooth and steady
            Excited      // Fast and chaotic
        }
        
        public enum DanceStyle
        {
            GentleSway,     // Soft side-to-side
            BouncyBop,      // Up and down bouncing
            CircularSwirl,  // Circular motion
            RandomTwitch,   // Unpredictable movements
            RhythmicPulse   // Pulsing in place
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Start from nothing - before life
            target.localScale = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // The struggle of birth - wobbly emergence
            sequence.AppendCallback(() => canvasGroup.alpha = 0.5f);
            
            // Birth struggle - like something trying to break free
            for (int i = 0; i < 5; i++) {
                sequence.Append(target.DOScale(Vector3.one * Random.Range(0.1f, 0.3f), 0.1f).SetEase(Ease.InOutSine));
                sequence.Join(target.DOShakeRotation(0.1f, new Vector3(0, 0, _birthWobbleIntensity), 5, 0, true));
            }
            
            // Break free! - The moment of birth
            sequence.AppendCallback(() => {
                canvasGroup.alpha = 1f;
                if (_addLifeSounds) {
                    Debug.Log("👶 Birth sound would play here");
                }
            });
            
            sequence.Append(target.DOScale(Vector3.one * 1.2f, 0.3f).SetEase(Ease.OutBack));
            sequence.Join(target.DOShakeRotation(_struggleDuration, new Vector3(0, 0, _birthWobbleIntensity), (int)_birthWobbleSpeed, 0, true));
            
            // Settle into life
            sequence.Append(target.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack));
            
            // Start all life processes
            sequence.AppendCallback(() => StartLifeProcesses(target));
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Stop all life processes
            StopLifeProcesses(target);
            
            // Death struggle - like losing energy
            sequence.Append(target.DOScale(Vector3.one * 1.1f, 0.2f).SetEase(Ease.InBack));
            sequence.Join(target.DOShakeRotation(0.5f, new Vector3(0, 0, _birthWobbleIntensity * 0.5f), 10, 0, true));
            
            // Fade away
            sequence.Append(canvasGroup.DOFade(0.5f, 0.3f).SetEase(Ease.InQuad));
            
            // Final breath
            sequence.Append(target.DOScale(Vector3.one * 0.8f, 0.2f).SetEase(Ease.OutBack));
            sequence.Append(target.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));
            
            // Death sound
            if (_addLifeSounds) {
                sequence.AppendCallback(() => {
                    Debug.Log("💀 Death sound would play here");
                });
            }
            
            return sequence.Play();
        }

        private void StartLifeProcesses(RectTransform target)
        {
            // Breathing - the most fundamental life sign
            var breathingScale = Vector3.one * (1f + _breathingIntensity);
            _breathingTween = target.DOScale(breathingScale, 2f / _heartbeatRate)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);

            // Heartbeat - pulsing life force
            var heartbeatScale = Vector3.one * (1f + _breathingIntensity * 0.3f);
            _heartbeatTween = target.DOScale(heartbeatScale, 0.3f / _heartbeatRate)
                .SetEase(Ease.OutBack)
                .SetLoops(-1, LoopType.Restart)
                .SetDelay(1f / _heartbeatRate)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);

            // Base wobble - constant life movement (999s shake = effectively infinite)
            var wobbleIntensity = GetLifeFormWobble();
            _lifeTween = target.DOShakeRotation(999f, new Vector3(0, 0, wobbleIntensity), (int)GetLifeFormSpeed(), 0, true)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
            
            // Personality traits
            if (_addNervousTwitches) {
                StartNervousTwitches(target);
            }
            
            if (_addIdleDancing) {
                StartIdleDancing(target);
            }
            
            if (_addEmotionalReactions) {
                StartEmotionalReactions(target);
            }
            
            if (_addAttentionSeeking) {
                StartAttentionSeeking(target);
            }
            
            if (_addJigglePhysics) {
                StartJigglePhysics(target);
            }
            
            if (_addLifeSounds) {
                StartLifeSounds(target);
            }
        }

        private void StopLifeProcesses(RectTransform target)
        {
            _breathingTween?.Kill();
            _heartbeatTween?.Kill();
            _lifeTween?.Kill();
            _personalityTween?.Kill();
            
            // Don't use DOKill() here as it's already called in PlayHideAnimation
            // Just reset the state
            target.localScale = Vector3.one;
            target.localEulerAngles = Vector3.zero;
        }

        private float GetLifeFormWobble()
        {
            return _lifeFormType switch
            {
                LifeFormType.Playful => _baseWobble * 1.5f,
                LifeFormType.Sleepy => _baseWobble * 0.3f,
                LifeFormType.Nervous => _baseWobble * 2f,
                LifeFormType.Confident => _baseWobble * 0.5f,
                LifeFormType.Excited => _baseWobble * 2.5f,
                _ => _baseWobble
            };
        }

        private float GetLifeFormSpeed()
        {
            return _lifeFormType switch
            {
                LifeFormType.Playful => 8f,
                LifeFormType.Sleepy => 2f,
                LifeFormType.Nervous => 15f,
                LifeFormType.Confident => 3f,
                LifeFormType.Excited => 12f,
                _ => 5f
            };
        }

        private void StartNervousTwitches(RectTransform target)
        {
            if (!_addNervousTwitches) return;
            
            _personalityTween = DOTween.Sequence()
                .Append(target.DOLocalRotate(new Vector3(0, 0, Random.Range(-5f, 5f)), 0.1f).SetEase(Ease.InOutSine))
                .Append(target.DOLocalRotate(Vector3.zero, 0.1f).SetEase(Ease.InOutSine))
                .SetLoops(-1, LoopType.Restart)
                .SetDelay(Random.Range(0.5f, 2f) / _twitchFrequency)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
        }

        private void StartIdleDancing(RectTransform target)
        {
            if (!_addIdleDancing) return;
            
            switch (_danceStyle) {
                case DanceStyle.GentleSway:
                    target.DOShakePosition(999f, new Vector2(10, 0), 2, 0, true)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
                    break;
                case DanceStyle.BouncyBop:
                    target.DOShakePosition(999f, new Vector2(0, 15), 3, 0, true)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
                    break;
                case DanceStyle.CircularSwirl:
                    DOTween.Sequence()
                        .Append(target.DOLocalRotate(new Vector3(0, 0, 5), 1f).SetEase(Ease.InOutSine))
                        .Append(target.DOLocalRotate(new Vector3(0, 0, -5), 1f).SetEase(Ease.InOutSine))
                        .SetLoops(-1, LoopType.Restart)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable)
                        .Play();
                    break;
                case DanceStyle.RandomTwitch:
                    target.DOShakePosition(999f, new Vector2(20, 20), 5, 0, true)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
                    break;
                case DanceStyle.RhythmicPulse:
                    var pulseScale = Vector3.one * 1.1f;
                    target.DOScale(pulseScale, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
                    break;
            }
        }

        private void StartEmotionalReactions(RectTransform target)
        {
            if (!_addEmotionalReactions) return;
            
            DOTween.Sequence()
                .AppendInterval(Random.Range(2f, 5f))
                .AppendCallback(() => {
                    // Random emotional burst
                    target.DOScale(Vector3.one * (1f + _excitementLevel), 0.2f).SetEase(Ease.OutBack)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
                    target.DOShakeRotation(0.3f, new Vector3(0, 0, 10 * _excitementLevel), 5, 0, true)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
                })
                .SetLoops(-1, LoopType.Restart)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable)
                .Play();
        }

        private void StartAttentionSeeking(RectTransform target)
        {
            if (!_addAttentionSeeking) return;
            
            DOTween.Sequence()
                .AppendInterval(Random.Range(3f, 8f))
                .AppendCallback(() => {
                    // Jump for attention
                    target.DOAnchorPos(target.anchoredPosition + Vector2.up * 20, 0.2f).SetEase(Ease.OutQuad)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
                    target.DOAnchorPos(Vector2.zero, 0.2f).SetEase(Ease.InBounce).SetDelay(0.2f)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
                })
                .SetLoops(-1, LoopType.Restart)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable)
                .Play();
        }

        private void StartJigglePhysics(RectTransform target)
        {
            if (!_addJigglePhysics) return;
            
            target.DOShakeScale(999f, Vector3.one * _jiggleAmount * 0.1f, 10, 0, true)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
        }

        private void StartLifeSounds(RectTransform target)
        {
            if (!_addLifeSounds) return;
            
            DOTween.Sequence()
                .AppendInterval(1f / _soundFrequency)
                .AppendCallback(() => {
                    Debug.Log("🔊 Life sound would play here");
                })
                .SetLoops(-1, LoopType.Restart)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDisable)
                .Play();
        }
    }
}