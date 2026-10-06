using System;
using System.Globalization;

namespace AK.Kernel.Formatting
{
	/// <summary>
	/// Writes numbers short, with a suffix for each power of a thousand, and reads them back:
	/// 1500 is "1.5K" and 2,500,000 is "2.5M".
	///
	/// <para><b>Digits.</b> A long is taken exactly. A double is taken to fifteen significant
	/// digits and a float to seven, the most each type holds for every value, so 0.1 + 0.2 is
	/// taken as 0.3. Rounding works on those decimal digits, never on binary fractions: 1100
	/// rounded up to two decimals is 1.1K, not 1.11K.</para>
	///
	/// <para><b>Suffixes.</b> A value is rounded before its suffix is picked, so 999,950 at one
	/// decimal is "1M", never "1000K". A value that rounds to zero is "0", without a sign.</para>
	///
	/// <para><b>Text</b> is the invariant culture's on every device: a point before the
	/// decimals, no thousands separators, and a minus sign. NaN and infinities are written as the
	/// invariant culture writes them: "NaN", "Infinity" and "-Infinity".</para>
	///
	/// <para>Formatting into a span allocates nothing, and neither does parsing a number of up to
	/// 256 characters.</para>
	/// </summary>
	public static class CompactNumber
	{
		/// <summary>
		/// The longest text any format writes: a double past Q, with fifteen decimals. 294 digits
		/// before the point, plus a sign, the point, the decimals and the suffix.
		/// </summary>
		public const int MaxLength = 312;

		private const int ShortScaleLastGroup = 5;
		private const int LettersLastGroup    = 4 + 26 * 26;

		private const string ShortScaleSuffixes = "KMBTQ";

		/// <summary>The size of a long that is just out of range, as a ulong: 2^63.</summary>
		private const ulong LongLimit = 9_223_372_036_854_775_808UL;

		public static bool TryFormat(long value, CompactNumberFormat format, Span<char> destination, out int charsWritten)
		{
			Span<byte> digits = stackalloc byte[DigitCapacity];
			var number = new DecimalDigits(digits);
			number.SetLong(value);
			return TryWrite(ref number, format, destination, out charsWritten);
		}

		public static bool TryFormat(double value, CompactNumberFormat format, Span<char> destination, out int charsWritten)
		{
			if (double.IsNaN(value) || double.IsInfinity(value))
			{
				return TryWriteNonFinite(value, destination, out charsWritten);
			}

			Span<byte> digits = stackalloc byte[DigitCapacity];
			var number = new DecimalDigits(digits);
			number.SetReal(value, DoubleDigits);
			return TryWrite(ref number, format, destination, out charsWritten);
		}

		public static bool TryFormat(float value, CompactNumberFormat format, Span<char> destination, out int charsWritten)
		{
			if (float.IsNaN(value) || float.IsInfinity(value))
			{
				return TryWriteNonFinite(value, destination, out charsWritten);
			}

			Span<byte> digits = stackalloc byte[DigitCapacity];
			var number = new DecimalDigits(digits);
			number.SetReal(value, FloatDigits);
			return TryWrite(ref number, format, destination, out charsWritten);
		}

		public static string Format(long value, CompactNumberFormat format)
		{
			Span<char> text = stackalloc char[MaxLength];
			TryFormat(value, format, text, out int written);
			return new string(text.Slice(0, written));
		}

		public static string Format(double value, CompactNumberFormat format)
		{
			Span<char> text = stackalloc char[MaxLength];
			TryFormat(value, format, text, out int written);
			return new string(text.Slice(0, written));
		}

		public static string Format(float value, CompactNumberFormat format)
		{
			Span<char> text = stackalloc char[MaxLength];
			TryFormat(value, format, text, out int written);
			return new string(text.Slice(0, written));
		}

