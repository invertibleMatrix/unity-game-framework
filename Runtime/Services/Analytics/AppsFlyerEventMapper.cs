using System.Collections.Generic;
using System.Globalization;
using AK.CoreDomain.Analytics;

namespace AK.Services.Analytics
{
	/// <summary>
	/// One impression's revenue in the shape AppsFlyer's ad-revenue API wants.
	/// Vendor-free so the mapper stays testable without the plugin assembly.
	/// </summary>
	public readonly struct AppsFlyerAdRevenue
	{
		public AppsFlyerAdRevenue(
			string mediationSdk,
			string monetizationNetwork,
			string currency,
			double revenue,
			Dictionary<string, string> additionalParameters)
		{
			MediationSdk = mediationSdk;
			MonetizationNetwork = monetizationNetwork;
			Currency = currency;
			Revenue = revenue;
			AdditionalParameters = additionalParameters ?? new Dictionary<string, string>();
		}

		/// <summary>Mediation layer that reported the impression (e.g. "applovin").</summary>
		public string MediationSdk { get; }

		/// <summary>Network that actually filled the ad (e.g. "Google AdMob", "Unity Ads").</summary>
		public string MonetizationNetwork { get; }

		public string Currency { get; }

		public double Revenue { get; }

		public Dictionary<string, string> AdditionalParameters { get; }
	}

	/// <summary>
	/// Curated allow-list of what goes to AppsFlyer. AppsFlyer is the attribution source of
	/// truth, not the analytics warehouse, so it only gets the signals ad networks optimize
	/// on: rewarded views, impression revenue, purchases and the design events the game's
	/// <see cref="IAppsFlyerEventSelector"/> picks, such as onboarding completion or level
	/// progress, under AppsFlyer's standard names so partner postbacks map automatically.
	/// Everything else is dropped. Pure; no vendor SDK reference.
	/// </summary>
	public static class AppsFlyerEventMapper
	{
		public const string TutorialCompletion = "af_tutorial_completion";
		public const string LevelAchieved = "af_level_achieved";
		public const string AdView = "af_ad_view";
		public const string Purchase = "af_purchase";

		public const string ParamSuccess = "af_success";
		public const string ParamTutorialId = "af_tutorial_id";
		public const string ParamContentId = "af_content_id";
		public const string ParamContentType = "af_content_type";
		public const string ParamLevel = "af_level";
		public const string ParamRevenue = "af_revenue";
		public const string ParamCurrency = "af_currency";
		public const string ParamQuantity = "af_quantity";
		public const string ParamAdType = "af_adrev_ad_type";
		public const string ParamAdNetwork = "af_adrev_network_name";
		public const string ParamPlacement = "placement";
		public const string ParamAdUnit = "ad_unit";
		public const string ParamAdTypeShort = "ad_type";

		/// <summary>MAX reports impression revenue in USD regardless of the user's locale.</summary>
		public const string AdRevenueCurrency = "USD";

		/// <summary>
		/// True when <paramref name="evt"/> is one of the allow-listed in-app events. Design events
		/// go to <paramref name="selector"/>; with no selector, none are sent. Ad-revenue
		/// impressions are not in-app events; see <see cref="TryMapAdRevenue"/>.
		/// </summary>
		public static bool TryMapEvent(
			AnalyticsEvent evt,
			IAppsFlyerEventSelector selector,
			out string eventName,
			out Dictionary<string, string> values)
		{
			eventName = null;
			values = null;
			if (evt == null)
			{
				return false;
			}

			switch (AnalyticsEventResolver.InferKind(evt))
			{
				case AnalyticsEventKind.Business:
					return TryMapPurchase(evt, out eventName, out values);
				case AnalyticsEventKind.Ad:
					return TryMapAdEnvelope(evt, out eventName, out values);
				case AnalyticsEventKind.Progression:
				case AnalyticsEventKind.Resource:
				case AnalyticsEventKind.Error:
					return false;
				default:
					return TrySelectDesign(evt, selector, out eventName, out values);
			}
		}

		/// <summary>
		/// True when <paramref name="evt"/> is a shown ad that carries impression revenue.
		/// </summary>
		public static bool TryMapAdRevenue(AnalyticsEvent evt, out AppsFlyerAdRevenue revenue)
		{
			revenue = default;
			if (evt == null
				|| AnalyticsEventResolver.InferKind(evt) != AnalyticsEventKind.Ad
				|| evt.AdAction != AnalyticsAdAction.Show
				|| !evt.AdRevenue.HasValue
				|| !(evt.AdRevenue.Value > 0))
			{
				return false;
			}

			string mediation = string.IsNullOrEmpty(evt.AdSdkName) ? "applovin" : evt.AdSdkName;
			string network = evt.ParameterText("network") ?? mediation;

			var additional = new Dictionary<string, string>();
			Put(additional, ParamAdTypeShort, evt.AdType);
			Put(additional, ParamPlacement, evt.AdPlacement ?? evt.ParameterText(ParamPlacement));
			Put(additional, ParamAdUnit, evt.ParameterText(ParamAdUnit));

			revenue = new AppsFlyerAdRevenue(mediation, network, AdRevenueCurrency, evt.AdRevenue.Value, additional);
			return true;
		}

		private static bool TrySelectDesign(
			AnalyticsEvent evt,
			IAppsFlyerEventSelector selector,
			out string eventName,
			out Dictionary<string, string> values)
		{
			if (selector == null
				|| !selector.TrySelect(evt, DesignEventParts.Parse(evt.Id), out eventName, out values)
				|| string.IsNullOrEmpty(eventName))
			{
				eventName = null;
				values = null;
				return false;
			}

			values ??= new Dictionary<string, string>();
			return true;
		}

		private static bool TryMapAdEnvelope(AnalyticsEvent evt, out string eventName, out Dictionary<string, string> values)
		{
			eventName = null;
			values = null;
			if (evt.AdAction != AnalyticsAdAction.RewardReceived)
			{
				return false;
			}

			eventName = AdView;
			values = new Dictionary<string, string>
			{
				{ ParamAdType, string.IsNullOrEmpty(evt.AdType) ? "rewarded" : evt.AdType }
			};
			Put(values, ParamAdNetwork, evt.ParameterText("network") ?? evt.AdSdkName);
			Put(values, ParamPlacement, evt.AdPlacement ?? evt.ParameterText(ParamPlacement));
			return true;
		}

		private static bool TryMapPurchase(AnalyticsEvent evt, out string eventName, out Dictionary<string, string> values)
		{
			eventName = null;
			values = null;
			if (!evt.Price.HasValue || string.IsNullOrEmpty(evt.Currency))
			{
				return false;
			}

			eventName = Purchase;
			values = new Dictionary<string, string>
			{
				{ ParamRevenue, evt.Price.Value.ToString(CultureInfo.InvariantCulture) },
				{ ParamCurrency, evt.Currency },
				{ ParamQuantity, "1" }
			};
			Put(values, ParamContentId, evt.ItemId);
			Put(values, ParamContentType, string.IsNullOrEmpty(evt.CartType) ? "iap" : evt.CartType);
			return true;
		}

		private static void Put(Dictionary<string, string> values, string key, string value)
		{
			if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
			{
				values[key] = value;
			}
		}
	}
}
