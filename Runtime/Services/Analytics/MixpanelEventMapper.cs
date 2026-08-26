using System;
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
	/// Flattens colon Design ids (and GA progression/ad envelopes) into Mixpanel
	/// event name + properties. Pure; no vendor SDK reference.
	/// </summary>
	public static class MixpanelEventMapper
	{
		public static MixpanelMappedEvent Map(AnalyticsEvent evt)
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
				_ => MapDesign(evt)
			};
		}

		private static MixpanelMappedEvent MapDesign(AnalyticsEvent evt)
		{
			Dictionary<string, object> props = CopyParams(evt);
			List<string> parts = SplitParts(evt.Id);
			Put(props, "ga_event_id", GameAnalyticsEventMapper.BuildDesignEventId(evt.Id));
			PutValue(props, evt);

			if (StartsWith(parts, "install", "first_open"))
			{
				return new MixpanelMappedEvent("install_first_open", props);
			}

			if (StartsWith(parts, "meeple", "start"))
			{
				return new MixpanelMappedEvent("meeple_start", props);
			}

			if (StartsWith(parts, "meeple", "switch"))
			{
				return new MixpanelMappedEvent("meeple_switch", props);
			}

			if (StartsWith(parts, "meeple", "chat"))
			{
				Put(props, "age_dim", Part(parts, 2));
				Put(props, "ambient_kind", Part(parts, 3));
				Put(props, "archetype", Part(parts, 4));
				return new MixpanelMappedEvent("meeple_chat", props);
			}

			if (StartsWith(parts, "dice", "roll"))
			{
				Put(props, "age_dim", Part(parts, 2));
				return new MixpanelMappedEvent("dice_roll", props);
			}

			if (StartsWith(parts, "age", "reached"))
			{
				Put(props, "age_dim", Part(parts, 2));
				return new MixpanelMappedEvent("age_reached", props);
			}

			if (StartsWith(parts, "age", "time"))
			{
				Put(props, "age_dim", Part(parts, 2));
				return new MixpanelMappedEvent("age_time", props);
			}

			if (StartsWith(parts, "content", "cap"))
			{
				return new MixpanelMappedEvent("content_cap", props);
			}

			if (StartsWith(parts, "funnel", "onboarding"))
			{
				Put(props, "step", Part(parts, 2));
				return new MixpanelMappedEvent("funnel_onboarding", props);
			}

			if (StartsWith(parts, "checkpoint"))
			{
				Put(props, "surface", Part(parts, 1));
				Put(props, "id3", Part(parts, 2));
				Put(props, "id4", Part(parts, 3));
				Put(props, "id5", Part(parts, 4));
				return new MixpanelMappedEvent("checkpoint", props);
			}

			if (StartsWith(parts, "time", "board"))
			{
				return new MixpanelMappedEvent("time_board", props);
			}

			if (StartsWith(parts, "time", "district"))
			{
				return new MixpanelMappedEvent("time_district", props);
			}

			if (StartsWith(parts, "time", "onboarding"))
			{
				return new MixpanelMappedEvent("time_onboarding", props);
			}

			if (StartsWith(parts, "time", "meeple"))
			{
				return new MixpanelMappedEvent("time_meeple", props);
			}

			if (StartsWith(parts, "ad", "offer", "shown"))
			{
				Put(props, "placement", Part(parts, 3), overwrite: false);
				return new MixpanelMappedEvent("ad_offer_shown", props);
			}

			if (StartsWith(parts, "ad", "offer", "watched"))
			{
				Put(props, "placement", Part(parts, 3), overwrite: false);
				return new MixpanelMappedEvent("ad_offer_watched", props);
			}

			if (StartsWith(parts, "ad", "offer", "failed"))
			{
				Put(props, "placement", Part(parts, 3), overwrite: false);
				return new MixpanelMappedEvent("ad_offer_failed", props);
			}

			if (StartsWith(parts, "ad", "watch"))
			{
				Put(props, "placement", Part(parts, 2), overwrite: false);
				return new MixpanelMappedEvent("ad_watch", props);
			}

			if (StartsWith(parts, "ad", "continue"))
			{
				Put(props, "placement", Part(parts, 2), overwrite: false);
				return new MixpanelMappedEvent("ad_continue", props);
			}

			if (StartsWith(parts, "ad", "revenue"))
			{
				return new MixpanelMappedEvent("ad_revenue", props);
			}

			return new MixpanelMappedEvent(FallbackName(parts), props);
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

		private static List<string> SplitParts(string id)
		{
			var parts = new List<string>();
			if (string.IsNullOrEmpty(id))
			{
				return parts;
			}

			string[] split = id.Split(':');
			for (int i = 0; i < split.Length; i++)
			{
				if (!string.IsNullOrEmpty(split[i]))
				{
					parts.Add(split[i]);
				}
			}

			return parts;
		}

		private static bool StartsWith(List<string> parts, params string[] prefix)
		{
			if (parts.Count < prefix.Length)
			{
				return false;
			}

			for (int i = 0; i < prefix.Length; i++)
			{
				if (!string.Equals(parts[i], prefix[i], StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
			}

			return true;
		}

		private static string Part(List<string> parts, int index)
		{
			return index >= 0 && index < parts.Count ? parts[index] : null;
		}

		private static string FallbackName(List<string> parts)
		{
			return parts.Count == 0 ? "event" : string.Join("_", parts);
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
