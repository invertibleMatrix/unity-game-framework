using System;
using AK.Kernel.Purchasing;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class MoneyTests
	{
		[TestCase(499L, "USD", "USD 4.99")]
		[TestCase(5L, "USD", "USD 0.05")]
		[TestCase(-5L, "USD", "USD -0.05")]
		[TestCase(0L, "EUR", "EUR 0.00")]
		[TestCase(120L, "JPY", "JPY 120")]
		[TestCase(-1250L, "KWD", "KWD -1.250")]
		[TestCase(10000L, "CLF", "CLF 1.0000")]
		[TestCase(long.MaxValue, "USD", "USD 92233720368547758.07")]
		[TestCase(long.MinValue, "USD", "USD -92233720368547758.08")]
		[TestCase(long.MinValue, "JPY", "JPY -9223372036854775808")]
		public void ToString_WritesTheExactAmount_InMajorUnits(long minorUnits, string currency, string expected)
		{
			Assert.AreEqual(expected, new Money(minorUnits, currency).ToString());
		}

		[Test]
		public void ToString_IgnoresTheCulture()
		{
			using (new CultureScope("de-DE"))
			{
				Assert.AreEqual("EUR 1234.50", new Money(123450, "EUR").ToString());
			}
		}

		[Test]
		public void None_HasNoCurrency()
		{
			Assert.IsTrue(Money.None.IsNone);
			Assert.IsTrue(default(Money).IsNone);
			Assert.IsNull(Money.None.Currency);
			Assert.AreEqual("None", Money.None.ToString());
			Assert.IsFalse(new Money(0, "USD").IsNone, "a price of nothing is still a price");
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("usd")]
		[TestCase("US")]
		[TestCase("USDT")]
		[TestCase("U$D")]
		public void AMalformedCurrency_IsRefused(string currency)
		{
			Assert.Throws<ArgumentException>(() => new Money(1, currency));
		}

		[Test]
		public void Equality_IsByAmountAndCurrency()
		{
			var price = new Money(499, "USD");

			Assert.AreEqual(price, new Money(499, "USD"));
			Assert.IsTrue(price == new Money(499, "USD"));
			Assert.AreEqual(price.GetHashCode(), new Money(499, "USD").GetHashCode());
			Assert.IsTrue(price != new Money(499, "CAD"));
			Assert.IsTrue(price != new Money(500, "USD"));
			Assert.AreNotEqual(Money.None, new Money(0, "USD"));
		}

		[TestCase("USD", 2)]
		[TestCase("EUR", 2)]
		[TestCase("JPY", 0)]
		[TestCase("KRW", 0)]
		[TestCase("KWD", 3)]
		[TestCase("BHD", 3)]
		[TestCase("CLF", 4)]
		[TestCase("XYZ", 2)]
		public void MinorDigits_FollowISO4217_AndDefaultToTwo(string currency, int digits)
		{
			Assert.AreEqual(digits, CurrencyCode.MinorDigits(currency));
			Assert.AreEqual(digits, new Money(1, currency).Digits);
		}

		[TestCase("USD", true)]
		[TestCase("XYZ", true)]
		[TestCase("usd", false)]
		[TestCase("US", false)]
		[TestCase("\u00DCSD", false)]
		[TestCase(null, false)]
		public void IsValid_TakesThreeCapitalLetters(string currency, bool valid)
		{
			Assert.AreEqual(valid, CurrencyCode.IsValid(currency));
		}
	}
}