		/// <summary>
		/// Reads a number with an optional suffix, such as "1.5K", "-2M" or "3.25aa". The number
		/// is digits with an optional sign and point; no exponent or thousands separators. Suffixes
		/// are matched in any case, and whitespace around the number and before the suffix is
		/// ignored. The result is the double nearest the text's value; false when it isn't a
		/// number or is too large for a double.
		/// </summary>
		public static bool TryParseDouble(ReadOnlySpan<char> text, NumberSuffixes suffixes, out double value)
		{
			value = 0d;
			if (!TryReadParts(text, suffixes, out ReadOnlySpan<char> number, out int group)) return false;

			// Hand the runtime the number with its exponent, so it rounds once: "1.5aa" reads as "1.5E15".
			Span<char> scientific = number.Length <= 256 ? stackalloc char[number.Length + 6] : new char[number.Length + 6];
			number.CopyTo(scientific);

			int length = number.Length;
			scientific[length++] = 'E';
			length += WriteWhole(3 * group, scientific.Slice(length));

			const NumberStyles style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
			if (!double.TryParse(scientific.Slice(0, length), style, CultureInfo.InvariantCulture, out double parsed) || double.IsInfinity(parsed))
			{
				return false;
			}

			value = parsed;
			return true;
		}

		/// <summary>
		/// Reads a number as <see cref="TryParseDouble"/> does, exactly, and rounds it to a whole
		/// number, halves away from zero: "1.2345K" is 1235. False when it isn't a number or is
		/// out of the range of a long.
		/// </summary>
		public static bool TryParseLong(ReadOnlySpan<char> text, NumberSuffixes suffixes, out long value)
		{
			value = 0L;
			if (!TryReadParts(text, suffixes, out ReadOnlySpan<char> number, out int group)) return false;

			bool negative = number[0] == '-';
			int start = number[0] == '-' || number[0] == '+' ? 1 : 0;

			// Digits after the point that still land before it once the suffix moves it right.
			int shift = 3 * group;
			bool afterPoint = false;
			bool roundUp = false;
			bool dropping = false;
			ulong magnitude = 0;

			for (int i = start; i < number.Length; i++)
			{
				char c = number[i];
				if (c == '.')
				{
					afterPoint = true;
					continue;
				}

				uint digit = (uint)(c - '0');
				if (afterPoint && shift == 0)
				{
					// The first digit past the units decides the rounding; the rest can't change it.
					if (!dropping) roundUp = digit >= 5;
					dropping = true;
					continue;
				}

				if (afterPoint) shift--;
				if (magnitude > (LongLimit - digit) / 10) return false;
				magnitude = magnitude * 10 + digit;
			}

			for (; shift > 0; shift--)
			{
				if (magnitude > LongLimit / 10) return false;
				magnitude *= 10;
			}

			if (roundUp)
			{
				if (magnitude == LongLimit) return false;
				magnitude++;
			}

			if (magnitude > (negative ? LongLimit : LongLimit - 1)) return false;

			value = negative ? unchecked(-(long)magnitude) : (long)magnitude;
			return true;
		}

		// ------------------------------------------------------------------ writing

		/// <summary>Significant digits taken from a double and a float.</summary>
		private const int DoubleDigits = 15;
		private const int FloatDigits  = 7;

		/// <summary>Room for a long's nineteen digits and a carry.</summary>
		private const int DigitCapacity = 24;

