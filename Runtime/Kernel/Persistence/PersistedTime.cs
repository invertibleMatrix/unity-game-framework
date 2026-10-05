using System;
using System.Globalization;

namespace AK.Kernel.Persistence
{
	/// <summary>
	/// The text form of times in saved data: ISO 8601 with seven fraction digits (.NET's "O"),
	/// written and read with the invariant culture. The device's culture never changes what is
	/// saved or how it reads back; a Thai device, for one, would otherwise format the Buddhist
	/// year 2569.
	/// </summary>
	public static class PersistedTime
	{
		// "K" reads Z, an offset, or nothing; "FFFFFFF" reads zero to seven fraction digits.
		private static readonly string[] Formats =
		{
			"yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
			"yyyy-MM-dd'T'HH:mm:ssK",
		};

		/// <summary>
		/// <paramref name="time"/> as text: a UTC time ends in <c>Z</c>, a local time in its
		/// offset, and a time of unspecified kind in neither.
		/// </summary>
		public static string Format(DateTime time) => time.ToString("O", CultureInfo.InvariantCulture);

		/// <summary>
		/// Reads an ISO 8601 date and time as UTC. A time with an offset is converted; a time
		/// without one is taken as UTC. False, with <paramref name="utc"/> left default, for
		/// anything else.
		/// </summary>
		public static bool TryParse(string text, out DateTime utc)
		{
			if (!string.IsNullOrEmpty(text) &&
			    DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture,
			                           DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc))
			{
				return true;
			}

			utc = default;
			return false;
		}
	}
}
