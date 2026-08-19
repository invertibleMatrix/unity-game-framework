using UnityEngine;
using DG.Tweening;

namespace AK.Utilities
{
	public class DiscoEffect : MonoBehaviour
	{
		public enum EndBehavior
		{
			SmoothReset, // Returns to original color
			DisableSprite // Fades out and disables the component
		}

		[SerializeField] private SpriteRenderer _spriteRenderer;

		[Header("Settings")] [Tooltip("How long the disco effect lasts.")] [SerializeField]
		private float _effectDuration = 5f;

		[Tooltip("The speed of the color cycle. Lower is slower.")] [SerializeField]
		private float _colorCycleSpeed = 2f;

		[Tooltip("Transparency during the effect (0 = invisible, 1 = solid).")] [Range(0f, 1f)] [SerializeField]
		private float _targetAlpha = 0.8f;

		[Header("Cleanup")] [SerializeField]
		private EndBehavior onComplete = EndBehavior.DisableSprite;

		[SerializeField] private float _endTransitionTime = 0.5f;

		// Internal state
		private Tween _discoTween;
		private Color _originalColor;

		public void PlayDiscoEffect()
		{
			if (_spriteRenderer == null) return;

			// 1. Kill any existing tween on this sprite to prevent conflicts
			_spriteRenderer.DOKill();

			_spriteRenderer.enabled = true;
			// 2. Store original state
			_originalColor = _spriteRenderer.color;

			// 3. Create the Sequence
			// We use a Sequence so we can chain the Loop and the End Phase
			Sequence sequence = DOTween.Sequence();

			// Pause initially so we can satisfy the requirement to call Play() explicitly
			sequence.Pause();

			// --- PHASE 1: The Disco Loop ---
			// We use DOVirtual.Float to animate a value from 0 to 1 repeatedly.
			// inside the update callback, we convert that value to a Color (HSV).
			Tween colorLoop = DOVirtual.Float(0f, 1f, _colorCycleSpeed, (float value) =>
			                           {
				                           // Create a rainbow color based on the current 'value' (Hue)
				                           Color discoColor = Color.HSVToRGB(value, 1f, 1f);

				                           // Apply the user-configured transparency
				                           discoColor.a = _targetAlpha;

				                           _spriteRenderer.color = discoColor;
			                           })
			                           .SetLoops(-1, LoopType.Restart) // Infinite loop (we will kill it manually or via sequence duration)
			                           .SetEase(Ease.Linear); // Linear ensures the color transition is constant

			// Add the loop to the sequence. 
			// Note: Since the loop is infinite, we just append it. We will handle the "Duration" by limiting the sequence insert.
			// However, a cleaner way for a fixed duration is to Append the loop for the specific duration.
			// BUT, DOVirtual.Float with infinite loops blocks the sequence.

			// BETTER APPROACH FOR SEQUENCE: 
			// We run the infinite color changer on the side, and use the Sequence to manage the TIME.

			// Let's refactor the sequence logic for robustness:
			// We will tween a dummy value for 'effectDuration', and OnUpdate run the color logic.

			float hue = 0f;
			sequence.Append(DOVirtual.Float(0f, 1f, _effectDuration, (v) =>
			{
				// Increment hue based on time and speed
				hue += Time.deltaTime * (1f / _colorCycleSpeed);
				if (hue > 1f) hue -= 1f;

				Color c = Color.HSVToRGB(hue, 1f, 1f);
				c.a = _targetAlpha;
				_spriteRenderer.color = c;
			}).SetEase(Ease.Linear));

			// --- PHASE 2: The End Behavior ---
			sequence.OnComplete(() => { HandleEndBehavior(_spriteRenderer); });

			// 4. Explicitly Play the Tween
			_discoTween = sequence;
			_discoTween.Play();
		}

		private void HandleEndBehavior(SpriteRenderer targetSprite)
		{
			if (onComplete == EndBehavior.DisableSprite)
			{
				// Fade out alpha to 0, then disable
				targetSprite.DOFade(0f, _endTransitionTime)
				            .SetEase(Ease.InQuad)
				            .OnComplete(() => targetSprite.enabled = false)
				            .Play();
			}
			else
			{
				// Smoothly return to original color
				targetSprite.DOColor(_originalColor, _endTransitionTime)
				            .Play();
			}
		}

		private void OnDestroy()
		{
			// Safety: clean up tweens if the object is destroyed
			if (_discoTween != null && _discoTween.IsActive())
			{
				_discoTween.Kill();
			}
		}
	}
}