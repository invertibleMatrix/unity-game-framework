namespace AK.Systems
{
	/// <summary>
	/// Centralized constants for the UISystem to avoid magic numbers and improve maintainability.
	/// </summary>
	public static class UIConstants
	{
		/// <summary>
		/// Default duration for toast messages in seconds.
		/// </summary>
		public const float DEFAULT_TOAST_DURATION = 2f;

		/// <summary>
		/// Distance in pixels that toast messages float upward.
		/// </summary>
		public const float TOAST_FLOAT_DISTANCE = 120f;

		/// <summary>
		/// Duration in seconds for toast float animation.
		/// </summary>
		public const float TOAST_FLOAT_DURATION = 2f;

		/// <summary>
		/// Duration in seconds for toast fade out animation.
		/// </summary>
		public const float TOAST_FADE_OUT_DURATION = 0.5f;

		/// <summary>
		/// Duration in seconds for background overlay fade in animation.
		/// </summary>
		public const float OVERLAY_FADE_IN_DURATION = 0.4f;

		/// <summary>
		/// Duration in seconds for background overlay fade out animation.
		/// </summary>
		public const float OVERLAY_FADE_OUT_DURATION = 0.1f;

		/// <summary>
		/// Default duration for banner messages in seconds.
		/// </summary>
		public const float DEFAULT_BANNER_DURATION = 2f;

		/// <summary>
		/// Default fragment ID for single-variant fragments.
		/// </summary>
		public const string DEFAULT_FRAGMENT_ID = "";

		public const string BANNER1 = "banner1";
		public const string BANNER2 = "banner2";

		/// <summary>
		/// Affirmation banner variant ID.
		/// </summary>
		public const string AFFIRMATION_BANNER_ID = "affirmation";

		/// <summary>
		/// Default pivot value for UI elements (center).
		/// </summary>
		public const float DEFAULT_PIVOT = 0.5f;

		/// <summary>
		/// Default sprite pixels per unit.
		/// </summary>
		public const float DEFAULT_PPU = 1f;

		/// <summary>
		/// Default scale for UI elements.
		/// </summary>
		public const float DEFAULT_SCALE = 1f;

		/// <summary>
		/// Default alpha value for fully opaque elements.
		/// </summary>
		public const float FULL_ALPHA = 1.0f;

		/// <summary>
		/// Default alpha value for fully transparent elements.
		/// </summary>
		public const float ZERO_ALPHA = 0.0f;

		/// <summary>
		/// Alpha value for tutorial mode background overlay (95% opaque for stronger focus).
		/// </summary>
		public const float TUTORIAL_OVERLAY_ALPHA = 0.99f;

		/// <summary>
		/// Alpha value for default background overlay (80% opaque dark).
		/// </summary>
		public const float DEFAULT_OVERLAY_ALPHA = 0.85f;

		/// <summary>
		/// Default anchored position for UI elements (center).
		/// </summary>
		public const float ZERO_POSITION = 0f;
	}
}