using System;
using System.Collections.Generic;

namespace AK.Services.Analytics
{
	/// <summary>
	/// Firebase Analytics name rules. An event, parameter or user property name is ASCII letters,
	/// digits and underscores, starts with a letter, and doesn't start with <c>firebase_</c>,
	/// <c>google_</c> or <c>ga_</c>. Event and parameter names are at most 40 characters, user
	/// property names 24, and some event and user property names are Firebase's own. Firebase
	/// drops an event, parameter or user property whose name breaks these rules.
	///
	/// <para>To make a name valid, every UTF-16 code unit other than an ASCII letter, digit or '_'
	/// becomes '_', and the name is cut to its maximum length. A name that then doesn't start with
	/// a letter, starts with a reserved prefix or is reserved gets "e_" in front, and is cut again.
	/// A valid name comes back as is.</para>
	/// </summary>
	public static class FirebaseAnalyticsNames
	{
		public const int MaxEventNameLength = 40;
		public const int MaxParameterNameLength = 40;
		public const int MaxUserPropertyNameLength = 24;

		// Put in front of a name Firebase would refuse.
		private const string Prefix = "e_";

		private static readonly string[] ReservedPrefixes = { "firebase_", "google_", "ga_" };

		// Google Analytics' reserved names for apps, less those with a reserved prefix.
		private static readonly HashSet<string> ReservedEventNames = new(StringComparer.Ordinal)
		{
			"ad_activeview", "ad_click", "ad_exposure", "ad_query", "ad_reward", "adunit_exposure",
			"app_background", "app_clear_data", "app_exception", "app_install", "app_remove",
			"app_store_refund", "app_store_subscription_cancel", "app_store_subscription_convert",
			"app_store_subscription_renew", "app_update", "app_upgrade", "dynamic_link_app_open",
			"dynamic_link_app_update", "dynamic_link_first_open", "error", "first_open", "first_visit",
			"in_app_purchase", "notification_dismiss", "notification_foreground", "notification_open",
			"notification_receive", "notification_send", "os_update", "session_start",
			"session_start_with_rollout", "user_engagement",
		};

		private static readonly HashSet<string> ReservedUserPropertyNames = new(StringComparer.Ordinal)
		{
			"cid", "customer_id", "customerid", "first_open_after_install", "first_open_time",
			"first_visit_time", "last_advertising_id_reset", "last_deep_link_referrer", "last_gclid",
			"lifetime_user_engagement", "non_personalized_ads", "session_id", "session_number",
			"sessionid", "sfmc_id", "sid", "uid", "user_id", "userid",
		};

		/// <summary>Makes <paramref name="name"/> a valid event name. Null or empty is "event".</summary>
		public static string SanitizeEventName(string name)
		{
			return Sanitize(name, MaxEventNameLength, ReservedEventNames, "event");
		}

		/// <summary>Makes <paramref name="name"/> a valid parameter name. Null or empty is "parameter".</summary>
		public static string SanitizeParameterName(string name)
		{
			return Sanitize(name, MaxParameterNameLength, null, "parameter");
		}

		/// <summary>Makes <paramref name="name"/> a valid user property name. Null or empty is "property".</summary>
		public static string SanitizeUserPropertyName(string name)
		{
			return Sanitize(name, MaxUserPropertyNameLength, ReservedUserPropertyNames, "property");
		}

		private static string Sanitize(string name, int maxLength, HashSet<string> reserved, string empty)
		{
			if (string.IsNullOrEmpty(name))
			{
				return empty;
			}

			int length = Math.Min(name.Length, maxLength);
			int clean = 0;
			while (clean < length && IsAllowed(name[clean]))
			{
				clean++;
			}

			string cut;
			if (clean == name.Length)
			{
				cut = name;
			}
			else if (clean == length)
			{
				cut = name.Substring(0, length);
			}
			else
			{
				cut = string.Create(length, name, static (span, source) =>
				{
					for (int i = 0; i < span.Length; i++)
					{
						char c = source[i];
						span[i] = IsAllowed(c) ? c : '_';
					}
				});
			}

			if (IsAsciiLetter(cut[0]) && !HasReservedPrefix(cut) && (reserved == null || !reserved.Contains(cut)))
			{
				return cut;
			}

			return string.Create(Math.Min(Prefix.Length + cut.Length, maxLength), cut, static (span, source) =>
			{
				Prefix.AsSpan().CopyTo(span);
				source.AsSpan(0, span.Length - Prefix.Length).CopyTo(span.Slice(Prefix.Length));
			});
		}

		private static bool HasReservedPrefix(string name)
		{
			for (int i = 0; i < ReservedPrefixes.Length; i++)
			{
				if (name.StartsWith(ReservedPrefixes[i], StringComparison.Ordinal))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsAllowed(char c)
		{
			return IsAsciiLetter(c) || (c >= '0' && c <= '9') || c == '_';
		}

		private static bool IsAsciiLetter(char c)
		{
			return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
		}
	}
}
