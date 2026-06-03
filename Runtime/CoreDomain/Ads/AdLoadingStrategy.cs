using System;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// Preset options for ad loading strategies.
	/// </summary>
	public enum AdLoadingStrategyPreset
	{
		/// <summary>
		/// Use the custom LoadingStrategy defined on the placement.
		/// </summary>
		Custom = 0,

		/// <summary>
		/// Aggressive preloading - always keep an ad ready.
		/// </summary>
		Aggressive = 1,

		/// <summary>
		/// Standard loading - preload and reload after show.
		/// </summary>
		Standard = 2,

		/// <summary>
		/// Lazy loading - load only when needed.
		/// </summary>
		Lazy = 3,

		/// <summary>
		/// Manual loading - no automatic loading.
		/// </summary>
		Manual = 4
	}

	/// <summary>
	/// Defines the loading strategy for ad placements.
	/// Controls how ads are loaded, reloaded, and managed.
	/// </summary>
	[Serializable]
	public class AdLoadingStrategy
	{
		/// <summary>
		/// Predefined loading strategies for common use cases.
		/// </summary>
		public static class Presets
		{
			/// <summary>
			/// Aggressive preloading - always keep an ad ready.
			/// Best for rewarded ads that are frequently shown.
			/// </summary>
			public static AdLoadingStrategy Aggressive => new()
			{
				PreloadOnInitialize = true,
				AutoReloadAfterShow = true,
				AutoReloadOnFail = true,
				ReloadDelaySeconds = 0,
				MaxRetryAttempts = 5,
				RetryDelaySeconds = 5,
				KeepLoadedInBackground = true
			};

			/// <summary>
			/// Standard loading - preload and reload after show.
			/// Good balance between performance and resource usage.
			/// </summary>
			public static AdLoadingStrategy Standard => new()
			{
				PreloadOnInitialize = true,
				AutoReloadAfterShow = true,
				AutoReloadOnFail = true,
				ReloadDelaySeconds = 1,
				MaxRetryAttempts = 3,
				RetryDelaySeconds = 10,
				KeepLoadedInBackground = true
			};

			/// <summary>
			/// Lazy loading - load only when needed.
			/// Best for rarely shown ads like app open ads.
			/// </summary>
			public static AdLoadingStrategy Lazy => new()
			{
				PreloadOnInitialize = false,
				AutoReloadAfterShow = false,
				AutoReloadOnFail = false,
				ReloadDelaySeconds = 0,
				MaxRetryAttempts = 1,
				RetryDelaySeconds = 30,
				KeepLoadedInBackground = false
			};

			/// <summary>
			/// Minimal loading - no automatic loading at all.
			/// Full manual control over when ads are loaded.
			/// </summary>
			public static AdLoadingStrategy Manual => new()
			{
				PreloadOnInitialize = false,
				AutoReloadAfterShow = false,
				AutoReloadOnFail = false,
				ReloadDelaySeconds = 0,
				MaxRetryAttempts = 0,
				RetryDelaySeconds = 0,
				KeepLoadedInBackground = false
			};
		}

		/// <summary>
		/// Whether to preload this ad during service initialization.
		/// </summary>
		public bool PreloadOnInitialize = true;

		/// <summary>
		/// Whether to automatically reload the ad after it has been shown.
		/// Ensures the next ad is ready as soon as possible.
		/// </summary>
		public bool AutoReloadAfterShow = true;

		/// <summary>
		/// Whether to automatically retry loading if the ad fails to load.
		/// </summary>
		public bool AutoReloadOnFail = true;

		/// <summary>
		/// Delay in seconds before reloading after a successful show.
		/// Use 0 for immediate reload, or add a small delay to avoid rapid requests.
		/// </summary>
		[Range(0, 60)]
		public float ReloadDelaySeconds = 1f;

		/// <summary>
		/// Maximum number of retry attempts when auto-reload on fail is enabled.
		/// Set to 0 for unlimited retries, or a specific number to limit attempts.
		/// </summary>
		[Range(0, 10)]
		public int MaxRetryAttempts = 3;

		/// <summary>
		/// Delay in seconds between retry attempts.
		/// </summary>
		[Range(1, 120)]
		public float RetryDelaySeconds = 10f;

		/// <summary>
		/// Whether to keep the ad loaded when the app goes to background.
		/// If false, the ad will be destroyed when the app is paused.
		/// </summary>
		public bool KeepLoadedInBackground = true;

		/// <summary>
		/// Whether to load the next ad immediately on app resume if not loaded.
		/// Only applies when KeepLoadedInBackground is false.
		/// </summary>
		public bool LoadOnAppResume = true;

		/// <summary>
		/// Whether to use exponential backoff for retry delays.
		/// Each retry will wait longer than the previous one.
		/// </summary>
		public bool UseExponentialBackoff = false;

		/// <summary>
		/// Maximum delay in seconds for exponential backoff.
		/// </summary>
		[Range(10, 300)]
		public float MaxBackoffDelaySeconds = 60f;

		/// <summary>
		/// Creates a copy of this strategy.
		/// </summary>
		public AdLoadingStrategy Clone()
		{
			return new AdLoadingStrategy
			{
				PreloadOnInitialize = PreloadOnInitialize,
				AutoReloadAfterShow = AutoReloadAfterShow,
				AutoReloadOnFail = AutoReloadOnFail,
				ReloadDelaySeconds = ReloadDelaySeconds,
				MaxRetryAttempts = MaxRetryAttempts,
				RetryDelaySeconds = RetryDelaySeconds,
				KeepLoadedInBackground = KeepLoadedInBackground,
				LoadOnAppResume = LoadOnAppResume,
				UseExponentialBackoff = UseExponentialBackoff,
				MaxBackoffDelaySeconds = MaxBackoffDelaySeconds
			};
		}

		/// <summary>
		/// Gets the delay for a specific retry attempt.
		/// Handles exponential backoff if enabled.
		/// </summary>
		public float GetRetryDelay(int attemptNumber)
		{
			if (!UseExponentialBackoff)
				return RetryDelaySeconds;

			// Exponential backoff: delay * 2^attempt, capped at max
			float delay = RetryDelaySeconds * (float)Math.Pow(2, attemptNumber);
			return Math.Min(delay, MaxBackoffDelaySeconds);
		}
	}
}