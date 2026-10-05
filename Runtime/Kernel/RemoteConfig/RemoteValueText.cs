using System;
using System.Globalization;
using System.Text;

namespace AK.Kernel.RemoteConfig
{
	/// <summary>
	/// The text forms of remote config values. Values travel as text: an app's in-app
	/// defaults, a provider's fetched values and a game server's JSON are all strings, so every
	/// client has to write and read them the same way.
	///
	/// <para><b>Numbers</b> use the invariant culture on every device: <c>1.5</c>, never
	/// <c>1,5</c>. Integers take a sign and no fraction; reals also take a fraction and an
	/// exponent. Thousands separators, and NaN and infinities, are refused.</para>
	///
	/// <para><b>Booleans</b> read the words Firebase Remote Config accepts, in any case:
	/// <c>1 true t yes y on</c> and <c>0 false f no n off</c>. They are written as <c>true</c>
	/// and <c>false</c>. Unlike Firebase, empty text is no value, not false.</para>
	///
	/// <para>Whitespace around a value is ignored. Reading allocates nothing, except for a JSON
	/// string that has to be decoded.</para>
	/// </summary>
	public static class RemoteValueText
	{
		private static readonly string[] TrueWords  = { "1", "true", "t", "yes", "y", "on" };
		private static readonly string[] FalseWords = { "0", "false", "f", "no", "n", "off" };

		public static bool TryParseBool(string text, out bool value)
		{
			value = false;
			if (text == null) return false;

			int start = 0, end = text.Length;
			TrimWhitespace(text, ref start, ref end);

			foreach (string word in TrueWords)
			{
				if (IsWord(text, start, end, word))
				{
					value = true;
					return true;
				}
			}

			foreach (string word in FalseWords)
			{
				if (IsWord(text, start, end, word)) return true;
			}

			return false;
		}

		public static bool TryParseInt(string text, out int value) =>
			int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

		public static bool TryParseLong(string text, out long value) =>
			long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

		public static bool TryParseFloat(string text, out float value)
		{
			if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
			    !float.IsNaN(value) && !float.IsInfinity(value))
			{
				return true;
			}

			value = 0f;
			return false;
		}

		public static bool TryParseDouble(string text, out double value)
		{
			if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
			    !double.IsNaN(value) && !double.IsInfinity(value))
			{
				return true;
			}

			value = 0d;
			return false;
		}

		public static string Format(bool value) => value ? "true" : "false";

		public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

		public static string Format(long value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>
		/// Text that reads back as the same float: the shortest the runtime finds, or nine
		/// significant digits when that doesn't read back. NaN and infinities don't read back.
		/// </summary>
		public static string Format(float value)
		{
			string text = value.ToString("R", CultureInfo.InvariantCulture);
			return TryParseFloat(text, out float back) && back.Equals(value) ? text : value.ToString("G9", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Text that reads back as the same double: the shortest the runtime finds, or seventeen
		/// significant digits when that doesn't read back, as .NET Framework's and Mono's "R" can
		/// fail to. NaN and infinities don't read back.
		/// </summary>
		public static string Format(double value)
		{
			string text = value.ToString("R", CultureInfo.InvariantCulture);
			return TryParseDouble(text, out double back) && back.Equals(value) ? text : value.ToString("G17", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// The JSON in a JSON-typed value. Some providers deliver JSON inside a JSON string, such
		/// as <c>"{\"on\":true}"</c>; that string is decoded to the JSON it holds. Any other value
		/// comes back as it is, without surrounding whitespace or a byte order mark. Null stays null.
		///
		/// A string that isn't valid JSON, most often because a quote inside it isn't escaped, is
		/// still decoded, one escape at a time from the left, keeping what can't be decoded.
		/// <paramref name="repaired"/> is then true.
		/// </summary>
		public static string UnwrapJson(string text, out bool repaired)
		{
			repaired = false;
			if (text == null) return null;

			int start = 0, end = text.Length;
			while (start < end && (char.IsWhiteSpace(text[start]) || text[start] == '﻿')) start++;
			while (end > start && char.IsWhiteSpace(text[end - 1])) end--;

			if (end - start < 2 || text[start] != '"' || text[end - 1] != '"')
			{
				return start == 0 && end == text.Length ? text : text.Substring(start, end - start);
			}

			if (TryDecodeString(text, start + 1, end - 1, strict: true, out string decoded)) return decoded;

			repaired = true;
			TryDecodeString(text, start + 1, end - 1, strict: false, out decoded);
			return decoded;
		}

		/// <summary>
		/// Decodes the content of a JSON string, <c>[from, to)</c> between its quotes. Strictly,
		/// to RFC 8259, an unescaped quote or control character, or a bad escape, fails. Leniently,
		/// those are kept as they are.
		/// </summary>
		private static bool TryDecodeString(string text, int from, int to, bool strict, out string value)
		{
			value = null;
			StringBuilder decoded = null;
			int copied = from;

			for (int i = from; i < to; i++)
			{
				char c = text[i];

				if (c == '"' || c < ' ')
				{
					if (strict) return false;
					continue;
				}

				if (c != '\\') continue;

				if (!TryUnescape(text, i, to, out char unescaped, out int length))
				{
					if (strict) return false;
					continue;
				}

				decoded ??= new StringBuilder(to - from);
				decoded.Append(text, copied, i - copied).Append(unescaped);
				i     += length - 1;
				copied = i + 1;
			}

			value = decoded == null
				? text.Substring(from, to - from)
				: decoded.Append(text, copied, to - copied).ToString();
			return true;
		}

		/// <summary>Decodes the escape at <paramref name="backslash"/>, which takes <paramref name="length"/> characters.</summary>
		private static bool TryUnescape(string text, int backslash, int end, out char c, out int length)
		{
			c      = default;
			length = 2;
			if (backslash + 1 >= end) return false;

			switch (text[backslash + 1])
			{
				case '"':  c = '"';  return true;
				case '\\': c = '\\'; return true;
				case '/':  c = '/';  return true;
				case 'b':  c = '\b'; return true;
				case 'f':  c = '\f'; return true;
				case 'n':  c = '\n'; return true;
				case 'r':  c = '\r'; return true;
				case 't':  c = '\t'; return true;
				case 'u':
					if (backslash + 6 > end) return false;

					int code = 0;
					for (int k = 2; k < 6; k++)
					{
						int digit = HexValue(text[backslash + k]);
						if (digit < 0) return false;
						code = code << 4 | digit;
					}

					c      = (char)code;
					length = 6;
					return true;
				default:
					return false;
			}
		}

		private static int HexValue(char c)
		{
			if (c >= '0' && c <= '9') return c - '0';
			if (c >= 'a' && c <= 'f') return c - 'a' + 10;
			if (c >= 'A' && c <= 'F') return c - 'A' + 10;
			return -1;
		}

		/// <summary>Narrows <c>[start, end)</c> past the whitespace that number parsing skips too.</summary>
		private static void TrimWhitespace(string text, ref int start, ref int end)
		{
			while (start < end && IsNumberWhitespace(text[start])) start++;
			while (end > start && IsNumberWhitespace(text[end - 1])) end--;
		}

		private static bool IsNumberWhitespace(char c) => c == ' ' || c >= '\t' && c <= '\r';

		private static bool IsWord(string text, int start, int end, string word) =>
			end - start == word.Length &&
			string.Compare(text, start, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0;
	}
}
