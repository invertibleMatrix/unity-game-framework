using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem.Animations
{
    [CreateAssetMenu(fileName = "FlipAnimation", menuName = "Gameplay/UI/Animations/Flip Animation")]
    public class FlipAnimationStrategy : AnimationStrategy
    {
        [Title("Flip Settings")]
        [SerializeField] [Tooltip("Flip direction")]
        private FlipDirection _flipDirection = FlipDirection.Y;
        
        [SerializeField] [Tooltip("Number of flips")]
        private int _flipCount = 1;
        
        [SerializeField] [Tooltip("Flip angle per flip")]
        private float _flipAngle = 180f;
        
        [Title("Scale Settings")]
        [SerializeField] [Tooltip("Scale during flip (makes it look 3D)")]
        private Vector3 _flipScale = new Vector3(0.1f, 1f, 1f);
        
        [SerializeField] [Tooltip("Add bounce at the end")]
        private bool _addBounce = true;
        
        [SerializeField] [ShowIf("_addBounce")] [Tooltip("Bounce scale")]
        private Vector3 _bounceScale = new Vector3(1.1f, 1.1f, 1.1f);
        
        [Title("Timing Settings")]
        [SerializeField] [Tooltip("Duration per flip")]
        private float _flipDuration = 0.6f;
        
        [SerializeField] [Tooltip("Pause between flips")]
        private float _pauseBetweenFlips = 0.1f;
        
        [Title("Effects")]
        [SerializeField] [Tooltip("Add fade during flip")]
        private bool _addFade = true;
        
        [SerializeField] [ShowIf("_addFade")] [Tooltip("Fade amount at flip midpoint")]
        private float _midpointFade = 0.3f;
        
        [SerializeField] [Tooltip("Add shadow effect")]
        private bool _addShadowEffect = false;
        
        [SerializeField] [ShowIf("_addShadowEffect")] [Tooltip("Shadow scale")]
        private Vector3 _shadowScale = new Vector3(1.2f, 0.8f, 1f);
        
        [Title("Entry/Exit")]
        [SerializeField] [Tooltip("Start from off-screen")]
        private bool _startFromOffScreen = false;
        
        [SerializeField] [ShowIf("_startFromOffScreen")] [Tooltip("Entry direction")]
        private UIUtility.SlideDirection _entryDirection = UIUtility.SlideDirection.FromLeft;
        
        [SerializeField] [Tooltip("Exit to off-screen")]
        private bool _exitToOffScreen = false;
        
        [SerializeField] [ShowIf("_exitToOffScreen")] [Tooltip("Exit direction")]
        private UIUtility.SlideDirection _exitDirection = UIUtility.SlideDirection.FromRight;
        
        [SerializeField] [Tooltip("Off-screen offset")]
        private Vector2 _offScreenOffset = new Vector2(250, 250);

        public enum FlipDirection
        {
            X,
            Y,
            Both
        }

        public override Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, Vector2 entryPos = default)
        {
            var sequence = DOTween.Sequence();
            
            // Set initial state
            canvasGroup.alpha = 0f;
            target.localScale = Vector3.zero;
            
            // Start from off-screen if enabled
            if (_startFromOffScreen)
            {
                var startPos = UIUtility.GetOffScreenPosition(target, _entryDirection, _offScreenOffset);
                target.anchoredPosition = startPos;
            }
            
            // Initial pop in
            sequence.AppendCallback(() => canvasGroup.alpha = 1f);
            sequence.Append(target.DOScale(Vector3.one, EntryDuration * 0.2f).SetEase(Ease.OutBack));
            
            if (_startFromOffScreen)
            {
                sequence.Join(target.DOAnchorPos(Vector2.zero, EntryDuration * 0.3f).SetEase(Ease.OutBack));
            }
            
            // Perform flips
            for (int i = 0; i < _flipCount; i++)
            {
                if (i > 0)
                {
                    sequence.AppendInterval(_pauseBetweenFlips);
                }
                
                // First half of flip (to 90 degrees)
                var flipAxis = GetFlipAxis();
                var firstHalfRotation = flipAxis * _flipAngle * 0.5f;
                var secondHalfRotation = flipAxis * _flipAngle;
                
                // First half - scale down and fade
                sequence.Append(target.DOLocalRotate(firstHalfRotation, _flipDuration * 0.5f).SetEase(Ease.InQuad));
                sequence.Join(target.DOScale(_flipScale, _flipDuration * 0.5f).SetEase(Ease.InQuad));
                
                if (_addFade)
                {
                    sequence.Join(canvasGroup.DOFade(_midpointFade, _flipDuration * 0.5f).SetEase(Ease.InQuad));
                }
                
                if (_addShadowEffect)
                {
                    sequence.Join(target.DOScale(_shadowScale, _flipDuration * 0.5f).SetEase(Ease.InQuad));
                }
                
                // Second half - scale up and restore
                sequence.Append(target.DOLocalRotate(secondHalfRotation, _flipDuration * 0.5f).SetEase(Ease.OutQuad));
                sequence.Join(target.DOScale(Vector3.one, _flipDuration * 0.5f).SetEase(Ease.OutQuad));
                
                if (_addFade)
                {
                    sequence.Join(canvasGroup.DOFade(1f, _flipDuration * 0.5f).SetEase(Ease.OutQuad));
                }
                
                if (_addShadowEffect)
                {
                    sequence.Join(target.DOScale(Vector3.one, _flipDuration * 0.5f).SetEase(Ease.OutQuad));
                }
            }
            
            // Final bounce
            if (_addBounce)
            {
                sequence.Append(target.DOScale(_bounceScale, 0.1f).SetEase(Ease.OutBack));
                sequence.Append(target.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutBack));
            }
            
            return sequence.Play();
        }

        public override Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup)
        {
            var sequence = DOTween.Sequence();
            
            // Quick flip before disappearing
            var flipAxis = GetFlipAxis();
            var flipRotation = flipAxis * _flipAngle;
            
            sequence.Append(target.DOLocalRotate(flipRotation, ExitDuration * 0.3f).SetEase(Ease.InQuad));
            sequence.Join(target.DOScale(_flipScale, ExitDuration * 0.3f).SetEase(Ease.InQuad));
            sequence.Join(canvasGroup.DOFade(_midpointFade, ExitDuration * 0.3f).SetEase(Ease.InQuad));
            
            // Complete the flip and disappear
            sequence.Append(target.DOLocalRotate(flipAxis * _flipAngle * 1.5f, ExitDuration * 0.3f).SetEase(Ease.OutQuad));
            sequence.Join(target.DOScale(Vector3.zero, ExitDuration * 0.3f).SetEase(Ease.InBack));
            sequence.Join(canvasGroup.DOFade(0, ExitDuration * 0.3f).SetEase(Ease.InQuad));
            
            // Exit to off-screen if enabled
            if (_exitToOffScreen)
            {
                var exitPos = UIUtility.GetOffScreenPosition(target, _exitDirection, _offScreenOffset);
                sequence.Append(target.DOAnchorPos(exitPos, ExitDuration * 0.4f).SetEase(Ease.InBack));
            }
            
            return sequence.Play();
        }

        private Vector3 GetFlipAxis()
        {
            return _flipDirection switch
            {
                FlipDirection.X => Vector3.right,
                FlipDirection.Y => Vector3.up,
                FlipDirection.Both => Vector3.one,
                _ => Vector3.up
            };
        }
    }
}