namespace Utilities.ModelPreview
{
	public enum ModelPreviewRenderMode
	{
		/// <summary>Camera renders every frame — for animated or moving models.</summary>
		Live,

		/// <summary>Camera renders a few warmup frames, then disables itself. The RT persists as a still.</summary>
		Static
	}

	public sealed class ModelPreviewSessionOptions
	{
		/// <summary>Size used when the session creates a RenderTexture (ignored for caller-owned RTs).</summary>
		public int TextureSize = 256;

		/// <summary>Soft cap on concurrent live booths in this session.</summary>
		public int MaxConcurrent = 8;

		/// <summary>Live for animated/hovering models; Static for grids of stills (perf).</summary>
		public ModelPreviewRenderMode RenderMode = ModelPreviewRenderMode.Live;

		/// <summary>Default multiplier on the auto-framed camera distance. The fit is a bounding sphere, so 1 = already clip-proof at any rotation; &gt;1 just adds air.</summary>
		public float FramingMargin = 1.15f;

		/// <summary>Default for per-call interaction (drag to rotate, pinch/scroll to zoom).</summary>
		public bool EnableInteraction;

		/// <summary>Frames a Static booth renders before its camera disables.</summary>
		public int StaticWarmupFrames = 3;
	}
}
