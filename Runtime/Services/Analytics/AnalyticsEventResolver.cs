using System.Collections.Generic;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using UnityEngine;

namespace AK.Services.Analytics
{
	/// <summary>
	/// Applies an optional <see cref="AnalyticsEventDefinition"/> overlay.
	/// Missing/changed definitions never drop events unless FailClosed is set.
	/// Sampling is per user: <c>userBucket</c> is the user's <see cref="AK.Kernel.Analytics.UserSampling.Bucket"/>.
	/// </summary>
	public static class AnalyticsEventResolver
	{
		public readonly struct Result
		{
			public readonly bool Dropped;
			public readonly string DropReason;
			public readonly bool MissingDefinition;
			public readonly bool UsedDefaults;
			public readonly bool MissingRequired;
			public readonly AnalyticsEvent Event;

			public Result(
				bool dropped,
				string dropReason,
				bool missingDefinition,
				bool usedDefaults,
				bool missingRequired,
				AnalyticsEvent evt)
			{
				Dropped = dropped;
				DropReason = dropReason;
				MissingDefinition = missingDefinition;
				UsedDefaults = usedDefaults;
				MissingRequired = missingRequired;
				Event = evt;
			}

			public static Result Send(
				AnalyticsEvent evt,
				bool missingDefinition,
				bool usedDefaults,
				bool missingRequired = false) =>
				new(false, null, missingDefinition, usedDefaults, missingRequired, evt);

			public static Result Drop(string reason, AnalyticsEvent evt, bool missingDefinition) =>
				new(true, reason, missingDefinition, false, false, evt);
		}

		public static Result Resolve(AnalyticsEvent evt, AnalyticsMeta meta, double userBucket)
		{
			if (evt == null || string.IsNullOrEmpty(evt.Id))
			{
				return Result.Drop("empty_event", evt, true);
			}

			AnalyticsEvent resolved = evt.Clone();
			AnalyticsEventDefinition def = FindDefinition(meta, resolved.Id);

			if (def == null)
			{
				if (resolved.Kind == AnalyticsEventKind.Unspecified)
				{
					resolved.Kind = InferKind(resolved);
				}

				return Result.Send(resolved, missingDefinition: true, usedDefaults: false);
			}

			if (!def.IsActive)
			{
				return Result.Drop("inactive", resolved, false);
			}

			if (def.DevOnly && !Debug.isDebugBuild)
			{
				return Result.Drop("dev_only", resolved, false);
			}

			if (!def.ShouldTrack(userBucket))
			{
				return Result.Drop("sampled", resolved, false);
			}

			bool usedDefaults = ApplyDefaults(def, resolved);

			if (def.Kind != AnalyticsEventKind.Unspecified && resolved.Kind == AnalyticsEventKind.Unspecified)
			{
				resolved.Kind = def.Kind;
			}

			if (string.IsNullOrEmpty(resolved.Id) == false && !string.IsNullOrEmpty(def.DesignEventId) && resolved.Kind != AnalyticsEventKind.Progression)
			{
				if (resolved.Kind == AnalyticsEventKind.Unspecified || resolved.Kind == AnalyticsEventKind.Design)
				{
					resolved.Id = def.DesignEventId;
				}
			}
			else if (!string.IsNullOrEmpty(def.ProviderEventName) && resolved.Kind != AnalyticsEventKind.Progression && resolved.Kind != AnalyticsEventKind.Ad)
			{
				resolved.Id = def.ProviderEventName;
			}
			else if (!string.IsNullOrEmpty(def.EventID) && resolved.Kind == AnalyticsEventKind.Design)
			{
				resolved.Id = def.EventID;
			}

			if (!string.IsNullOrEmpty(def.Progression01) && string.IsNullOrEmpty(resolved.Progression01))
			{
				resolved.Progression01 = def.Progression01;
				resolved.Progression02 = def.Progression02;
				resolved.Progression03 = def.Progression03;
			}

			if (resolved.Kind == AnalyticsEventKind.Unspecified)
			{
				resolved.Kind = InferKind(resolved);
			}

			bool missingRequired = HasMissingRequired(def, resolved.Parameters);
			if (missingRequired && def.FailClosed)
			{
				return Result.Drop("missing_required", resolved, false);
			}

			return Result.Send(resolved, missingDefinition: false, usedDefaults, missingRequired);
		}

