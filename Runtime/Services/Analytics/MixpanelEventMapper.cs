using System.Collections.Generic;
using AK.CoreDomain.Analytics;

namespace AK.Services.Analytics
{
	public readonly struct MixpanelMappedEvent
	{
		public MixpanelMappedEvent(string eventName, Dictionary<string, object> properties)
		{
			EventName = string.IsNullOrEmpty(eventName) ? "event" : eventName;
			Properties = properties ?? new Dictionary<string, object>();
		}

		public string EventName { get; }
		public Dictionary<string, object> Properties { get; }
	}

	/// <summary>
	/// Maps an event to a flat event name and properties, for Mixpanel and Meta. A design event
	/// is named by the game's <see cref="IFlatEventNamer"/>, or by its id's parts joined with '_'.
	/// Pure; no vendor SDK reference.
	/// </summary>
	public static class MixpanelEventMapper
	{
		/// <param name="evt">The event; null maps to an empty "event".</param>
		/// <param name="namer">Names design events; null gives every design event the default name.</param>
		public static MixpanelMappedEvent Map(AnalyticsEvent evt, IFlatEventNamer namer)
		{
			if (evt == null)
			{
				return new MixpanelMappedEvent("event", new Dictionary<string, object>());
			}

			return AnalyticsEventResolver.InferKind(evt) switch
			{
				AnalyticsEventKind.Progression => MapProgression(evt),
				AnalyticsEventKind.Ad => MapAd(evt),
				AnalyticsEventKind.Resource => MapResource(evt),
				AnalyticsEventKind.Business => MapBusiness(evt),
				AnalyticsEventKind.Error => MapError(evt),
				_ => MapDesign(evt, namer)
			};
		}

		private static MixpanelMappedEvent MapDesign(AnalyticsEvent evt, IFlatEventNamer namer)
		{
			Dictionary<string, object> props = CopyParams(evt);
			DesignEventParts parts = DesignEventParts.Parse(evt.Id);
			Put(props, "ga_event_id", GameAnalyticsEventMapper.BuildDesignEventId(evt.Id));
			PutValue(props, evt);

			if (namer != null && namer.TryName(parts, props, out string eventName))
			{
				return new MixpanelMappedEvent(eventName, props);
			}

			return new MixpanelMappedEvent(parts.Count == 0 ? "event" : parts.Join("_"), props);
		}

		private static MixpanelMappedEvent MapProgression(AnalyticsEvent evt)
		{
			Dictionary<string, object> props = CopyParams(evt);
			Put(props, "status", ProgressionStatusName(evt.ProgressionStatus));
			Put(props, "p01", evt.Progression01 ?? evt.Id);
			Put(props, "p02", evt.Progression02);
			Put(props, "p03", evt.Progression03);
			if (evt.Score.HasValue)
			{
				props["score"] = evt.Score.Value;
			}

			return new MixpanelMappedEvent("progression", props);
		}

		private static MixpanelMappedEvent MapAd(AnalyticsEvent evt)
		{
			Dictionary<string, object> props = CopyParams(evt);
			Put(props, "placement", evt.AdPlacement, overwrite: false);
			Put(props, "type", evt.AdType);
			Put(props, "sdk", evt.AdSdkName);
			Put(props, "fail_reason", evt.AdFailReason);
			if (evt.AdDurationMs.HasValue)
			{
				props["duration_ms"] = evt.AdDurationMs.Value;
			}

			if (evt.AdRevenue.HasValue)
			{
				props["revenue"] = evt.AdRevenue.Value;
			}

			string name = evt.AdAction switch
			{
				AnalyticsAdAction.RewardReceived => "ad_sdk_reward",
				AnalyticsAdAction.FailedShow => "ad_sdk_failed",
				AnalyticsAdAction.Clicked => "ad_sdk_clicked",
				AnalyticsAdAction.Request => "ad_sdk_request",
				AnalyticsAdAction.Loaded => "ad_sdk_loaded",
				_ => "ad_sdk_show"
			};

			return new MixpanelMappedEvent(name, props);
		}

		private static MixpanelMappedEvent MapResource(AnalyticsEvent evt)
		{
			Dictionary<string, object> props = CopyParams(evt);
			Put(props, "flow", evt.ResourceFlow == AnalyticsResourceFlow.Sink ? "sink" : "source");
			Put(props, "currency", evt.ResourceCurrency);
			Put(props, "item_type", evt.ResourceItemType);
			Put(props, "item_id", evt.ResourceItemId);
			if (evt.ResourceAmount.HasValue)
			{
				props["amount"] = evt.ResourceAmount.Value;
			}

			return new MixpanelMappedEvent("resource", props);
		}

		private static MixpanelMappedEvent MapBusiness(AnalyticsEvent evt)
		{
			Dictionary<string, object> props = CopyParams(evt);
			Put(props, "item_id", evt.ItemId);
			Put(props, "currency", evt.Currency);
			Put(props, "cart_type", evt.CartType);
			if (evt.Price.HasValue)
			{
				props["price"] = evt.Price.Value;
			}

			return new MixpanelMappedEvent("purchase", props);
		}

		private static MixpanelMappedEvent MapError(AnalyticsEvent evt)
		{
			Dictionary<string, object> props = CopyParams(evt);
			Put(props, "severity", evt.ErrorSeverity?.ToString().ToLowerInvariant());
			Put(props, "message", evt.ErrorMessage);
			return new MixpanelMappedEvent("error", props);
		}

		private static Dictionary<string, object> CopyParams(AnalyticsEvent evt)
		{
			return evt.Parameters == null
				? new Dictionary<string, object>()
				: new Dictionary<string, object>(evt.Parameters);
		}

		private static void PutValue(Dictionary<string, object> props, AnalyticsEvent evt)
		{
			if (evt.Value.HasValue)
			{
				props["value"] = evt.Value.Value;
			}
		}

		private static void Put(Dictionary<string, object> props, string key, object value, bool overwrite = true)
		{
			if (value == null || string.IsNullOrEmpty(key))
			{
				return;
			}

			if (value is string s && string.IsNullOrEmpty(s))
			{
				return;
			}

			if (!overwrite && props.ContainsKey(key))
			{
				return;
			}

			props[key] = value;
		}

		private static string ProgressionStatusName(AnalyticsProgressionStatus? status)
		{
			return status switch
			{
				AnalyticsProgressionStatus.Complete => "complete",
				AnalyticsProgressionStatus.Fail => "fail",
				_ => "start"
			};
		}
	}
}
