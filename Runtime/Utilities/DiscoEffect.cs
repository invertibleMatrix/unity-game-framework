using AK.Core.Extensions;
using AK.Kernel.Timing;
using UnityEngine;
using DG.Tweening;

namespace AK.Utilities
{
	/// <summary>
	/// Cycles a sprite through the colors for a while, then fades it out and disables it, or
	/// eases it back to the color it had before. Every tween here targets the sprite: playing
	/// again restarts the effect, and destroying the component ends it.
	/// </summary>
	public class DiscoEffect : MonoBehaviour
	{
		public enum EndBehavior
		{
			SmoothReset, // Returns to original color
			DisableSprite // Fades out and disables the component
		}

		// Keeps a cycle setting of zero from dividing by zero.
		private const float MinCycleSeconds = 0.01f;

		[SerializeField] private SpriteRenderer _spriteRenderer;

		[Header("Settings")] [Tooltip("How long the disco effect lasts.")] [SerializeField]
		private float _effectDuration = 5f;

		[Tooltip("Seconds per cycle through the colors. Lower is faster.")] [SerializeField]
		private float _colorCycleSpeed = 2f;

		[Tooltip("Transparency during the effect (0 = invisible, 1 = solid).")] [Range(0f, 1f)] [SerializeField]
		private float _targetAlpha = 0.8f;

		[Tooltip("The time the effect runs on.")] [SerializeField]
		private TimeDomain _timeDomain = TimeDomain.Scaled;

		[Header("Cleanup")] [SerializeField]
		private EndBehavior onComplete = EndBehavior.DisableSprite;

		[SerializeField] private float _endTransitionTime = 0.5f;

		// The sprite's color before the effect. An effect played over another keeps it.
		private Color _originalColor;
		private bool  _running;

		public void PlayDiscoEffect()
		{
			if (_spriteRenderer == null) return;

			// Ends the effect already playing, its end phase included, and any other tween on the sprite.
			_spriteRenderer.DOKill();

			if (!_running) _originalColor = _spriteRenderer.color;
			_running = true;
			_spriteRenderer.enabled = true;

			float cycleSeconds = Mathf.Max(_colorCycleSpeed, MinCycleSeconds);
			float duration = Mathf.Max(0f, _effectDuration);

			// The tweened value is the time the effect has run.
			DOVirtual.Float(0f, duration, duration, elapsed =>
			         {
				         Color color = Color.HSVToRGB(Mathf.Repeat(elapsed / cycleSeconds, 1f), 1f, 1f);
				         color.a = _targetAlpha;
				         _spriteRenderer.color = color;
			         })
			         .SetEase(Ease.Linear)
			         .SetTarget(_spriteRenderer)
			         .SetId(this)
			         .SetTimeDomain(_timeDomain)
			         .OnComplete(EndEffect)
			         .Play();
		}

		private void EndEffect()
		{
			float duration = Mathf.Max(0f, _endTransitionTime);

			if (onComplete == EndBehavior.DisableSprite)
			{
				// Fades out, then disables the sprite with its color back as it was.
				_spriteRenderer.DOFade(0f, duration)
				               .SetEase(Ease.InQuad)
				               .SetId(this)
				               .SetTimeDomain(_timeDomain)
				               .OnComplete(() =>
				               {
					               _spriteRenderer.enabled = false;
					               _spriteRenderer.color = _originalColor;
					               _running = false;
				               })
				               .Play();
			}
			else
			{
				_spriteRenderer.DOColor(_originalColor, duration)
				               .SetId(this)
				               .SetTimeDomain(_timeDomain)
				               .OnComplete(() => _running = false)
				               .Play();
			}
		}

		private void OnDestroy()
		{
			// This effect's tweens only: the sprite may outlive the component.
			DOTween.Kill(this);
		}
	}
}
