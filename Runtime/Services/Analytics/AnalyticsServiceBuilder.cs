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
		private bool _useFirebase;
		private bool _useDebug;
		private AnalyticsMeta _meta;
		private AnalyticsInitOptions _options;

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

		public AnalyticsServiceBuilder UseFirebase(bool use = true)
		{
			_useFirebase = use;
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

			if (_useGameAnalytics)
			{
				IAnalyticsProvider ga = AnalyticsProviderFactory.TryCreateGameAnalytics();
				if (ga != null)
				{
					_service.RegisterProvider(ga);
				}
				else
				{
					Debug.LogWarning("[AnalyticsServiceBuilder] UseGameAnalytics() requested but AK.Services.GameAnalytics is not loaded. Install com.gameanalytics.sdk and reference AK.Services.GameAnalytics from the game assembly.");
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
					Debug.LogWarning("[AnalyticsServiceBuilder] UseMixpanel() requested but AK.Services.Mixpanel is not loaded. Install com.mixpanel.unity and reference AK.Services.Mixpanel from the game assembly.");
				}
			}

			if (_useFirebase)
			{
				_service.RegisterProvider(new FirebaseAnalyticsProvider());
			}

			if (_useDebug || (Application.isEditor && !_useGameAnalytics && !_useMixpanel && !_useFirebase))
			{
				_service.RegisterProvider(new DebugAnalyticsProvider());
			}

			return _service;
		}
	}
}
