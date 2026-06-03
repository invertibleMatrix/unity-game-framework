using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "CardFlipDealAnimation", menuName = "AK/UI/Animations/Card Flip Deal Animation")]
    public class CardFlipDealAnimationStrategy : AnimationStrategy
    {
        [Title("Spawn Settings")]
        [SerializeField] [Tooltip("Where the card spawns from")]
        private Vector2 _spawnOffset = new Vector2(0, 300);
        
        [SerializeField] [Tooltip("Initial scale")]
        private Vector3 _startScale = Vector3.zero;
        
        [Title("Movement Settings")]
        [SerializeField] [Tooltip("Add arc to movement")]
        private bool _addArc = true;
        
        [ShowIf("_addArc")] [SerializeField] [Tooltip("Arc height")]
        private float _arcHeight = 80f;
        
        [Title("Flip Settings")]
        [SerializeField] [Tooltip("Number of flips during deal")]
        private int _flipCount = 1;
        
        [SerializeField] [Tooltip("Flip axis")]
        private FlipAxis _flipAxis = FlipAxis.Y;
        
        [SerializeField] [Tooltip("Flip speed")]
        private float _flipSpeed = 1f;
        
        [Title("Scale Settings")]
        [SerializeField] [Tooltip("Scale during flip (3D effect)")]
        private Vector3 _flipScale = new Vector3(0.1f, 1f, 1f);
        
        [SerializeField] [Tooltip("Scale overshoot at end")]
        private float _scaleOvershoot = 1.1f;
        
        [Title("Timing")]
        [SerializeField] [Tooltip("Deal duration")]
        private float _dealDuration = 0.5f;
        
        [Title("Settle")]
        [SerializeField] [Tooltip("Add settle bounce")]
        private bool _addSettleBounce = true;
        
        [ShowIf("_addSettleBounce")] [SerializeField] [Tooltip("Bounce intensity")]
        private float _bounceIntensity = 0.05f;

        public enum FlipAxis
        {
            X,
            Y
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Set initial state - spawn from offset relative to current position
            target.anchoredPosition = _spawnOffset;
            target.localScale = _startScale;
            target.localEulerAngles = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Fade in
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Create movement path with arc - target is Vector2.zero (current position)
            if (_addArc)
            {
                var midPoint = (_spawnOffset + Vector2.zero) * 0.5f;
                midPoint.y += _arcHeight;
                
                var path = new Vector3[] { _spawnOffset, midPoint, Vector2.zero };
                sequence.Append(target.DOPath(path, _dealDuration, PathType.CatmullRom).SetEase(Ease.OutCubic));
            }
            else
            {
                sequence.Append(target.DOAnchorPos(Vector2.zero, _dealDuration).SetEase(Ease.OutCubic));
            }
            
            // Scale up
            sequence.Join(target.DOScale(Vector3.one, _dealDuration * 0.8f).SetEase(Ease.OutBack));
            
            // Add flips
            var flipAxis = GetFlipAxis();
            var totalRotation = 180f * _flipCount;
            
            for (int i = 0; i < _flipCount; i++)
            {
                var flipDuration = _dealDuration / _flipCount;
                var flipStart = i * flipDuration;
                
                // First half of flip (to 90 degrees)
                sequence.Insert(flipStart, target.DOLocalRotate(flipAxis * 90f, flipDuration * 0.5f).SetEase(Ease.InQuad));
                sequence.Join(target.DOScale(_flipScale, flipDuration * 0.5f).SetEase(Ease.InQuad));
                
                // Second half of flip (to 180 degrees)
                sequence.Insert(flipStart + flipDuration * 0.5f, target.DOLocalRotate(flipAxis * 180f, flipDuration * 0.5f).SetEase(Ease.OutQuad));
                sequence.Join(target.DOScale(Vector3.one, flipDuration * 0.5f).SetEase(Ease.OutQuad));
            }
            
            // Final rotation to zero
            sequence.Append(target.DOLocalRotate(Vector3.zero, 0.1f).SetEase(Ease.OutBack));
            
            // Settle bounce
            if (_addSettleBounce)
            {
                sequence.Append(target.DOScale(Vector3.one * (1f + _bounceIntensity), 0.1f).SetEase(Ease.OutBack));
                sequence.Append(target.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutBack));
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            // Quick flip and shrink
            var flipAxis = GetFlipAxis();
            
            sequence.Append(target.DOLocalRotate(flipAxis * 180, _dealDuration * 0.3f).SetEase(Ease.InBack));
            sequence.Join(target.DOScale(_flipScale, _dealDuration * 0.3f).SetEase(Ease.InBack));
            sequence.Join(canvasGroup.DOFade(0, _dealDuration * 0.3f).SetEase(Ease.InQuad));
            
            // Complete flip and disappear
            sequence.Append(target.DOLocalRotate(flipAxis * 360, _dealDuration * 0.3f).SetEase(Ease.OutBack));
            sequence.Join(target.DOScale(Vector3.zero, _dealDuration * 0.3f).SetEase(Ease.OutBack));
            
            return sequence.Play();
        }

        private Vector3 GetFlipAxis()
        {
            return _flipAxis switch
            {
                FlipAxis.X => Vector3.right,
                FlipAxis.Y => Vector3.up,
                _ => Vector3.up
            };
        }
    }
}