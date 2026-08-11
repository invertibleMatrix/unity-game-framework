using DG.Tweening;
using UnityEngine;

namespace Utilities.ModelPreview
{
	/// <summary>How a model makes its entrance when a preview loads.</summary>
	public enum ModelPreviewIntro
	{
		/// <summary>Appear at full scale immediately.</summary>
		None,

		/// <summary>Scale from zero with a bouncy pop — the collectible-reveal default.</summary>
		Pop,
	}

	/// <summary>Per-call overrides for a single preview. Null members fall back to session defaults.</summary>
	public sealed class ModelPreviewOptions
	{
		/// <summary>Override the session's interaction default for this preview only.</summary>
		public bool? EnableInteraction;

		/// <summary>Multiplier on the auto-framed camera distance. Null = session default. The fit is a bounding sphere, so 1 = already clip-proof at any rotation; &gt;1 just adds air.</summary>
		public float? FramingMargin;

		/// <summary>Idle yaw rotation in degrees/second. 0 = off.</summary>
		public float AutoRotateSpeed;

		/// <summary>Camera background. Null = transparent (UI behind the preview shows through).</summary>
		public Color? BackgroundColor;

		/// <summary>Entrance animation. Default Pop — a bouncy scale-up reveal.</summary>
		public ModelPreviewIntro Intro = ModelPreviewIntro.Pop;

		/// <summary>Intro length in seconds.</summary>
		public float IntroDuration = 0.45f;

		/// <summary>Intro easing. OutBack overshoots slightly, which reads as "pop".</summary>
		public Ease IntroEase = Ease.OutBack;
	}
}
