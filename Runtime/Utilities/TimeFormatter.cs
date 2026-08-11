using System;
using System.Text;
using System.Text.RegularExpressions;

namespace AK.Utilities
{
	// -------------------------------------------------------------------------
	// CONFIGURATION
	// -------------------------------------------------------------------------
	public enum TimeFormat
	{
		Digital, // 01:30:45
		Abbreviated, // 1h 30m
		Full, // 1 Hour 30 Minutes
		Compact, // 1h30m (No spaces)
		Stopwatch // 01:30.45 (Milliseconds)
	}

	public enum TimeRounding
	{
		Floor, // 59s -> 0m (Good for "Time Played")
		Ceil, // 59s -> 1m (CRITICAL for Cooldowns)
		Nearest // 59s -> 1m, 29s -> 0m
	}

	/// <summary>
	/// The comprehensive Time Utility for Unity.
	/// Handles Formatting, Parsing, Rounding, and Future Predictions.
	/// </summary>
	public static class TimeFormatter
	{
		private const int SECONDS_PER_MINUTE = 60;
		private const int SECONDS_PER_HOUR   = 3600;
		private const int SECONDS_PER_DAY    = 86400;

		// -------------------------------------------------------------------------
		// 1. EXTENSIONS (The API)
		// -------------------------------------------------------------------------

		// --- Duration ---
		public static string FormatDuration(this float s, TimeFormat f = TimeFormat.Digital, int max = 3, TimeRounding r = TimeRounding.Floor)
			=> FormatSpan(TimeSpan.FromSeconds(s), f, max, r);

		public static string FormatDuration(this int s, TimeFormat f = TimeFormat.Digital, int max = 3, TimeRounding r = TimeRounding.Floor)
			=> FormatSpan(TimeSpan.FromSeconds(s), f, max, r);

		public static string FormatDuration(this double s, TimeFormat f = TimeFormat.Digital, int max = 3, TimeRounding r = TimeRounding.Floor)
			=> FormatSpan(TimeSpan.FromSeconds(s), f, max, r);

		// --- Progress (New!) ---
		/// <summary>
		/// Formats a "Current / Total" string. Example: "01:30 / 02:00"
		/// </summary>
		public static string FormatProgress(this float current, float total, TimeFormat f = TimeFormat.Digital)
		{
			return $"{current.FormatDuration(f)} / {total.FormatDuration(f)}";
		}

		// --- Arrival / Future ---
		public static string GetArrivalTime(this float s, string fmt = "t") => DateTime.Now.AddSeconds(s).ToString(fmt);

		/// <summary>
		/// Returns smart descriptions like "Tomorrow at 5pm" or "Just now".
		/// </summary>
		public static string GetArrivalDescription(this float s) => GetArrivalDescImpl(s);

		public static string ToRelativeTime(this DateTime dt) => GetRelativeTimeImpl(dt);

		// --- Dynamic ---
		/// <summary>
		/// Switches format based on urgency. >1m: Digital, <1m: Decimals.
		/// </summary>
		public static string FormatDynamic(this float s) =>
			s >= 60 ? FormatSpan(TimeSpan.FromSeconds(s), TimeFormat.Digital, 3, TimeRounding.Floor) : s.ToString("0.0");

		// -------------------------------------------------------------------------
		// 2. PARSING (Input Logic)
		// -------------------------------------------------------------------------

		/// <summary>
		/// Parses "1h 30m", "90m", or "01:30:00" back to seconds.
		/// </summary>
		public static double ParseTime(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return 0;
			s = s.Trim().ToLowerInvariant();

			// 1. Digital (Colon)
			if (s.Contains(":"))
			{
				if (TimeSpan.TryParse(s, out TimeSpan ts)) return ts.TotalSeconds;
				// Fallback for M:S
				var parts = s.Split(':');
				if (parts.Length == 2 && double.TryParse(parts[0], out double m) && double.TryParse(parts[1], out double sc))
					return m * 60 + sc;
			}

			// 2. Suffixes (1h 30m)
			double total = 0;
			var matches = Regex.Matches(s, @"(\d*\.?\d+)\s*([dhms])");
			if (matches.Count > 0)
			{
				foreach (Match m in matches)
				{
					double val = double.Parse(m.Groups[1].Value);
					switch (m.Groups[2].Value)
					{
						case "d": total += val * SECONDS_PER_DAY; break;
						case "h": total += val * SECONDS_PER_HOUR; break;
						case "m": total += val * SECONDS_PER_MINUTE; break;
						case "s": total += val; break;
					}
				}

				return total;
			}

			// 3. Raw number fallback
			if (double.TryParse(s, out double raw)) return raw;

			return 0;
		}

