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
	/// How a placement's ad unit is kept loaded. Placements that share an ad unit share its
	/// loading; the first of them in the ads meta sets it.
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
				LoadOnAppResume = true
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
				LoadOnAppResume = true
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
				LoadOnAppResume = false
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
				LoadOnAppResume = false
			};
		}

		/// <summary>
		/// Whether the ad is kept loaded ahead of time: loaded when the service initializes, when
		/// a level refresh makes the placement available, and when an ad provider comes up late.
		/// Off, the ad loads when it's asked for: a show, LoadAdAsync or PreloadAdsAsync.
		/// </summary>
		public bool PreloadOnInitialize = true;

		/// <summary>
		/// Whether the next ad loads once a show ends, however it ended: completed, closed early
		/// or failed to display.
		/// </summary>
		public bool AutoReloadAfterShow = true;

		/// <summary>
		/// Whether a failed load retries on its own, up to <see cref="MaxRetryAttempts"/> times.
		/// Off, the ad unit waits until the ad is asked for again. Failures that waiting can't
		/// fix, such as an invalid ad unit, never retry.
		/// </summary>
		public bool AutoReloadOnFail = true;

		/// <summary>
		/// Delay in seconds between a show ending and the reload.
		/// Use 0 for immediate reload, or add a small delay to avoid rapid requests.
		/// </summary>
		[Range(0, 60)]
		public float ReloadDelaySeconds = 1f;

		/// <summary>
		/// How many times a failed load retries before the ad unit gives up, until the ad is next
		/// wanted. Each of these starts the count afresh: asking for the ad, a level refresh that
		/// preloads it, coming back to the foreground (with <see cref="LoadOnAppResume"/>), and a
		/// load that succeeds. Set to 0 for unlimited retries.
		/// </summary>
		[Range(0, 10)]
		public int MaxRetryAttempts = 3;

		/// <summary>
		/// Delay in seconds before each retry; with <see cref="UseExponentialBackoff"/>, before
		/// the first. Each delay is spread by a little random jitter, so that devices which
		/// failed together don't all retry together.
		/// </summary>
		[Range(1, 120)]
		public float RetryDelaySeconds = 10f;

		/// <summary>
		/// Whether coming back to the foreground loads an ad that was asked for, or preloads,
		/// and isn't loaded: one that ran out of retries, or is waiting to retry. It loads at
		/// once, with <see cref="MaxRetryAttempts"/> retries afresh.
		/// </summary>
		public bool LoadOnAppResume = true;

		/// <summary>
		/// Whether to use exponential backoff for retry delays.
		/// Each retry will wait twice as long as the previous one, up to
		/// <see cref="MaxBackoffDelaySeconds"/>.
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
				LoadOnAppResume = LoadOnAppResume,
				UseExponentialBackoff = UseExponentialBackoff,
				MaxBackoffDelaySeconds = MaxBackoffDelaySeconds
			};
		}
	}
}
