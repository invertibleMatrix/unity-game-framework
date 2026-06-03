using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "CardDealAnimation", menuName = "AK/UI/Animations/Card Deal Animation")]
    public class CardDealAnimationStrategy : AnimationStrategy
    {
        [Title("Spawn Settings")]
        [SerializeField] [Tooltip("Where the card spawns from (relative to screen center)")]
        private Vector2 _spawnOffset = new Vector2(0, 300);
        
        [SerializeField] [Tooltip("Initial scale when spawning")]
        private Vector3 _startScale = Vector3.zero;
        
        [Title("Movement Settings")]
        [SerializeField] [Tooltip("Add slight arc to movement")]
        private bool _addArc = true;
        
        [ShowIf("_addArc")] [SerializeField] [Tooltip("Arc height")]
        private float _arcHeight = 50f;
        
        [Title("Rotation Settings")]
        [SerializeField] [Tooltip("Add rotation during deal")]
        private bool _addRotation = true;
        
        [ShowIf("_addRotation")] [SerializeField] [Tooltip("Rotation amount")]
        private float _rotationAmount = 15f;
        
        [ShowIf("_addRotation")] [SerializeField] [Tooltip("Randomize rotation direction")]
        private bool _randomizeRotation = true;
        
        [Title("Timing")]
        [SerializeField] [Tooltip("Deal duration")]
        private float _dealDuration = 0.4f;
        
        [SerializeField] [Tooltip("Scale overshoot at end")]
        private float _scaleOvershoot = 1.1f;
        
        [Title("Settle")]
        [SerializeField] [Tooltip("Add small bounce at end")]
        private bool _addSettleBounce = true;
        
        [ShowIf("_addSettleBounce")] [SerializeField] [Tooltip("Bounce intensity")]
        private float _bounceIntensity = 0.05f;

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Set initial state - spawn from offset relative to current position
            target.anchoredPosition = _spawnOffset;
            target.localScale = _startScale;
            target.localEulerAngles = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Quick fade in
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Calculate rotation
            var rotation = _addRotation ? _rotationAmount : 0f;
            if (_randomizeRotation && _addRotation)
            {
                rotation *= Random.value > 0.5f ? 1f : -1f;
            }
            
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
            
            // Scale up with overshoot
            sequence.Join(target.DOScale(Vector3.one * _scaleOvershoot, _dealDuration * 0.8f).SetEase(Ease.OutBack));
            
            // Add rotation
            if (_addRotation)
            {
                sequence.Join(target.DOLocalRotate(new Vector3(0, 0, rotation), _dealDuration).SetEase(Ease.OutCubic));
            }
            
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
            sequence.Append(target.DOLocalRotate(new Vector3(0, 0, 180), _dealDuration * 0.3f).SetEase(Ease.InBack));
            sequence.Join(target.DOScale(Vector3.zero, _dealDuration * 0.3f).SetEase(Ease.InBack));
            sequence.Join(canvasGroup.DOFade(0, _dealDuration * 0.3f).SetEase(Ease.InQuad));
            
            return sequence.Play();
        }
    }
}