		private static bool TryWrite(ref DecimalDigits number, in CompactNumberFormat format, Span<char> destination, out int charsWritten)
		{
			int lastGroup = format.Suffixes == NumberSuffixes.ShortScale ? ShortScaleLastGroup : LettersLastGroup;

			// The power of a thousand that leaves one to three digits before the point.
			int group = number.Count == 0 || number.Point <= 3 ? 0 : Math.Min((number.Point - 1) / 3, lastGroup);
			bool whole = group == 0 && format.WholeBelowThousand;

			number.Point -= 3 * group;
			number.Round(whole ? 0 : format.MaxDecimals, format.Rounding);

			if (number.Point > 3 && group < lastGroup)
			{
				// It rounded up to exactly a thousand of this group: one of the next.
				group++;
				whole = false;
				number.Point -= 3;
			}

			int decimals = whole ? 0 : Math.Max(format.MinDecimals, number.Count - number.Point);
			int integerDigits = Math.Max(number.Point, 1);
			int suffixLength = group == 0 ? 0 : group <= 4 || format.Suffixes == NumberSuffixes.ShortScale ? 1 : 2;
			int length = (number.Negative ? 1 : 0) + integerDigits + (decimals > 0 ? 1 + decimals : 0) + suffixLength;

			if (destination.Length < length)
			{
				charsWritten = 0;
				return false;
			}

			int at = 0;
			if (number.Negative) destination[at++] = '-';

			if (number.Point <= 0)
			{
				destination[at++] = '0';
			}
			else
			{
				for (int place = 0; place < number.Point; place++)
				{
					destination[at++] = number.DigitAt(place);
				}
			}

			if (decimals > 0)
			{
				destination[at++] = '.';
				for (int place = number.Point; place < number.Point + decimals; place++)
				{
					destination[at++] = number.DigitAt(place);
				}
			}

			if (suffixLength == 1)
			{
				destination[at++] = ShortScaleSuffixes[group - 1];
			}
			else if (suffixLength == 2)
			{
				destination[at++] = (char)('a' + (group - 5) / 26);
				destination[at++] = (char)('a' + (group - 5) % 26);
			}

			charsWritten = at;
			return true;
		}

		private static bool TryWriteNonFinite(double value, Span<char> destination, out int charsWritten)
		{
			string text = double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity";
			if (!text.AsSpan().TryCopyTo(destination))
			{
				charsWritten = 0;
				return false;
			}

			charsWritten = text.Length;
			return true;
		}

		/// <summary>Writes a non-negative whole number; returns the characters written.</summary>
		private static int WriteWhole(int value, Span<char> destination)
		{
			int length = 1;
			for (int rest = value / 10; rest > 0; rest /= 10) length++;

			for (int i = length - 1; i >= 0; i--)
			{
				destination[i] = (char)('0' + value % 10);
				value /= 10;
			}

			return length;
		}

		// ------------------------------------------------------------------ reading

		/// <summary>
		/// Splits text into the number, with its sign, and the power of a thousand its suffix
		/// stands for. False when either part is malformed.
		/// </summary>
		private static bool TryReadParts(ReadOnlySpan<char> text, NumberSuffixes suffixes, out ReadOnlySpan<char> number, out int group)
		{
			number = default;
			group  = 0;

			text = Trim(text);
			int at = 0;
			if (at < text.Length && (text[at] == '-' || text[at] == '+')) at++;

			int digits = 0;
			while (at < text.Length && IsDigit(text[at]))
			{
				at++;
				digits++;
			}

			if (at < text.Length && text[at] == '.')
			{
				at++;
				while (at < text.Length && IsDigit(text[at]))
				{
					at++;
					digits++;
				}
			}

			if (digits == 0) return false;

			number = text.Slice(0, at);
			ReadOnlySpan<char> suffix = Trim(text.Slice(at));
			return suffix.IsEmpty || TryReadSuffix(suffix, suffixes, out group);
		}

		private static bool TryReadSuffix(ReadOnlySpan<char> suffix, NumberSuffixes suffixes, out int group)
		{
			group = 0;

			if (suffix.Length == 1)
			{
				int index = ShortScaleSuffixes.IndexOf(char.ToUpperInvariant(suffix[0]));
				int last = suffixes == NumberSuffixes.ShortScale ? ShortScaleLastGroup : 4;
				if (index < 0 || index + 1 > last) return false;

				group = index + 1;
				return true;
			}

			if (suffix.Length == 2 && suffixes == NumberSuffixes.Letters)
			{
				int first  = char.ToLowerInvariant(suffix[0]) - 'a';
				int second = char.ToLowerInvariant(suffix[1]) - 'a';
				if ((uint)first >= 26 || (uint)second >= 26) return false;

				group = 5 + first * 26 + second;
				return true;
			}

			return false;
		}

		private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> text)
		{
			int start = 0, end = text.Length;
			while (start < end && IsWhitespace(text[start])) start++;
			while (end > start && IsWhitespace(text[end - 1])) end--;
			return text.Slice(start, end - start);
		}

		private static bool IsDigit(char c) => c >= '0' && c <= '9';

