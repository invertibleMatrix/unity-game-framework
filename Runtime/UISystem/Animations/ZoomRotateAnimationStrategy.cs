using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "ZoomRotateAnimation", menuName = "Gameplay/UI/Animations/Zoom Rotate Animation")]
    public class ZoomRotateAnimationStrategy : AnimationStrategy
    {
        [Title("Zoom Settings")]
        [SerializeField] [Tooltip("Initial zoom scale")]
        private Vector3 _initialZoom = new Vector3(0.1f, 0.1f, 0.1f);
        
        [SerializeField] [Tooltip("Target zoom scale")]
        private Vector3 _targetZoom = Vector3.one;
        
        [SerializeField] [Tooltip("Zoom overshoot amount")]
        private Vector3 _zoomOvershoot = new Vector3(1.3f, 1.3f, 1.3f);
        
        [SerializeField] [Tooltip("Add zoom bounce")]
        private bool _addZoomBounce = true;
        
        [Title("Rotation Settings")]
        [SerializeField] [Tooltip("Rotation axis")]
        private RotationAxis _rotationAxis = RotationAxis.Z;
        
        [SerializeField] [Tooltip("Entry rotation amount")]
        private float _entryRotation = 720f;
        
        [SerializeField] [Tooltip("Exit rotation amount")]
        private float _exitRotation = -1080f;
        
        [SerializeField] [Tooltip("Rotation direction")]
        private RotationDirection _rotationDirection = RotationDirection.Clockwise;
        
        [SerializeField] [Tooltip("Add wobble to rotation")]
        private bool _addRotationWobble = false;
        
        [SerializeField] [ShowIf("_addRotationWobble")] [Tooltip("Wobble intensity")]
        private float _wobbleIntensity = 10f;
        
        [Title("Cinematic Effects")]
        [SerializeField] [Tooltip("Add camera shake effect")]
        private bool _addCameraShake = false;
        
        [SerializeField] [ShowIf("_addCameraShake")] [Tooltip("Camera shake intensity")]
        private float _cameraShakeIntensity = 0.5f;
        
        [SerializeField] [Tooltip("Add motion blur effect")]
        private bool _addMotionBlur = false;
        
        [SerializeField] [Tooltip("Add lens flare effect")]
        private bool _addLensFlare = false;
        
        [Title("Timing")]
        [SerializeField] [Tooltip("Zoom in duration")]
        private float _zoomInDuration = 0.8f;
        
        [SerializeField] [Tooltip("Zoom out duration")]
        private float _zoomOutDuration = 0.6f;
        
        [SerializeField] [Tooltip("Add dramatic pause")]
        private bool _addDramaticPause = true;
        
        [SerializeField] [ShowIf("_addDramaticPause")] [Tooltip("Pause duration")]
        private float _pauseDuration = 0.2f;
        
        [Title("Advanced")]
        [SerializeField] [Tooltip("Add secondary rotation")]
        private bool _addSecondaryRotation = false;
        
        [SerializeField] [ShowIf("_addSecondaryRotation")] [Tooltip("Secondary axis")]
        private RotationAxis _secondaryAxis = RotationAxis.Y;
        
        [SerializeField] [ShowIf("_addSecondaryRotation")] [Tooltip("Secondary rotation amount")]
        private float _secondaryRotation = 180f;
        
        [SerializeField] [Tooltip("Add scale pulsing")]
        private bool _addScalePulse = false;
        
        [SerializeField] [ShowIf("_addScalePulse")] [Tooltip("Pulse intensity")]
        private float _pulseIntensity = 0.1f;
        
        public enum RotationAxis
        {
            X,
            Y,
            Z
        }
        
        public enum RotationDirection
        {
            Clockwise,
            CounterClockwise,
            Alternating
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Set initial state
            target.localScale = _initialZoom;
            canvasGroup.alpha = 0f;
            
            // Set initial rotation
            var rotationAxis = GetRotationAxis();
            var entryRotation = rotationAxis * _entryRotation * GetRotationDirection();
            target.localEulerAngles = entryRotation;
            
            // Dramatic entrance
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Zoom in with rotation
            sequence.Append(target.DOScale(_zoomOvershoot, _zoomInDuration * 0.7f).SetEase(Ease.OutBack));
            sequence.Join(target.DOLocalRotate(Vector3.zero, _zoomInDuration * 0.7f).SetEase(Ease.OutBack));
            
            // Add secondary rotation
            if (_addSecondaryRotation)
            {
                var secondaryAxis = GetSecondaryAxis();
                var secondaryRot = secondaryAxis * _secondaryRotation * GetRotationDirection();
                sequence.Join(target.DOLocalRotate(secondaryRot, _zoomInDuration * 0.7f).SetEase(Ease.OutBack));
            }
            
            // Add rotation wobble
            if (_addRotationWobble)
            {
                sequence.AppendCallback(() =>
                {
                    target.DOShakeRotation(_zoomInDuration * 0.3f, new Vector3(0, 0, _wobbleIntensity), 10, 0, true);
                });
            }
            
            // Dramatic pause
            if (_addDramaticPause)
            {
                sequence.AppendInterval(_pauseDuration);
            }
            
            // Settle to final scale
            sequence.Append(target.DOScale(_targetZoom, _zoomInDuration * 0.3f).SetEase(Ease.OutBack));
            
            // Add scale pulse
            if (_addScalePulse)
            {
                var pulseScale = _targetZoom * (1f + _pulseIntensity);
                sequence.Append(target.DOScale(pulseScale, 0.2f).SetLoops(2, LoopType.Yoyo).SetEase(Ease.InOutSine));
            }
            
            // Camera shake effect
            if (_addCameraShake)
            {
                sequence.AppendCallback(() =>
                {
                    // This would need to be implemented in your camera system
                    // Camera.main.DOShakePosition(_zoomInDuration * 0.5f, _cameraShakeIntensity);
                    Debug.Log("Camera shake would be implemented here");
                });
            }
            
            // Motion blur effect
            if (_addMotionBlur)
            {
                sequence.AppendCallback(() =>
                {
                    // This would need to be implemented with post-processing
                    Debug.Log("Motion blur would be implemented here");
                });
            }
            
            // Lens flare effect
            if (_addLensFlare)
            {
                sequence.AppendCallback(() =>
                {
                    // This would need to be implemented with particle effects
                    Debug.Log("Lens flare would be implemented here");
                });
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            // Dramatic exit preparation
            if (_addDramaticPause)
            {
                sequence.AppendInterval(_pauseDuration * 0.5f);
            }
            
            // Quick zoom out with intense rotation
            var rotationAxis = GetRotationAxis();
            var exitRotation = rotationAxis * _exitRotation * GetRotationDirection();
            
            sequence.Append(target.DOScale(_zoomOvershoot, _zoomOutDuration * 0.3f).SetEase(Ease.InBack));
            sequence.Join(target.DOLocalRotate(exitRotation * 0.5f, _zoomOutDuration * 0.3f).SetEase(Ease.InBack));
            
            // Add secondary rotation
            if (_addSecondaryRotation)
            {
                var secondaryAxis = GetSecondaryAxis();
                var secondaryRot = secondaryAxis * _secondaryRotation * GetRotationDirection() * 2f;
                sequence.Join(target.DOLocalRotate(secondaryRot, _zoomOutDuration * 0.3f).SetEase(Ease.InBack));
            }
            
            // Final zoom out
            sequence.Append(target.DOScale(_initialZoom, _zoomOutDuration * 0.7f).SetEase(Ease.InBack));
            sequence.Join(target.DOLocalRotate(exitRotation, _zoomOutDuration * 0.7f).SetEase(Ease.InBack));
            
            // Fade out
            sequence.Join(canvasGroup.DOFade(0, _zoomOutDuration * 0.5f).SetEase(Ease.InQuad));
            
            // Camera shake on exit
            if (_addCameraShake)
            {
                sequence.AppendCallback(() =>
                {
                    // Camera.main.DOShakePosition(_zoomOutDuration * 0.3f, _cameraShakeIntensity * 2f);
                    Debug.Log("Exit camera shake would be implemented here");
                });
            }
            
            return sequence.Play();
        }

        private Vector3 GetRotationAxis()
        {
            return _rotationAxis switch
            {
                RotationAxis.X => Vector3.right,
                RotationAxis.Y => Vector3.up,
                RotationAxis.Z => Vector3.forward,
                _ => Vector3.forward
            };
        }

        private Vector3 GetSecondaryAxis()
        {
            return _secondaryAxis switch
            {
                RotationAxis.X => Vector3.right,
                RotationAxis.Y => Vector3.up,
                RotationAxis.Z => Vector3.forward,
                _ => Vector3.up
            };
        }

        private float GetRotationDirection()
        {
            return _rotationDirection switch
            {
                RotationDirection.Clockwise => 1f,
                RotationDirection.CounterClockwise => -1f,
                RotationDirection.Alternating => Random.value > 0.5f ? 1f : -1f,
                _ => 1f
            };
        }
    }
}