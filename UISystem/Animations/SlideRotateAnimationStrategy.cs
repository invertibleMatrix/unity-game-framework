using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "SlideRotateAnimation", menuName = "Gameplay/UI/Animations/Slide Rotate Animation")]
    public class SlideRotateAnimationStrategy : AnimationStrategy
    {
        [Title("Slide Settings")]
        [SerializeField] [Tooltip("Entry direction")]
        private UIUtility.SlideDirection _entryDirection = UIUtility.SlideDirection.FromLeft;
        
        [SerializeField] [Tooltip("Exit direction")]
        private UIUtility.SlideDirection _exitDirection = UIUtility.SlideDirection.FromRight;
        
        [SerializeField] [Tooltip("Override exit direction")]
        private bool _overrideExitDirection = false;
        
        [SerializeField] [Tooltip("Off-screen offset")]
        private Vector2 _edgesOffset = new Vector2(250, 250);
        
        [Title("Rotation Settings")]
        [SerializeField] [Tooltip("Rotation axis")]
        private RotationAxis _rotationAxis = RotationAxis.Z;
        
        [SerializeField] [Tooltip("Entry rotation amount")]
        private float _entryRotation = 360f;
        
        [SerializeField] [Tooltip("Exit rotation amount")]
        private float _exitRotation = -360f;
        
        [SerializeField] [Tooltip("Rotation direction")]
        private RotationDirection _rotationDirection = RotationDirection.Clockwise;
        
        [Title("Movement Pattern")]
        [SerializeField] [Tooltip("Movement pattern during slide")]
        private SlidePattern _slidePattern = SlidePattern.Straight;
        
        [SerializeField] [ShowIf("_slidePattern", SlidePattern.Curved)] [Tooltip("Curve intensity")]
        private float _curveIntensity = 100f;
        
        [SerializeField] [ShowIf("_slidePattern", SlidePattern.Zigzag)] [Tooltip("Zigzag amplitude")]
        private float _zigzagAmplitude = 50f;
        
        [SerializeField] [ShowIf("_slidePattern", SlidePattern.Wave)] [Tooltip("Wave frequency")]
        private float _waveFrequency = 2f;
        
        [Title("Scale Effects")]
        [SerializeField] [Tooltip("Add scale effect")]
        private bool _addScaleEffect = true;
        
        [SerializeField] [ShowIf("_addScaleEffect")] [Tooltip("Scale at entry")]
        private Vector3 _entryScale = new Vector3(0.8f, 0.8f, 0.8f);
        
        [SerializeField] [ShowIf("_addScaleEffect")] [Tooltip("Scale at exit")]
        private Vector3 _exitScale = new Vector3(1.2f, 1.2f, 1.2f);
        
        [Title("Timing")]
        [SerializeField] [Tooltip("Rotation speed multiplier")]
        private float _rotationSpeedMultiplier = 1f;
        
        [SerializeField] [Tooltip("Add anticipation before entry")]
        private bool _addAnticipation = true;
        
        [SerializeField] [ShowIf("_addAnticipation")] [Tooltip("Anticipation distance")]
        private float _anticipationDistance = 30f;
        
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
        
        public enum SlidePattern
        {
            Straight,
            Curved,
            Zigzag,
            Wave,
            Spiral
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Set initial state
            canvasGroup.alpha = 1f;
            target.localScale = _addScaleEffect ? _entryScale : Vector3.one;
            
            // Calculate start position
            var startPos = UIUtility.GetOffScreenPosition(target, _entryDirection, _edgesOffset);
            
            // Add anticipation if enabled
            if (_addAnticipation)
            {
                var anticipationPos = startPos;
                switch (_entryDirection)
                {
                    case UIUtility.SlideDirection.FromLeft:
                        anticipationPos.x += _anticipationDistance;
                        break;
                    case UIUtility.SlideDirection.FromRight:
                        anticipationPos.x -= _anticipationDistance;
                        break;
                    case UIUtility.SlideDirection.FromTop:
                        anticipationPos.y -= _anticipationDistance;
                        break;
                    case UIUtility.SlideDirection.FromBottom:
                        anticipationPos.y += _anticipationDistance;
                        break;
                }
                
                target.anchoredPosition = anticipationPos;
                sequence.Append(target.DOAnchorPos(startPos, EntryDuration * 0.2f).SetEase(Ease.InQuad));
            }
            else
            {
                target.anchoredPosition = startPos;
            }
            
            // Set initial rotation
            var rotationAxis = GetRotationAxis();
            var entryRotation = rotationAxis * _entryRotation * GetRotationDirection();
            target.localEulerAngles = entryRotation;
            
            // Create slide path
            var slidePath = GetSlidePath(startPos, Vector2.zero, true);
            
            // Animate along path with rotation
            sequence.Append(target.DOPath(slidePath, EntryDuration * 0.8f, PathType.CatmullRom).SetEase(EntryEase));
            
            // Rotate during slide
            var rotationDuration = EntryDuration * 0.8f / _rotationSpeedMultiplier;
            sequence.Join(target.DOLocalRotate(Vector3.zero, rotationDuration).SetEase(EntryEase));
            
            // Scale effect
            if (_addScaleEffect)
            {
                sequence.Join(target.DOScale(Vector3.one, EntryDuration * 0.8f).SetEase(EntryEase));
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            // Calculate exit direction
            var exitDir = _overrideExitDirection ? _exitDirection : UIUtility.GetOppositeDirection(_entryDirection);
            var endPos = UIUtility.GetOffScreenPosition(target, exitDir, _edgesOffset);
            
            // Create slide path
            var slidePath = GetSlidePath(Vector2.zero, endPos, false);
            
            // Set final rotation
            var rotationAxis = GetRotationAxis();
            var exitRotation = rotationAxis * _exitRotation * GetRotationDirection();
            
            // Animate along path with rotation
            sequence.Append(target.DOPath(slidePath, ExitDuration * 0.8f, PathType.CatmullRom).SetEase(ExitEase));
            
            // Rotate during slide
            var rotationDuration = ExitDuration * 0.8f / _rotationSpeedMultiplier;
            sequence.Join(target.DOLocalRotate(exitRotation, rotationDuration).SetEase(ExitEase));
            
            // Scale effect
            if (_addScaleEffect)
            {
                sequence.Join(target.DOScale(_exitScale, ExitDuration * 0.8f).SetEase(ExitEase));
            }
            
            // Fade out
            sequence.Join(canvasGroup.DOFade(0, ExitDuration * 0.5f).SetEase(ExitEase));
            
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

        private Vector3[] GetSlidePath(Vector2 start, Vector2 end, bool isEntry)
        {
            var points = new Vector3[5];
            points[0] = start;
            points[4] = end;
            
            var midPoint = (start + end) * 0.5f;
            
            switch (_slidePattern)
            {
                case SlidePattern.Straight:
                    points[1] = Vector2.Lerp(start, end, 0.25f);
                    points[2] = Vector2.Lerp(start, end, 0.5f);
                    points[3] = Vector2.Lerp(start, end, 0.75f);
                    break;
                    
                case SlidePattern.Curved:
                    var perpendicular = new Vector2(-(end.y - start.y), end.x - start.x).normalized;
                    points[1] = Vector2.Lerp(start, midPoint, 0.5f) + perpendicular * _curveIntensity * 0.5f;
                    points[2] = midPoint + perpendicular * _curveIntensity;
                    points[3] = Vector2.Lerp(midPoint, end, 0.5f) + perpendicular * _curveIntensity * 0.5f;
                    break;
                    
                case SlidePattern.Zigzag:
                    var direction = (end - start).normalized;
                    var cross = new Vector2(-direction.y, direction.x);
                    points[1] = Vector2.Lerp(start, end, 0.25f) + cross * _zigzagAmplitude;
                    points[2] = Vector2.Lerp(start, end, 0.5f) - cross * _zigzagAmplitude;
                    points[3] = Vector2.Lerp(start, end, 0.75f) + cross * _zigzagAmplitude;
                    break;
                    
                case SlidePattern.Wave:
                    for (int i = 1; i <= 3; i++)
                    {
                        var t = i * 0.25f;
                        var waveOffset = Mathf.Sin(t * Mathf.PI * 2 * _waveFrequency) * _zigzagAmplitude;
                        var perpendicular2 = new Vector2(-(end.y - start.y), end.x - start.x).normalized;
                        points[i] = Vector2.Lerp(start, end, t) + perpendicular2 * waveOffset;
                    }
                    break;
                    
                case SlidePattern.Spiral:
                    var center = (start + end) * 0.5f;
                    for (int i = 1; i <= 3; i++)
                    {
                        var t = i * 0.25f;
                        var angle = t * Mathf.PI * 2;
                        var radius = Mathf.Lerp(0, _curveIntensity, t);
                        points[i] = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                    }
                    break;
            }
            
            return points;
        }
    }
}