		private static bool IsWhitespace(char c) => c == ' ' || c >= '\t' && c <= '\r';

		// ------------------------------------------------------------------ digits

		/// <summary>
		/// A finite number as decimal digits: the value is 0.d₁d₂…dₙ × 10^<see cref="Point"/>, with
		/// no leading or trailing zeros among the digits. Zero has no digits and no sign.
		/// </summary>
		private ref struct DecimalDigits
		{
			private readonly Span<byte> _digits;

			/// <summary>Significant digits held.</summary>
			public int Count;

			/// <summary>Where the point goes: the number of digits before it, which may be zero or negative.</summary>
			public int Point;

			public bool Negative;

			public DecimalDigits(Span<byte> digits)
			{
				_digits  = digits;
				Count    = 0;
				Point    = 0;
				Negative = false;
			}

			/// <summary>The digit at <paramref name="place"/>, counting from the first significant one, or '0' outside them.</summary>
			public char DigitAt(int place) => place >= 0 && place < Count ? (char)('0' + _digits[place]) : '0';

			public void SetLong(long value)
			{
				Negative = value < 0;
				ulong magnitude = Negative ? unchecked((ulong)(-(value + 1)) + 1UL) : (ulong)value;

				int length = 0;
				for (ulong rest = magnitude; rest > 0; rest /= 10) length++;

				for (int i = length - 1; i >= 0; i--)
				{
					_digits[i] = (byte)(magnitude % 10);
					magnitude /= 10;
				}

				Count = length;
				Point = length;
				TrimZeros();
			}

			/// <summary>Takes a finite value to <paramref name="significant"/> digits, correctly rounded.</summary>
			public void SetReal(double value, int significant)
			{
				// "E14" writes fifteen significant digits: -d.ddddddddddddddE+ddd.
				Span<char> text = stackalloc char[32];
				value.TryFormat(text, out int written, significant == DoubleDigits ? "E14" : "E6", CultureInfo.InvariantCulture);

				int at = 0;
				Negative = text[0] == '-';
				if (Negative) at++;

				Count = 0;
				for (; text[at] != 'E'; at++)
				{
					if (text[at] != '.') _digits[Count++] = (byte)(text[at] - '0');
				}

				at++;
				bool negativeExponent = text[at] == '-';
				int exponent = 0;
				for (at++; at < written; at++)
				{
					exponent = exponent * 10 + (text[at] - '0');
				}

				Point = (negativeExponent ? -exponent : exponent) + 1;
				TrimZeros();
			}

			/// <summary>
			/// Rounds to <paramref name="decimals"/> digits after the point. The size of the value
			/// grows by one unit at the last place kept, or doesn't, as <paramref name="rounding"/> says.
			/// </summary>
			public void Round(int decimals, NumberRounding rounding)
			{
				int keep = Point + decimals;
				if (Count <= keep) return;

				// Digits are dropped, and they aren't all zero: trailing zeros are trimmed.
				bool grow = rounding switch
				{
					NumberRounding.Down => Negative,
					NumberRounding.Up   => !Negative,
					_                   => keep >= 0 && _digits[keep] >= 5,
				};

				if (keep <= 0)
				{
					// Nothing is kept: one unit at the last place, or zero.
					if (grow)
					{
						_digits[0] = 1;
						Count      = 1;
						Point      = 1 - decimals;
					}
					else
					{
						Count = 0;
						TrimZeros();
					}

					return;
				}

				Count = keep;
				if (grow)
				{
					int place = keep - 1;
					while (place >= 0 && _digits[place] == 9)
					{
						_digits[place] = 0;
						place--;
					}

					if (place >= 0)
					{
						_digits[place]++;
					}
					else
					{
						// All nines: 99.95 → 100.0.
						_digits[0] = 1;
						Count      = 1;
						Point++;
					}
				}

				TrimZeros();
			}

			private void TrimZeros()
			{
				while (Count > 0 && _digits[Count - 1] == 0) Count--;

				if (Count == 0)
				{
					Point    = 0;
					Negative = false;
				}
			}
		}
	}
}
