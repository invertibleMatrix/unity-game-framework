namespace AK.Systems
{
	/// <summary>
	/// Legacy constants kept for backward compatibility with V1 code.
	/// </summary>
	public static class UIConstants
	{
		// --- Toast ---
		public const float TOAST_FLOAT_DISTANCE    = 120f;
		public const float TOAST_FLOAT_DURATION    = 2f;
		public const float TOAST_FADE_OUT_DURATION = 0.5f;

		// --- Overlay ---
		public const float OVERLAY_FADE_IN_DURATION  = 0.4f;
		public const float OVERLAY_FADE_OUT_DURATION = 0.1f;

		// --- Banner ---
		public const float  DEFAULT_BANNER_DURATION = 2f;
		public const string BANNER1                 = "banner1";
		public const string BANNER2                 = "banner2";
		public const string AFFIRMATION_BANNER_ID   = "affirmation";

		// --- Layout ---
		public const float DEFAULT_PIVOT = 0.5f;
		public const float DEFAULT_PPU   = 1f;
		public const float DEFAULT_SCALE = 1f;
		public const float FULL_ALPHA    = 1.0f;
		public const float ZERO_ALPHA    = 0.0f;
		public const float ZERO_POSITION = 0f;

		/// <summary>
		/// Default duration for toast messages in seconds.
		/// </summary>
		public const float DEFAULT_TOAST_DURATION = 2f;

		/// <summary>
		/// Default fragment ID for single-variant fragments.
		/// </summary>
		public const string DEFAULT_FRAGMENT_ID = "";

		/// <summary>
		/// Alpha value for tutorial mode background overlay.
		/// Uses UIViewConstants value to stay consistent.
		/// </summary>
		public const float TUTORIAL_OVERLAY_ALPHA = 0.95f;

		/// <summary>
		/// Alpha value for default background overlay.
		/// </summary>
		public const float DEFAULT_OVERLAY_ALPHA = 0.85f;
	}
}