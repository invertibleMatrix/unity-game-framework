using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Systems.Animations
{
    [CreateAssetMenu(fileName = "ShakeAnimation", menuName = "AK/UI/Animations/Shake Animation")]
    public class ShakeAnimationStrategy : AnimationStrategy
    {
        [Title("Shake Settings")]
        [SerializeField] [Tooltip("Shake strength for position")]
        private Vector3 _positionStrength = new Vector3(10, 10, 0);
        
        [SerializeField] [Tooltip("Shake strength for rotation")]
        private Vector3 _rotationStrength = new Vector3(0, 0, 5);
        
        [SerializeField] [Tooltip("Shake strength for scale")]
        private Vector3 _scaleStrength = new Vector3(0.1f, 0.1f, 0);
        
        [Title("Shake Characteristics")]
        [SerializeField] [Tooltip("Number of vibrations")]
        private int _vibrato = 10;
        
        [SerializeField] [Tooltip("Randomness factor (0-1)")]
        private float _randomness = 0.5f;
        
        [SerializeField] [Tooltip("Should shake fade out?")]
        private bool _fadeOut = true;
        
        [Title("Entry Settings")]
        [SerializeField] [Tooltip("Initial scale before shake")]
        private Vector3 _initialScale = Vector3.zero;
        
        [SerializeField] [Tooltip("Pop in duration before shake")]
        private float _popInDuration = 0.2f;
        
        [Title("Exit Settings")]
        [SerializeField] [Tooltip("Shake intensity on exit")]
        private float _exitShakeIntensity = 2f;
        
        [SerializeField] [Tooltip("Final shake duration")]
        private float _finalShakeDuration = 0.5f;
        
        [Title("Effects")]
        [SerializeField] [Tooltip("Add glow effect during shake")]
        private bool _addGlowEffect = false;
        
        [SerializeField] [ShowIf("_addGlowEffect")] [Tooltip("Glow intensity")]
        private float _glowIntensity = 1.2f;
        
        [SerializeField] [Tooltip("Add screen shake effect")]
        private bool _addScreenShake = false;
        
        [SerializeField] [ShowIf("_addScreenShake")] [Tooltip("Screen shake strength")]
        private float _screenShakeStrength = 5f;

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Set initial state
            target.localScale = _initialScale;
            canvasGroup.alpha = 0f;
            
            // Pop in
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            sequence.Append(target.DOScale(Vector3.one, _popInDuration).SetEase(Ease.OutBack));
            
            // Start shake immediately after pop
            sequence.AppendCallback(() =>
            {
                // Position shake
                if (_positionStrength != Vector3.zero)
                {
                    target.DOShakePosition(EntryDuration, _positionStrength, _vibrato, _randomness, _fadeOut);
                }
                
                // Rotation shake
                if (_rotationStrength != Vector3.zero)
                {
                    target.DOShakeRotation(EntryDuration, _rotationStrength, _vibrato, _randomness, _fadeOut);
                }
                
                // Scale shake
                if (_scaleStrength != Vector3.zero)
                {
                    target.DOShakeScale(EntryDuration, _scaleStrength, _vibrato, _randomness, _fadeOut);
                }
                
                // Glow effect
                if (_addGlowEffect)
                {
                    target.DOScale(Vector3.one * _glowIntensity, 0.1f).SetLoops(10, LoopType.Yoyo).SetEase(Ease.InOutSine);
                }
                
                // Screen shake (if applicable)
                if (_addScreenShake)
                {
                    // This would need to be implemented in your camera system
                    // Camera.main.DOShakePosition(EntryDuration, _screenShakeStrength);
                }
            });
            
            // Settle after shake
            sequence.AppendInterval(EntryDuration);
            sequence.Append(target.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack));
            sequence.Append(target.DOLocalRotate(Vector3.zero, 0.2f).SetEase(Ease.OutBack));
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            // Intense shake before disappearing
            var exitPositionStrength = _positionStrength * _exitShakeIntensity;
            var exitRotationStrength = _rotationStrength * _exitShakeIntensity;
            var exitScaleStrength = _scaleStrength * _exitShakeIntensity;
            
            sequence.AppendCallback(() =>
            {
                // Position shake
                if (exitPositionStrength != Vector3.zero)
                {
                    target.DOShakePosition(_finalShakeDuration, exitPositionStrength, _vibrato * 2, _randomness, false);
                }
                
                // Rotation shake
                if (exitRotationStrength != Vector3.zero)
                {
                    target.DOShakeRotation(_finalShakeDuration, exitRotationStrength, _vibrato * 2, _randomness, false);
                }
                
                // Scale shake
                if (exitScaleStrength != Vector3.zero)
                {
                    target.DOShakeScale(_finalShakeDuration, exitScaleStrength, _vibrato * 2, _randomness, false);
                }
            });
            
            // Fade out during shake
            sequence.Join(canvasGroup.DOFade(0, _finalShakeDuration * 0.5f).SetEase(Ease.InQuad));
            
            // Final collapse
            sequence.Append(target.DOScale(Vector3.zero, ExitDuration * 0.3f).SetEase(Ease.InBack));
            
            return sequence.Play();
        }
    }
}