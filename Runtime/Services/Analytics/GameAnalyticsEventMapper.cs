using System;
using System.Collections.Generic;

namespace AK.Services.Analytics
{
	/// <summary>
	/// GameAnalytics id rules. A design event id is 1 to 5 parts joined by colons, and every part,
	/// a progression's included, is 1 to 64 of <c>A-Z a-z 0-9 - _ . ( ) ! ?</c> and white space.
	/// GameAnalytics drops an event whose id breaks them. Never dumps arbitrary parameter values
	/// into the hierarchy (that is what blew cardinality and silently truncated events in the old
	/// provider).
	/// </summary>
	public static class GameAnalyticsEventMapper
	{
		public const int MaxParts = 5;
		public const int MaxPartLength = 64;

		private const string None = "none";

		/// <summary>
		/// Makes <paramref name="input"/> a valid id part, by ASCII rules alone:
		/// <list type="bullet">
		/// <item>ASCII white space (space, and tab to carriage return) is trimmed from both ends;</item>
		/// <item>every UTF-16 code unit other than <c>A-Z a-z 0-9 - _ . ( ) ! ?</c> becomes '_',
		/// white space inside the part included, so a character outside the BMP becomes two;</item>
		/// <item>the part is cut to <see cref="MaxPartLength"/>.</item>
		/// </list>
		/// Null, empty or blank input is "none". A part made only of those characters comes back
		/// as is.
		/// </summary>
		public static string SanitizeSegment(string input)
		{
			if (string.IsNullOrEmpty(input))
			{
				return None;
			}

			int start = 0;
			int end   = input.Length;
			while (start < end && IsAsciiWhiteSpace(input[start]))
			{
				start++;
			}

			while (end > start && IsAsciiWhiteSpace(input[end - 1]))
			{
				end--;
			}

			int length = Math.Min(end - start, MaxPartLength);
			if (length == 0)
			{
				return None;
			}

			int clean = 0;
			while (clean < length && IsAllowed(input[start + clean]))
			{
				clean++;
			}

			if (clean == length)
			{
				return length == input.Length ? input : input.Substring(start, length);
			}

			return string.Create(length, (input, start), static (part, source) =>
			{
				for (int i = 0; i < part.Length; i++)
				{
					char c = source.input[source.start + i];
					part[i] = IsAllowed(c) ? c : '_';
				}
			});
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

		private static bool IsAllowed(char c)
		{
			return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
			    || c == '-' || c == '_' || c == '.' || c == '(' || c == ')' || c == '!' || c == '?';
		}

		private static bool IsAsciiWhiteSpace(char c)
		{
			return c == ' ' || (c >= '\t' && c <= '\r');
		}
	}
}
