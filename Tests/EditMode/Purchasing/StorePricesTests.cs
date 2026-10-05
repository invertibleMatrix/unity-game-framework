using AK.Kernel.Purchasing;
using AK.Services;
using NUnit.Framework;

namespace AK.Tests.Purchasing
{
	public class StorePricesTests
	{
		[TestCase("4.99", "USD", 499L)]
		[TestCase("0", "USD", 0L)]
		[TestCase("120", "JPY", 120L)]
		[TestCase("1.2345", "KWD", 1235L)]
		[TestCase("0.005", "USD", 1L)]
		[TestCase("-0.005", "USD", -1L)]
		[TestCase("0.004", "USD", 0L)]
		[TestCase("119.5", "JPY", 120L)]
		[TestCase("4.99", "usd", 499L)]
		public void APrice_BecomesWholeMinorUnits_RoundedHalfAwayFromZero(string price, string currency, long minorUnits)
		{
			Assert.IsTrue(StorePrices.TryToMoney(decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture), currency, out Money money));
			Assert.AreEqual(minorUnits, money.MinorUnits);
			Assert.AreEqual(currency.ToUpperInvariant(), money.Currency);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("US")]
		[TestCase("US1")]
		[TestCase("DOLLARS")]
		public void AMissingOrMalformedCurrency_GivesNoPrice(string currency)
		{
			Assert.IsFalse(StorePrices.TryToMoney(4.99m, currency, out Money money));
			Assert.IsTrue(money.IsNone);
		}

		[Test]
		public void APriceBeyondTheRangeOfMinorUnits_GivesNoPrice()
		{
			Assert.IsFalse(StorePrices.TryToMoney(decimal.MaxValue, "USD", out Money overflow), "overflows the decimal when scaled");
			Assert.IsTrue(overflow.IsNone);

			Assert.IsFalse(StorePrices.TryToMoney(100_000_000_000_000_000m, "USD", out Money tooBig), "scales past a long");
			Assert.IsTrue(tooBig.IsNone);

			Assert.IsTrue(StorePrices.TryToMoney(92_233_720_368_547_758.07m, "USD", out Money largest));
			Assert.AreEqual(long.MaxValue, largest.MinorUnits);
		}
	}
}
