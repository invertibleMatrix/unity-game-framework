using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "SpiralAnimation", menuName = "AK/UI/Animations/Spiral Animation")]
    public class SpiralAnimationStrategy : AnimationStrategy
    {
        [Title("Spiral Settings")]
        [SerializeField] [Tooltip("Number of spiral rotations")]
        private int _spiralRotations = 3;
        
        [SerializeField] [Tooltip("Spiral radius")]
        private float _spiralRadius = 150f;
        
        [SerializeField] [Tooltip("Spiral direction")]
        private SpiralDirection _spiralDirection = SpiralDirection.Outward;
        
        [Title("Scale Settings")]
        [SerializeField] [Tooltip("Start scale")]
        private Vector3 _startScale = Vector3.zero;
        
        [SerializeField] [Tooltip("End scale")]
        private Vector3 _endScale = Vector3.one;
        
        [SerializeField] [Tooltip("Add scale pulsing during spiral")]
        private bool _addScalePulse = true;
        
        [SerializeField] [ShowIf("_addScalePulse")] [Tooltip("Pulse intensity")]
        private float _pulseIntensity = 0.2f;
        
        [Title("Rotation Settings")]
        [SerializeField] [Tooltip("Add local rotation during spiral")]
        private bool _addLocalRotation = true;
        
        [SerializeField] [ShowIf("_addLocalRotation")] [Tooltip("Local rotation amount")]
        private Vector3 _localRotation = new Vector3(0, 0, 360f);
        
        [SerializeField] [Tooltip("Rotation direction")]
        private RotationDirection _rotationDirection = RotationDirection.Clockwise;
        
        [Title("Fade Settings")]
        [SerializeField] [Tooltip("Fade during spiral")]
        private bool _addFade = true;
        
        [SerializeField] [ShowIf("_addFade")] [Tooltip("Fade curve")]
        private FadeCurve _fadeCurve = FadeCurve.InOut;
        
        [Title("Effects")]
        [SerializeField] [Tooltip("Add trail effect")]
        private bool _addTrailEffect = false;
        
        [SerializeField] [ShowIf("_addTrailEffect")] [Tooltip("Trail intensity")]
        private float _trailIntensity = 0.5f;
        
        [SerializeField] [Tooltip("Add color shift")]
        private bool _addColorShift = false;
        
        [SerializeField] [ShowIf("_addColorShift")] [Tooltip("Color shift speed")]
        private float _colorShiftSpeed = 2f;
        
        [Title("Exit Settings")]
        [SerializeField] [Tooltip("Reverse spiral on exit")]
        private bool _reverseOnExit = true;
        
        [SerializeField] [Tooltip("Exit spiral speed multiplier")]
        private float _exitSpeedMultiplier = 1.5f;
        
        public enum SpiralDirection
        {
            Inward,
            Outward
        }
        
        public enum RotationDirection
        {
            Clockwise,
            CounterClockwise
        }
        
        public enum FadeCurve
        {
            Linear,
            InOut,
            EarlyIn,
            LateOut
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            // Set initial state
            target.localScale = _startScale;
            canvasGroup.alpha = 0f;
            
            // Calculate spiral path
            var spiralPath = GetSpiralPath(target.anchoredPosition, _spiralRadius, _spiralRotations, _spiralDirection == SpiralDirection.Outward);
            
            // Set initial position
            target.anchoredPosition = spiralPath[0];
            
            // Start animation
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Animate along spiral path
            sequence.Append(target.DOPath(spiralPath, EntryDuration, PathType.CatmullRom).SetEase(EntryEase));
            
            // Scale animation
            sequence.Join(target.DOScale(_endScale, EntryDuration).SetEase(EntryEase));
            
            // Add scale pulsing
            if (_addScalePulse)
            {
                var pulseScale = _endScale * (1f + _pulseIntensity);
                sequence.Join(target.DOScale(pulseScale, EntryDuration * 0.1f).SetLoops(10, LoopType.Yoyo).SetEase(Ease.InOutSine));
            }
            
            // Add local rotation
            if (_addLocalRotation)
            {
                var rotationAmount = _localRotation * GetRotationDirection();
                sequence.Join(target.DOLocalRotate(rotationAmount, EntryDuration).SetEase(EntryEase));
            }
            
            // Add fade
            if (_addFade)
            {
                var fadeEase = GetFadeEase();
                sequence.Join(canvasGroup.DOFade(1f, EntryDuration).SetEase(fadeEase));
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            // Kill any existing tweens on this target to prevent memory leaks
            target.DOKill();
            
            var sequence = DOTween.Sequence();
            
            var exitDuration = ExitDuration * _exitSpeedMultiplier;
            
            if (_reverseOnExit)
            {
                // Reverse spiral
                var spiralPath = GetSpiralPath(target.anchoredPosition, _spiralRadius, _spiralRotations, _spiralDirection == SpiralDirection.Inward);
                
                // Animate along reverse spiral path
                sequence.Append(target.DOPath(spiralPath, exitDuration, PathType.CatmullRom).SetEase(ExitEase));
                
                // Scale animation
                sequence.Join(target.DOScale(_startScale, exitDuration).SetEase(ExitEase));
                
                // Add local rotation
                if (_addLocalRotation)
                {
                    var rotationAmount = _localRotation * GetRotationDirection() * -1f;
                    sequence.Join(target.DOLocalRotate(rotationAmount, exitDuration).SetEase(ExitEase));
                }
            }
            else
            {
                // Collapse spiral
                var collapsePath = GetCollapseSpiralPath(target.anchoredPosition, _spiralRadius, _spiralRotations);
                
                sequence.Append(target.DOPath(collapsePath, exitDuration, PathType.CatmullRom).SetEase(ExitEase));
                sequence.Join(target.DOScale(_startScale, exitDuration).SetEase(ExitEase));
                
                if (_addLocalRotation)
                {
                    var rotationAmount = _localRotation * GetRotationDirection() * 2f;
                    sequence.Join(target.DOLocalRotate(rotationAmount, exitDuration).SetEase(ExitEase));
                }
            }
            
            // Fade out
            if (_addFade)
            {
                var fadeEase = GetFadeEase();
                sequence.Join(canvasGroup.DOFade(0, exitDuration).SetEase(fadeEase));
            }
            
            return sequence.Play();
        }

        private Vector3[] GetSpiralPath(Vector2 center, float radius, int rotations, bool outward)
        {
            var points = new Vector3[rotations * 8 + 1];
            var direction = outward ? 1f : -1f;
            
            for (int i = 0; i < points.Length; i++)
            {
                var t = (float)i / (points.Length - 1);
                var angle = t * rotations * 2f * Mathf.PI * direction;
                var currentRadius = t * radius;
                
                points[i] = center + new Vector2(
                    Mathf.Cos(angle) * currentRadius,
                    Mathf.Sin(angle) * currentRadius
                );
            }
            
            return points;
        }

        private Vector3[] GetCollapseSpiralPath(Vector2 center, float radius, int rotations)
        {
            var points = new Vector3[rotations * 4 + 1];
            
            for (int i = 0; i < points.Length; i++)
            {
                var t = (float)i / (points.Length - 1);
                var angle = t * rotations * 4f * Mathf.PI;
                var currentRadius = radius * (1f - t);
                
                points[i] = center + new Vector2(
                    Mathf.Cos(angle) * currentRadius,
                    Mathf.Sin(angle) * currentRadius
                );
            }
            
            return points;
        }

        private float GetRotationDirection()
        {
            return _rotationDirection switch
            {
                RotationDirection.Clockwise => 1f,
                RotationDirection.CounterClockwise => -1f,
                _ => 1f
            };
        }

        private Ease GetFadeEase()
        {
            return _fadeCurve switch
            {
                FadeCurve.Linear => Ease.Linear,
                FadeCurve.InOut => Ease.InOutQuad,
                FadeCurve.EarlyIn => Ease.InQuad,
                FadeCurve.LateOut => Ease.OutQuad,
                _ => Ease.InOutQuad
            };
        }
    }
}