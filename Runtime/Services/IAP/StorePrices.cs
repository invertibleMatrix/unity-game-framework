using System;
using AK.Kernel.Purchasing;

namespace AK.Services
{
	/// <summary>Turns the decimal prices stores report into <see cref="Money"/>.</summary>
	public static class StorePrices
	{
		/// <summary>
		/// The price in whole minor units of its currency, rounded half away from zero: 4.99 USD
		/// is 499, 120 JPY is 120. A lower-case code is read as upper case. False for a missing or
		/// malformed code, or a price beyond the range of minor units.
		/// </summary>
		public static bool TryToMoney(decimal price, string currency, out Money money)
		{
			money = Money.None;

			if (currency != null && currency.Length == 3 && !CurrencyCode.IsValid(currency))
			{
				currency = currency.ToUpperInvariant();
			}

			if (!CurrencyCode.IsValid(currency)) return false;

			decimal scale = 1m;
			for (int digits = CurrencyCode.MinorDigits(currency); digits > 0; digits--) scale *= 10m;

			decimal minor;
			try
			{
				minor = decimal.Round(price * scale, MidpointRounding.AwayFromZero);
			}
			catch (OverflowException)
			{
				return false;
			}

			if (minor < long.MinValue || minor > long.MaxValue) return false;

			money = new Money((long)minor, currency);
			return true;
		}
	}
}
