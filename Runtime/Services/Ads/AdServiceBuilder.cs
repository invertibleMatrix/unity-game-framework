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
		private readonly AdService _adService;
		private bool _useAdMob = true;
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
		/// Enables or disables AdMob provider.
		/// </summary>
		public AdServiceBuilder UseAdMob(bool useAdMob = true)
		{
			_useAdMob = useAdMob;
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

			// Add AdMob provider if enabled
#if UNITY_ANDROID || UNITY_IOS
			if (_useAdMob)
			{
				_adService.AddProvider(new AdMobAdProvider());
			}
#endif

			// Add null provider for testing/fallback
			// if (_useNullProviderAsFallback || Application.isEditor && _simulateAdsInEditor)
			// {
			// 	_adService.AddProvider(new NullAdProvider(_simulateAdsInEditor));
			// }

			return _adService;
		}

		/// <summary>
		/// Creates a default AdService with AdMob (on devices) and NullProvider (in editor/fallback).
		/// </summary>
		/// <returns>A configured AdService instance.</returns>
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
		/// Creates and configures an AdService with default settings.
		/// </summary>
		/// <param name="metaDataRepository">The meta data repository.</param>
		/// <param name="canTrack">Whether the user has consented to tracking.</param>
		/// <param name="isUnderAge">Whether the user is under age.</param>
		/// <returns>A configured AdService instance.</returns>
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