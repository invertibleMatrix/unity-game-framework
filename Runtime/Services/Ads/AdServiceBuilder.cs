using System;
using AK.Services.Ads.Providers;
using AK.CoreDomain;
using AK.CoreDomain.RemoteConfig;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// Builder class for creating and configuring AdService instances.
	/// Provides a fluent API for setting up ad providers and configuration.
	/// </summary>
	public class AdServiceBuilder
	{
		private const string TAG = "[AdServiceBuilder]";

		private readonly AdService _adService;
		private bool _useAdMob;
		private bool _useMax;
		private bool _useNullProviderAsFallback = false;
		private bool _simulateAdsInEditor = true;
		private bool _userCanTrack = true;
		private bool _userUnderAge = false;

		/// <summary>
		/// Creates a new AdServiceBuilder.
		/// </summary>
		public AdServiceBuilder()
		{
			_adService = new AdService();
		}

		/// <summary>
		/// Opt in to the AdMob provider. Off unless this is called.
		/// Ignored when MAX is also enabled — MAX already mediates Google demand.
		/// </summary>
		public AdServiceBuilder UseAdMob(bool useAdMob = true)
		{
			_useAdMob = useAdMob;
			return this;
		}

		/// <summary>
		/// Opt in to the AppLovin MAX provider. Off unless this is called.
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
		/// Adds a custom ad provider.
		/// </summary>
		public AdServiceBuilder AddProvider(IAdProvider provider)
		{
			_adService.AddProvider(provider);
			return this;
		}

		/// <summary>
		/// Builds and returns the configured AdService.
		/// Note: You still need to call InitializeAsync() on the service.
		/// </summary>
		public AdService Build()
		{
			// Set user consent settings
			_adService.SetUserConsent(_userCanTrack);
			_adService.SetUserUnderAge(_userUnderAge);

			if (_useMax && _useAdMob)
				Debug.LogWarning($"{TAG} UseAdMob and UseMax were both requested; AdMob is skipped because MAX already mediates Google.");

			bool maxAdded = false;

#if MAX_ENABLED
			if (_useMax)
			{
				_adService.AddProvider(new MaxAdProvider());
				maxAdded = true;
			}
#else
			if (_useMax)
				Debug.LogWarning($"{TAG} UseMax() was requested but the AppLovin MAX package is not present (MAX_ENABLED).");
#endif

			// AdMob requires ADMOB_ENABLED; without it the provider would report initialized with no SDK.
#if ADMOB_ENABLED && (UNITY_ANDROID || UNITY_IOS)
			if (_useAdMob && !maxAdded)
			{
				_adService.AddProvider(new AdMobAdProvider());
			}
#elif !ADMOB_ENABLED
			if (_useAdMob && !maxAdded)
				Debug.LogWarning($"{TAG} UseAdMob() was requested but ADMOB_ENABLED is not defined.");
#endif

			// Add null provider for testing/fallback
			if (_useNullProviderAsFallback || (Application.isEditor && _simulateAdsInEditor))
			{
				_adService.AddProvider(new NullAdProvider(_simulateAdsInEditor));
			}

			return _adService;
		}

		/// <summary>
		/// Creates an AdService with no network providers. Call
		/// <see cref="UseAdMob"/>, <see cref="UseMax"/>, or <see cref="AddProvider"/> on a builder instead.
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
				.UseAdMob(false)
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
		/// Use <see cref="AdServiceBuilder"/> and call <see cref="AdServiceBuilder.UseAdMob"/>,
		/// <see cref="AdServiceBuilder.UseMax"/>, or <see cref="AdServiceBuilder.AddProvider"/>.
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