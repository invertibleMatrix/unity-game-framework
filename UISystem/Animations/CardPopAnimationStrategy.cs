using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "CardPopAnimation", menuName = "Gameplay/UI/Animations/Card Pop Animation")]
    public class CardPopAnimationStrategy : AnimationStrategy
    {
        [Title("Pop Settings")]
        [SerializeField] [Tooltip("Pop scale multiplier")]
        private float _popScaleMultiplier = 1.3f;
        
        [SerializeField] [Tooltip("Pop speed")]
        private float _popSpeed = 0.2f;
        
        [SerializeField] [Tooltip("Pop ease")]
        private Ease _popEase = Ease.OutBack;
        
        [Title("Movement Settings")]
        [SerializeField] [Tooltip("Add movement during pop")]
        private bool _addMovement = true;
        
        [ShowIf("_addMovement")] [SerializeField] [Tooltip("Movement distance")]
        private float _movementDistance = 30f;
        
        [ShowIf("_addMovement")] [SerializeField] [Tooltip("Movement direction")]
        private PopDirection _popDirection = PopDirection.Up;
        
        [Title("Rotation Settings")]
        [SerializeField] [Tooltip("Add rotation during pop")]
        private bool _addRotation = true;
        
        [ShowIf("_addRotation")] [SerializeField] [Tooltip("Rotation amount")]
        private float _rotationAmount = 10f;
        
        [ShowIf("_addRotation")] [SerializeField] [Tooltip("Randomize rotation direction")]
        private bool _randomizeRotation = true;
        
        [Title("Multiple Pops")]
        [SerializeField] [Tooltip("Number of pops")]
        private int _popCount = 1;
        
        [SerializeField] [Tooltip("Delay between pops")]
        private float _popDelay = 0.1f;
        
        [Title("Settle")]
        [SerializeField] [Tooltip("Add settle wobble")]
        private bool _addSettleWobble = true;
        
        [ShowIf("_addSettleWobble")] [SerializeField] [Tooltip("Wobble intensity")]
        private float _wobbleIntensity = 3f;
        
        [ShowIf("_addSettleWobble")] [SerializeField] [Tooltip("Wobble duration")]
        private float _wobbleDuration = 0.3f;

        public enum PopDirection
        {
            Up,
            Down,
            Left,
            Right,
            Random
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Set initial state - card stays at its current position (Vector2.zero)
            target.anchoredPosition = Vector2.zero;
            target.localScale = Vector3.zero;
            target.localEulerAngles = Vector3.zero;
            canvasGroup.alpha = 0f;
            
            // Fade in
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            
            // Calculate movement offset (relative to current position)
            var movementOffset = Vector2.zero;
            if (_addMovement)
            {
                movementOffset = GetMovementOffset();
            }
            
            // Calculate rotation
            var rotation = 0f;
            if (_addRotation)
            {
                rotation = _rotationAmount;
                if (_randomizeRotation)
                {
                    rotation *= Random.value > 0.5f ? 1f : -1f;
                }
            }
            
            // Execute pops
            for (int i = 0; i < _popCount; i++)
            {
                if (i > 0)
                {
                    sequence.AppendInterval(_popDelay);
                }
                
                // Pop up
                sequence.Append(target.DOScale(Vector3.one * _popScaleMultiplier, _popSpeed).SetEase(_popEase));
                
                // Add movement
                if (_addMovement)
                {
                    sequence.Join(target.DOAnchorPos(movementOffset, _popSpeed).SetEase(_popEase));
                }
                
                // Add rotation
                if (_addRotation)
                {
                    sequence.Join(target.DOLocalRotate(new Vector3(0, 0, rotation), _popSpeed).SetEase(_popEase));
                }
                
                // Pop back down
                sequence.Append(target.DOScale(Vector3.one, _popSpeed * 0.5f).SetEase(Ease.InBack));
                
                // Return to position
                if (_addMovement)
                {
                    sequence.Join(target.DOAnchorPos(Vector2.zero, _popSpeed * 0.5f).SetEase(Ease.InBack));
                }
                
                // Return to rotation
                if (_addRotation)
                {
                    sequence.Join(target.DOLocalRotate(Vector3.zero, _popSpeed * 0.5f).SetEase(Ease.InBack));
                }
            }
            
            // Settle wobble
            if (_addSettleWobble)
            {
                sequence.Append(target.DOShakeRotation(_wobbleDuration, new Vector3(0, 0, _wobbleIntensity), 10, 0, true));
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            // Quick pop before disappearing
            sequence.Append(target.DOScale(Vector3.one * _popScaleMultiplier, _popSpeed * 0.3f).SetEase(Ease.OutBack));
            
            // Shrink and fade
            sequence.Append(target.DOScale(Vector3.zero, _popSpeed * 0.4f).SetEase(Ease.InBack));
            sequence.Join(canvasGroup.DOFade(0, _popSpeed * 0.4f).SetEase(Ease.InQuad));
            
            return sequence.Play();
        }

        private Vector2 GetMovementOffset()
        {
            return _popDirection switch
            {
                PopDirection.Up => Vector2.up * _movementDistance,
                PopDirection.Down => Vector2.down * _movementDistance,
                PopDirection.Left => Vector2.left * _movementDistance,
                PopDirection.Right => Vector2.right * _movementDistance,
                PopDirection.Random => new Vector2(
                    Random.Range(-_movementDistance, _movementDistance),
                    Random.Range(-_movementDistance, _movementDistance)
                ),
                _ => Vector2.zero
            };
        }
    }
}