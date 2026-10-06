using System;
using AK.Kernel.Persistence;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class PersistedTimeTests
	{
		private static readonly DateTime Instant = new DateTime(2026, 10, 4, 12, 34, 56, DateTimeKind.Utc).AddTicks(1234567);

		[Test]
		public void AUtcTime_EndsInZ()
		{
			Assert.AreEqual("2026-10-04T12:34:56.1234567Z", PersistedTime.Format(Instant));
		}

		[Test]
		public void ATimeOfUnspecifiedKind_HasNoZ()
		{
			var unspecified = DateTime.SpecifyKind(Instant, DateTimeKind.Unspecified);

			Assert.AreEqual("2026-10-04T12:34:56.1234567", PersistedTime.Format(unspecified));
		}

		[Test]
		public void ALocalTime_CarriesItsOffset_AndReadsBackAsTheSameInstant()
		{
			string text = PersistedTime.Format(Instant.ToLocalTime());

			StringAssert.DoesNotEndWith("Z", text);
			Assert.IsTrue(PersistedTime.TryParse(text, out DateTime utc));
			Assert.AreEqual(Instant, utc);
		}

		[TestCase("2026-10-04T12:34:56.0000000Z", Description = "UGFW's earlier format")]
		[TestCase("2026-10-04T12:34:56Z")]
		[TestCase("2026-10-04T14:34:56.0000000+02:00")]
		[TestCase("2026-10-04T07:34:56-05:00")]
		[TestCase("2026-10-04T12:34:56.0000000", Description = "no zone: taken as UTC")]
		public void TryParse_ReadsIso8601_AsUtc(string text)
		{
			Assert.IsTrue(PersistedTime.TryParse(text, out DateTime utc));
			Assert.AreEqual(new DateTime(2026, 10, 4, 12, 34, 56, DateTimeKind.Utc), utc);
			Assert.AreEqual(DateTimeKind.Utc, utc.Kind);
		}

		[Test]
		public void TryParse_ReadsFractionDigits()
		{
			Assert.IsTrue(PersistedTime.TryParse("2026-10-04T12:34:56.5Z", out DateTime utc));
			Assert.AreEqual(new DateTime(2026, 10, 4, 12, 34, 56, 500, DateTimeKind.Utc), utc);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("not a time")]
		[TestCase("10/04/2026 12:34:56", Description = "a culture's display form")]
		[TestCase("2026-10-04", Description = "a date without a time")]
		[TestCase("2026-13-04T12:34:56Z")]
		public void TryParse_RejectsAnythingElse(string text)
		{
			Assert.IsFalse(PersistedTime.TryParse(text, out DateTime utc));
			Assert.AreEqual(default(DateTime), utc);
		}

		[TestCase("th-TH")]
		[TestCase("ar-SA")]
		public void FormatAndParse_IgnoreTheDevicesCulture(string culture)
		{
			using (new CultureScope(culture))
			{
				string text = PersistedTime.Format(Instant);

				Assert.AreEqual("2026-10-04T12:34:56.1234567Z", text, "a Gregorian year in ASCII digits, not the culture's calendar");
				Assert.IsTrue(PersistedTime.TryParse(text, out DateTime utc));
				Assert.AreEqual(Instant, utc);
			}
		}
	}
}
