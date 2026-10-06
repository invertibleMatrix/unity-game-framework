using AK.CoreDomain;
using AK.Services.Analytics;
using AK.Services.Analytics.Providers;
using UnityEngine;

namespace AK.Services
{
	public class AnalyticsServiceBuilder
	{
		private readonly AnalyticsService _service = new();
		private bool _useGameAnalytics;
		private bool _useMixpanel;
		private bool _useMeta;
		private bool _useAppsFlyer;
		private bool _useDebug;
		private AnalyticsMeta _meta;
		private AnalyticsInitOptions _options;
		private AnalyticsTaxonomy _taxonomy;

		public AnalyticsServiceBuilder UseGameAnalytics(bool use = true)
		{
			_useGameAnalytics = use;
			return this;
		}

		public AnalyticsServiceBuilder UseMixpanel(bool use = true)
		{
			_useMixpanel = use;
			return this;
		}

		public AnalyticsServiceBuilder UseMeta(bool use = true)
		{
			_useMeta = use;
			return this;
		}

		public AnalyticsServiceBuilder UseAppsFlyer(bool use = true)
		{
			_useAppsFlyer = use;
			return this;
		}

		public AnalyticsServiceBuilder UseDebug(bool use = true)
		{
			_useDebug = use;
			return this;
		}

		public AnalyticsServiceBuilder WithMeta(AnalyticsMeta meta)
		{
			_meta = meta;
			return this;
		}

		public AnalyticsServiceBuilder WithOptions(AnalyticsInitOptions options)
		{
			_options = options;
			return this;
		}

		/// <summary>The game's analytics vocabulary; see <see cref="AnalyticsService.SetTaxonomy"/>.</summary>
		public AnalyticsServiceBuilder WithTaxonomy(AnalyticsTaxonomy taxonomy)
		{
			_taxonomy = taxonomy;
			return this;
		}

		public AnalyticsServiceBuilder AddProvider(IAnalyticsProvider provider)
		{
			_service.RegisterProvider(provider);
			return this;
		}

		public AnalyticsService Build()
		{
			if (_meta != null)
			{
				_service.SetMeta(_meta);
			}

			if (_options != null)
			{
				_service.SetOptions(_options);
			}

			if (_taxonomy != null)
			{
				_service.SetTaxonomy(_taxonomy);
			}

			if (_useGameAnalytics)
			{
				IAnalyticsProvider ga = AnalyticsProviderFactory.TryCreateGameAnalytics();
				if (ga != null)
				{
					_service.RegisterProvider(ga);
				}
				else
				{
					Debug.LogWarning("[AnalyticsServiceBuilder] UseGameAnalytics() requested but AK.Services.GameAnalytics is not loaded. Install com.gameanalytics.sdk so the GameAnalytics provider assembly compiles.");
				}
			}

			if (_useMixpanel)
			{
				IAnalyticsProvider mixpanel = AnalyticsProviderFactory.TryCreateMixpanel();
				if (mixpanel != null)
				{
					_service.RegisterProvider(mixpanel);
				}
				else
				{
					Debug.LogWarning("[AnalyticsServiceBuilder] UseMixpanel() requested but AK.Services.Mixpanel is not loaded. Install com.mixpanel.unity so the Mixpanel provider assembly compiles.");
				}
			}

			if (_useMeta)
			{
				IAnalyticsProvider meta = AnalyticsProviderFactory.TryCreateMeta();
				if (meta != null)
				{
					_service.RegisterProvider(meta);
				}
				else
				{
					Debug.LogWarning("[AnalyticsServiceBuilder] UseMeta() requested but AK.Services.Meta is not loaded. Import the Facebook SDK for Unity so the Meta provider assembly compiles.");
				}
			}

			if (_useAppsFlyer)
			{
				IAnalyticsProvider appsFlyer = AnalyticsProviderFactory.TryCreateAppsFlyer();
				if (appsFlyer != null)
				{
					_service.RegisterProvider(appsFlyer);
				}
				else
				{
					Debug.LogWarning("[AnalyticsServiceBuilder] UseAppsFlyer() requested but AK.Services.AppsFlyer is not loaded. Install appsflyer-unity-plugin so the AppsFlyer provider assembly compiles.");
				}
			}

			if (_useDebug || (Application.isEditor && !_useGameAnalytics && !_useMixpanel && !_useMeta && !_useAppsFlyer))
			{
				_service.RegisterProvider(new DebugAnalyticsProvider());
			}

			return _service;
		}
	}
}