		public static AnalyticsEventDefinition FindDefinition(AnalyticsMeta meta, string eventId)
		{
			if (meta == null || string.IsNullOrEmpty(eventId))
			{
				return null;
			}

			return meta.GetEventByName(eventId);
		}

		public static string ParameterKey(AnalyticsParameter parameter)
		{
			if (parameter == null)
			{
				return "param";
			}

			if (!string.IsNullOrEmpty(parameter.Key))
			{
				return parameter.Key;
			}

			if (parameter.Name != ParameterName.None)
			{
				return AnalyticsNameUtility.ToSnakeCase(parameter.Name.ToString());
			}

			return "param";
		}

		public static AnalyticsEventKind InferKind(AnalyticsEvent evt)
		{
			if (evt == null)
			{
				return AnalyticsEventKind.Design;
			}

			if (evt.Kind != AnalyticsEventKind.Unspecified)
			{
				return evt.Kind;
			}

			if (evt.ProgressionStatus.HasValue || !string.IsNullOrEmpty(evt.Progression01))
			{
				return AnalyticsEventKind.Progression;
			}

			if (evt.AdAction.HasValue)
			{
				return AnalyticsEventKind.Ad;
			}

			if (evt.ResourceFlow.HasValue)
			{
				return AnalyticsEventKind.Resource;
			}

			if (evt.ErrorSeverity.HasValue)
			{
				return AnalyticsEventKind.Error;
			}

			if (evt.Price.HasValue || !string.IsNullOrEmpty(evt.ItemId) && evt.Kind == AnalyticsEventKind.Business)
			{
				return AnalyticsEventKind.Business;
			}

			return AnalyticsEventKind.Design;
		}

		public static Dictionary<string, object> Stringify(Dictionary<ParameterName, object> parameters)
		{
			var result = new Dictionary<string, object>();
			if (parameters == null)
			{
				return result;
			}

			foreach (var kvp in parameters)
			{
				string key = kvp.Key == ParameterName.None
					? "param"
					: AnalyticsNameUtility.ToSnakeCase(kvp.Key.ToString());
				result[key] = kvp.Value;
			}

			return result;
		}

		private static bool ApplyDefaults(AnalyticsEventDefinition def, AnalyticsEvent evt)
		{
			if (def.Parameters == null || def.Parameters.Count == 0)
			{
				return false;
			}

			Dictionary<string, object> parameters = evt.EnsureParameters();
			bool used = false;

			for (int i = 0; i < def.Parameters.Count; i++)
			{
				AnalyticsParameter param = def.Parameters[i];
				if (param == null || string.IsNullOrEmpty(param.DefaultValue))
				{
					continue;
				}

				string key = ParameterKey(param);
				if (parameters.ContainsKey(key))
				{
					continue;
				}

				parameters[key] = param.GetDefaultValue();
				used = true;
			}

			return used;
		}

		private static bool HasMissingRequired(AnalyticsEventDefinition def, Dictionary<string, object> parameters)
		{
			if (def.Parameters == null)
			{
				return false;
			}

			for (int i = 0; i < def.Parameters.Count; i++)
			{
				AnalyticsParameter param = def.Parameters[i];
				if (param == null || !param.IsRequired)
				{
					continue;
				}

				string key = ParameterKey(param);
				if (parameters == null || !parameters.ContainsKey(key) || parameters[key] == null)
				{
					return true;
				}
			}

			return false;
		}
	}
}
