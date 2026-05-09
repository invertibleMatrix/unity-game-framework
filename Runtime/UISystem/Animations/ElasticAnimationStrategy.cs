using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "ElasticAnimation", menuName = "Gameplay/UI/Animations/Elastic Animation")]
    public class ElasticAnimationStrategy : AnimationStrategy
    {
        [Title("Elastic Settings")]
        [SerializeField] [Tooltip("Stretch amount on entry")]
        private Vector3 _entryStretch = new Vector3(0.3f, 1.5f, 1f);
        
        [SerializeField] [Tooltip("Stretch amount on exit")]
        private Vector3 _exitStretch = new Vector3(2f, 0.3f, 1f);
        
        [SerializeField] [Tooltip("Elasticity factor (higher = more bouncy)")]
        private float _elasticity = 1f;
        
        [Title("Movement Settings")]
        [SerializeField] [Tooltip("Add elastic movement")]
        private bool _addMovement = true;
        
        [SerializeField] [ShowIf("_addMovement")] [Tooltip("Movement distance")]
        private Vector2 _movementDistance = new Vector2(50, 30);
        
        [SerializeField] [ShowIf("_addMovement")] [Tooltip("Movement pattern")]
        private ElasticMovementPattern _movementPattern = ElasticMovementPattern.Spiral;
        
        [Title("Rotation Settings")]
        [SerializeField] [Tooltip("Add elastic rotation")]
        private bool _addRotation = true;
        
        [SerializeField] [ShowIf("_addRotation")] [Tooltip("Rotation amount")]
        private Vector3 _rotationAmount = new Vector3(0, 0, 15f);
        
        [Title("Overshoot Settings")]
        [SerializeField] [Tooltip("Scale overshoot amount")]
        private Vector3 _scaleOvershoot = new Vector3(1.3f, 1.3f, 1.3f);
        
        [SerializeField] [Tooltip("Number of elastic oscillations")]
        private int _oscillations = 3;
        
        [Title("Effects")]
        [SerializeField] [Tooltip("Add wobble effect")]
        private bool _addWobble = true;
        
        [SerializeField] [ShowIf("_addWobble")] [Tooltip("Wobble intensity")]
        private float _wobbleIntensity = 5f;
        
        [SerializeField] [Tooltip("Add squash and stretch")]
        private bool _addSquashStretch = true;
        
        [SerializeField] [ShowIf("_addSquashStretch")] [Tooltip("Squash factor")]
        private float _squashFactor = 0.7f;
        
        public enum ElasticMovementPattern
        {
            Linear,
            Circular,
            Spiral,
            Figure8,
            Random
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Set initial state
            target.localScale = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Initial stretch in
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            sequence.Append(target.DOScale(_entryStretch, EntryDuration * 0.3f).SetEase(Ease.OutBack));
            
            // Add movement if enabled
            if (_addMovement)
            {
                var movementPath = GetMovementPath(Vector2.zero, _movementDistance);
                sequence.Append(target.DOPath(movementPath, EntryDuration * 0.7f, PathType.CatmullRom).SetEase(Ease.OutElastic));
            }
            
            // Elastic scale to normal
            sequence.Join(target.DOScale(_scaleOvershoot, EntryDuration * 0.5f).SetEase(Ease.OutElastic));
            sequence.Append(target.DOScale(Vector3.one, EntryDuration * 0.5f).SetEase(Ease.OutElastic));
            
            // Add rotation if enabled
            if (_addRotation)
            {
                sequence.Join(target.DOLocalRotate(_rotationAmount, EntryDuration * 0.6f).SetEase(Ease.OutElastic));
                sequence.Append(target.DOLocalRotate(Vector3.zero, EntryDuration * 0.4f).SetEase(Ease.OutElastic));
            }
            
            // Add wobble effect
            if (_addWobble)
            {
                sequence.AppendCallback(() =>
                {
                    target.DOShakeRotation(EntryDuration * 0.3f, new Vector3(0, 0, _wobbleIntensity), 10, 0, true);
                });
            }
            
            // Add squash and stretch
            if (_addSquashStretch)
            {
                sequence.Append(target.DOScale(new Vector3(1f, _squashFactor, 1f), 0.1f).SetEase(Ease.OutQuad));
                sequence.Append(target.DOScale(new Vector3(_squashFactor, 1f, 1f), 0.1f).SetEase(Ease.InQuad));
                sequence.Append(target.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutBack));
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            // Stretch out
            sequence.Append(target.DOScale(_exitStretch, ExitDuration * 0.4f).SetEase(Ease.InBack));
            sequence.Join(canvasGroup.DOFade(0, ExitDuration * 0.4f).SetEase(Ease.InQuad));
            
            // Add elastic movement out
            if (_addMovement)
            {
                var exitPath = GetMovementPath(target.anchoredPosition, _movementDistance * 2f);
                sequence.Append(target.DOPath(exitPath, ExitDuration * 0.6f, PathType.CatmullRom).SetEase(Ease.InBack));
            }
            
            // Add rotation out
            if (_addRotation)
            {
                sequence.Join(target.DOLocalRotate(-_rotationAmount * 2f, ExitDuration * 0.6f).SetEase(Ease.InBack));
            }
            
            // Final collapse
            sequence.Append(target.DOScale(Vector3.zero, ExitDuration * 0.3f).SetEase(Ease.InBack));
            
            return sequence.Play();
        }

        private Vector3[] GetMovementPath(Vector2 start, Vector2 distance)
        {
            var points = new Vector3[8];
            points[0] = start;
            
            switch (_movementPattern)
            {
                case ElasticMovementPattern.Linear:
                    for (int i = 1; i < points.Length; i++)
                    {
                        var t = (float)i / (points.Length - 1);
                        points[i] = Vector2.Lerp(start, start + distance, t);
                    }
                    break;
                    
                case ElasticMovementPattern.Circular:
                    for (int i = 1; i < points.Length; i++)
                    {
                        var angle = (float)i / (points.Length - 1) * Mathf.PI * 2;
                        var radius = distance.magnitude * 0.5f;
                        points[i] = start + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                    }
                    break;
                    
                case ElasticMovementPattern.Spiral:
                    for (int i = 1; i < points.Length; i++)
                    {
                        var t = (float)i / (points.Length - 1);
                        var angle = t * Mathf.PI * 4;
                        var radius = distance.magnitude * t;
                        points[i] = start + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                    }
                    break;
                    
                case ElasticMovementPattern.Figure8:
                    for (int i = 1; i < points.Length; i++)
                    {
                        var t = (float)i / (points.Length - 1);
                        var angle = t * Mathf.PI * 2;
                        points[i] = start + new Vector2(Mathf.Sin(angle) * distance.x, Mathf.Sin(angle * 2) * distance.y * 0.5f);
                    }
                    break;
                    
                case ElasticMovementPattern.Random:
                    for (int i = 1; i < points.Length; i++)
                    {
                        var randomOffset = new Vector2(
                            Random.Range(-distance.x, distance.x),
                            Random.Range(-distance.y, distance.y)
                        );
                        points[i] = start + randomOffset * ((float)i / (points.Length - 1));
                    }
                    break;
            }
            
            return points;
        }
    }
}