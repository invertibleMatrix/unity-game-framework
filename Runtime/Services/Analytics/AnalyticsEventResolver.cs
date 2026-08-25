using System;
using System.Collections.Generic;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using UnityEngine;

namespace AK.Services.Analytics
{
	/// <summary>
	/// Applies an optional <see cref="AnalyticsEventDefinition"/> overlay.
	/// Missing/changed definitions never drop events unless FailClosed is set.
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

		public static Result Resolve(AnalyticsEvent evt, AnalyticsMeta meta)
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

			if (!def.ShouldTrack())
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

				parameters[key] = CoerceDefault(param);
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

		private static object CoerceDefault(AnalyticsParameter param)
		{
			switch (param.Type)
			{
				case AnalyticsParameterType.Integer:
					return int.TryParse(param.DefaultValue, out int i) ? i : 0;
				case AnalyticsParameterType.Float:
					return float.TryParse(param.DefaultValue, out float f) ? f : 0f;
				case AnalyticsParameterType.Boolean:
					return bool.TryParse(param.DefaultValue, out bool b) && b;
				default:
					return param.DefaultValue;
			}
		}
	}

	/// <summary>
	/// GameAnalytics design-event id rules: 1-5 colon parts, [A-Za-z0-9-_.,:()!?], 64 chars each.
	/// Never dumps arbitrary parameter values into the hierarchy (that is what blew cardinality
	/// and silently truncated events in the old provider).
	/// </summary>
	public static class GameAnalyticsEventMapper
	{
		public const int MaxParts = 5;
		public const int MaxPartLength = 64;

		public static string SanitizeSegment(string input)
		{
			if (string.IsNullOrEmpty(input))
			{
				return "none";
			}

			var chars = input.Trim().ToCharArray();
			for (int i = 0; i < chars.Length; i++)
			{
				char c = chars[i];
				bool ok = char.IsLetterOrDigit(c)
					|| c == '-' || c == '_' || c == '.' || c == ','
					|| c == '(' || c == ')' || c == '!' || c == '?';
				if (!ok)
				{
					chars[i] = '_';
				}
			}

			string sanitized = new string(chars);
			if (sanitized.Length > MaxPartLength)
			{
				sanitized = sanitized.Substring(0, MaxPartLength);
			}

			return string.IsNullOrEmpty(sanitized) ? "none" : sanitized;
		}

		public static string BuildDesignEventId(params string[] parts)
		{
			if (parts == null || parts.Length == 0)
			{
				return "event";
			}

			var built = new List<string>(MaxParts);
			for (int i = 0; i < parts.Length && built.Count < MaxParts; i++)
			{
				if (string.IsNullOrEmpty(parts[i]))
				{
					continue;
				}

				string[] split = parts[i].Split(':');
				for (int s = 0; s < split.Length && built.Count < MaxParts; s++)
				{
					if (!string.IsNullOrEmpty(split[s]))
					{
						built.Add(SanitizeSegment(split[s]));
					}
				}
			}

			return built.Count == 0 ? "event" : string.Join(":", built);
		}

		public static string AgeDimension(int age)
		{
			if (age < 0)
			{
				age = 0;
			}

			return age > 10 ? "age_10plus" : "age_" + age;
		}

		/// <summary>
		/// Street vs family for ambient chat. Roster proto has no kind field;
		/// birth-family walkers carry a family role, promoted street people do not.
		/// </summary>
		public static string AmbientKindDimension(string role)
		{
			if (string.IsNullOrEmpty(role))
			{
				return "street";
			}

			switch (role.Trim().ToLowerInvariant())
			{
				case "mother":
				case "father":
				case "sibling":
				case "aunt":
				case "uncle":
				case "cousin":
				case "grandmother":
				case "grandfather":
					return "family";
				default:
					return "street";
			}
		}

		/// <summary>
		/// Dashboard archetype rows are UUIDs; local/tests use slugs like bus-driver.
		/// Prefer a readable slug or label in the Design id — never a unique person name.
		/// </summary>
		public static string AmbientArchetypeDimension(string archetypeId, string archetypeLabel)
		{
			if (!string.IsNullOrEmpty(archetypeId) && !LooksLikeUuid(archetypeId))
			{
				return SanitizeSegment(archetypeId.ToLowerInvariant());
			}

			if (!string.IsNullOrEmpty(archetypeLabel))
			{
				return SanitizeSegment(archetypeLabel.ToLowerInvariant());
			}

			if (!string.IsNullOrEmpty(archetypeId))
			{
				return SanitizeSegment(archetypeId.ToLowerInvariant());
			}

			return "unknown";
		}

		private static bool LooksLikeUuid(string value)
		{
			return Guid.TryParse(value, out _);
		}

		public static string AgeProgressionPart(int age)
		{
			if (age < 0)
			{
				age = 0;
			}

			return age > 10 ? "10plus" : age.ToString();
		}

		public static string LifeDimension(int meepleCount)
		{
			if (meepleCount <= 1)
			{
				return "life_1";
			}

			if (meepleCount == 2)
			{
				return "life_2";
			}

			return "life_3plus";
		}

		public static string RollsBucket(int rolls)
		{
			if (rolls <= 0) return "0";
			if (rolls <= 5) return "1_5";
			if (rolls <= 10) return "6_10";
			if (rolls <= 20) return "11_20";
			return "21plus";
		}

		public static string EventIndexBucket(int eventIndex)
		{
			if (eventIndex <= 0)
			{
				return "e0";
			}

			if (eventIndex <= 10)
			{
				return "e" + eventIndex;
			}

			if (eventIndex <= 20)
			{
				return "e11_20";
			}

			return "e21plus";
		}

		public static string[] AgeDimensionValues()
		{
			var values = new string[12];
			for (int i = 0; i <= 10; i++)
			{
				values[i] = "age_" + i;
			}

			values[11] = "age_10plus";
			return values;
		}

		public static readonly string[] ContentStateValues = { "onboarding", "playing", "capped" };
		public static readonly string[] LifeDimensionValues = { "life_1", "life_2", "life_3plus" };
	}
}
