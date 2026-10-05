using System.Collections.Generic;
using AK.Core;
using AK.Services.Ads;
using AK.Services.Ads.Providers;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// Builder class for creating and configuring AdService instances.
	/// Provides a fluent API for setting up ad providers and configuration.
	/// Each <see cref="Build"/> makes a new service, which owns the providers it was given.
	/// </summary>
	public class AdServiceBuilder
	{
		private const string TAG = "[AdServiceBuilder]";

		private readonly List<IAdProvider> _customProviders = new();
		private AdServiceOptions _options = AdServiceOptions.Default;
		private IAdsClock _clock;
		private PrefsStore _capStore;
		private bool _capStoreSet;
		private bool _useMax;
		private bool _useNullProviderAsFallback = false;
		private bool _simulateAdsInEditor = true;
		private bool _userCanTrack = true;
		private bool _userUnderAge = false;

		/// <summary>
		/// Opt in to the AppLovin MAX provider. Off unless this is called.
		/// Requires the game assembly to reference AK.Services.MaxAds (and the MAX package).
		/// </summary>
		public AdServiceBuilder UseMax(bool useMax = true)
		{
			_useMax = useMax;
			return this;
		}

		/// <summary>
		/// Enables or disables the null provider as a fallback.
		/// </summary>
		public AdServiceBuilder UseNullProviderAsFallback(bool useFallback = true)
		{
			_useNullProviderAsFallback = useFallback;
			return this;
		}

		/// <summary>
		/// Enables or disables ad simulation in the Unity editor.
		/// </summary>
		public AdServiceBuilder SimulateAdsInEditor(bool simulate = true)
		{
			_simulateAdsInEditor = simulate;
			return this;
		}

		/// <summary>
		/// Sets the user consent for personalized ads (GDPR/CCPA compliance).
		/// </summary>
		public AdServiceBuilder WithUserConsent(bool canTrack)
		{
			_userCanTrack = canTrack;
			return this;
		}

		/// <summary>
		/// Sets whether the user is under age (COPPA compliance).
		/// </summary>
		public AdServiceBuilder WithUserUnderAge(bool isUnderAge)
		{
			_userUnderAge = isUnderAge;
			return this;
		}

		/// <summary>
		/// Sets how long the service waits for providers and loads. <see cref="AdServiceOptions.Default"/> unless called.
		/// </summary>
		public AdServiceBuilder WithOptions(AdServiceOptions options)
		{
			_options = options;
			return this;
		}

		/// <summary>
		/// Sets the clock the service's timers run on. A <see cref="ForegroundAdsClock"/> unless called.
		/// </summary>
		public AdServiceBuilder WithClock(IAdsClock clock)
		{
			_clock = clock;
			return this;
		}

		/// <summary>
		/// Sets where the service keeps frequency caps across launches; null keeps them for the
		/// service's lifetime only. <see cref="UniPrefs.Store"/> unless called.
		/// </summary>
		public AdServiceBuilder WithCapStore(PrefsStore capStore)
		{
			_capStore    = capStore;
			_capStoreSet = true;
			return this;
		}

		/// <summary>
		/// Adds a custom ad provider.
		/// </summary>
		public AdServiceBuilder AddProvider(IAdProvider provider)
		{
			if (provider != null && !_customProviders.Contains(provider))
			{
				_customProviders.Add(provider);
			}

			return this;
		}

		/// <summary>
		/// Builds and returns the configured AdService.
		/// Note: You still need to call InitializeAsync() on the service.
		/// </summary>
		public AdService Build()
		{
			var adService = new AdService(_options, _clock ?? new ForegroundAdsClock(), _capStoreSet ? _capStore : UniPrefs.Store);

			// Set user consent settings
			adService.SetUserConsent(_userCanTrack);
			adService.SetUserUnderAge(_userUnderAge);

			foreach (IAdProvider provider in _customProviders)
			{
				adService.AddProvider(provider);
			}

			if (_useMax)
			{
				IAdProvider max = AdsProviderFactory.TryCreateMax();
				if (max != null)
				{
					adService.AddProvider(max);
				}
				else
				{
					Debug.LogWarning($"{TAG} UseMax() requested but AK.Services.MaxAds is not loaded. Install com.applovin.mediation.ads and reference AK.Services.MaxAds from the game assembly.");
				}
			}

			// Add null provider for testing/fallback
			if (_useNullProviderAsFallback || (Application.isEditor && _simulateAdsInEditor))
			{
				adService.AddProvider(new NullAdProvider(_simulateAdsInEditor));
			}

			return adService;
		}

		/// <summary>
		/// Creates an AdService with no network providers. Call
		/// <see cref="UseMax"/> or <see cref="AddProvider"/> on a builder instead.
		/// </summary>
		public static AdService CreateDefault()
		{
			return new AdServiceBuilder()
				.Build();
		}

		/// <summary>
		/// Creates an AdService for testing/development with simulated ads.
		/// </summary>
		/// <returns>An AdService with simulated ad behavior.</returns>
		public static AdService CreateForTesting()
		{
			return new AdServiceBuilder()
				.UseMax(false)
				.UseNullProviderAsFallback(true)
				.SimulateAdsInEditor(true)
				.Build();
		}
	}

	/// <summary>
	/// Extension methods for registering AdService with dependency injection.
	/// </summary>
	public static class AdServiceExtensions
	{
		/// <summary>
		/// Creates an AdService with consent flags only — no network provider is selected.
		/// Use <see cref="AdServiceBuilder"/> and call <see cref="AdServiceBuilder.UseMax"/>
		/// or <see cref="AdServiceBuilder.AddProvider"/>.
		/// </summary>
		public static AdService CreateAdService(
			bool canTrack = true,
			bool isUnderAge = false)
		{
			return new AdServiceBuilder()
				.WithUserConsent(canTrack)
				.WithUserUnderAge(isUnderAge)
				.Build();
		}
	}
}
