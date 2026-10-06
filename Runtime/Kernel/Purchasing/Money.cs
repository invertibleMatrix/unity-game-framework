using System;
using System.Globalization;

namespace AK.Kernel.Purchasing
{
	/// <summary>
	/// An amount of real money in whole minor units of an ISO 4217 currency: cents for USD, yen
	/// for JPY, fils for KWD. Sums and comparisons are exact. <see cref="None"/> has no currency.
	/// </summary>
	public readonly struct Money : IEquatable<Money>
	{
		public static readonly Money None = default;

		/// <summary>The amount, in the currency's minor units.</summary>
		public readonly long MinorUnits;

		/// <summary>The ISO 4217 code, such as "USD"; null for <see cref="None"/>.</summary>
		public readonly string Currency;

		/// <exception cref="ArgumentException">The currency isn't three letters A-Z.</exception>
		public Money(long minorUnits, string currency)
		{
			if (!CurrencyCode.IsValid(currency))
			{
				throw new ArgumentException($"'{currency}' is not an ISO 4217 code: three letters A-Z.", nameof(currency));
			}

			MinorUnits = minorUnits;
			Currency   = currency;
		}

		public bool IsNone => Currency == null;

		/// <summary>Digits after the decimal point in the currency's major unit: 2 for USD, 0 for JPY, 3 for KWD.</summary>
		public int Digits => CurrencyCode.MinorDigits(Currency);

		public bool Equals(Money other) => MinorUnits == other.MinorUnits && string.Equals(Currency, other.Currency, StringComparison.Ordinal);

		public override bool Equals(object obj) => obj is Money other && Equals(other);

		public override int GetHashCode() => MinorUnits.GetHashCode() * 397 ^ (Currency != null ? StringComparer.Ordinal.GetHashCode(Currency) : 0);

		public static bool operator ==(Money a, Money b) => a.Equals(b);
		public static bool operator !=(Money a, Money b) => !a.Equals(b);

		/// <summary>The code and the amount in major units, exactly: "USD 4.99", "JPY 120", "KWD -1.250". "None" without a currency.</summary>
		public override string ToString()
		{
			if (Currency == null) return "None";

			// The magnitude as unsigned, so long.MinValue has one too.
			ulong magnitude = MinorUnits < 0 ? (ulong)-(MinorUnits + 1) + 1 : (ulong)MinorUnits;
			string sign     = MinorUnits < 0 ? "-" : string.Empty;

			int digits = Digits;
			if (digits == 0) return $"{Currency} {sign}{magnitude.ToString(CultureInfo.InvariantCulture)}";

			ulong scale = 1;
			for (int i = 0; i < digits; i++) scale *= 10;

			string major = (magnitude / scale).ToString(CultureInfo.InvariantCulture);
			string minor = (magnitude % scale).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
			return $"{Currency} {sign}{major}.{minor}";
		}
	}

	/// <summary>ISO 4217 currency codes.</summary>
	public static class CurrencyCode
	{
		/// <summary>Whether the code is three letters A-Z. Whether ISO lists it isn't checked.</summary>
		public static bool IsValid(string code)
		{
			return code != null && code.Length == 3 && IsLetter(code[0]) && IsLetter(code[1]) && IsLetter(code[2]);
		}

		/// <summary>
		/// Digits after the decimal point in the currency's major unit, from ISO 4217: 0 for JPY,
		/// 3 for KWD, 4 for CLF. 2 for any code not listed with another number.
		/// </summary>
		public static int MinorDigits(string code)
		{
			switch (code)
			{
				case "BIF": case "CLP": case "DJF": case "GNF": case "ISK": case "JPY":
				case "KMF": case "KRW": case "PYG": case "RWF": case "UGX": case "UYI":
				case "VND": case "VUV": case "XAF": case "XOF": case "XPF":
					return 0;

				case "BHD": case "IQD": case "JOD": case "KWD": case "LYD": case "OMR": case "TND":
					return 3;

				case "CLF": case "UYW":
					return 4;

				default:
					return 2;
			}
		}

		private static bool IsLetter(char c) => c >= 'A' && c <= 'Z';
	}
}