		// -------------------------------------------------------------------------
		// 3. CORE LOGIC (Formatting & Rounding)
		// -------------------------------------------------------------------------

		private static string FormatSpan(TimeSpan t, TimeFormat format, int maxUnits, TimeRounding rounding)
		{
			// Apply Rounding to the TOTAL seconds first to ensure unit cascading works
			// (e.g. 59s -> 60s -> 1m)
			double totalSeconds = t.TotalSeconds;

			if (rounding == TimeRounding.Ceil)
				totalSeconds = Math.Ceiling(totalSeconds);
			else if (rounding == TimeRounding.Nearest)
				totalSeconds = Math.Round(totalSeconds);
			else
				totalSeconds = Math.Floor(totalSeconds);

			// Reconstruct TimeSpan from rounded value
			t = TimeSpan.FromSeconds(totalSeconds);

			if (totalSeconds <= 0) return GetZeroString(format);

			if (format == TimeFormat.Digital)
				return FormatDigital(t);
			if (format == TimeFormat.Stopwatch)
				return $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}.{(t.Milliseconds / 10):D2}";

			return FormatNatural(t, format, maxUnits);
		}

		private static string FormatDigital(TimeSpan t)
		{
			if (t.TotalHours < 1) return $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}";
			if (t.TotalDays < 1) return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
			return $"{t.Days:D2}:{(int)t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
		}

		private static string FormatNatural(TimeSpan t, TimeFormat format, int max)
		{
			StringBuilder sb = new StringBuilder();
			int count = 0;
			bool full = format == TimeFormat.Full;
			string sp = format == TimeFormat.Compact ? "" : " ";

			void Add(int val, string s, string l)
			{
				if (count >= max) return;
				// If we found a value, OR if we are showing at least one unit for a larger magnitude
				if (val > 0)
				{
					sb.Append($"{val}{(full ? (val == 1 ? l.TrimEnd('s') : l) : s)}{sp}");
					count++;
				}
			}

			Add(t.Days, "d", " Days");
			Add(t.Hours, "h", " Hours");
			Add(t.Minutes, "m", " Minutes");

			// Show seconds if we have space, OR if we haven't shown anything yet (e.g. 0m 30s)
			if (count < max && (t.Seconds > 0 || count == 0))
				sb.Append($"{t.Seconds}{(full ? (t.Seconds == 1 ? " Second" : " Seconds") : "s")}");

			return sb.ToString().Trim();
		}

		// -------------------------------------------------------------------------
		// 4. HELPERS
		// -------------------------------------------------------------------------

		private static string GetZeroString(TimeFormat f)
		{
			switch (f)
			{
				case TimeFormat.Digital:   return "00:00";
				case TimeFormat.Stopwatch: return "00:00.00";
				case TimeFormat.Full:      return "0 Seconds";
				default:                   return "0s";
			}
		}

		private static string GetArrivalDescImpl(double seconds)
		{
			var f = DateTime.Now.AddSeconds(seconds);
			var now = DateTime.Now;
			if (f.Date == now.Date) return $"Today at {f:t}";
			if (f.Date == now.AddDays(1).Date) return $"Tomorrow at {f:t}";
			return $"{f:MMM dd} at {f:t}";
		}

		private static string GetRelativeTimeImpl(DateTime past)
		{
			double s = (DateTime.Now - past).TotalSeconds;
			if (s < 60) return "Just now";
			if (s < 3600) return $"{(int)(s / 60)}m ago";
			if (s < 86400) return $"{(int)(s / 3600)}h ago";
			return $"{(int)(s / 86400)}d ago";
		}
	}
}