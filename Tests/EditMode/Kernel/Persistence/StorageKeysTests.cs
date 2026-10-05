using System;
using AK.Kernel.Persistence;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class StorageKeysTests
	{
		[TestCase("UGFW_FACT_LEDGER")]
		[TestCase("remote_config_feature.enabled")]
		[TestCase("a@b")]
		[TestCase(" spaced key ")]
		[TestCase("ключ")]
		public void AnyNonEmptyKeyWithoutControlCharacters_IsValid(string key)
		{
			Assert.IsTrue(StorageKeys.IsValid(key));
			Assert.DoesNotThrow(() => StorageKeys.Validate(key));
		}

		[TestCase("")]
		[TestCase("line\nbreak")]
		[TestCase("tab\tbed")]
		[TestCase("nul\0")]
		[TestCase(StorageKeys.Index, Description = "reserved for the store's own list of keys")]
		public void EmptyControlAndReservedKeys_AreRejected(string key)
		{
			Assert.IsFalse(StorageKeys.IsValid(key));
			Assert.Throws<ArgumentException>(() => StorageKeys.Validate(key));
		}

		[Test]
		public void ANullKey_IsRejected()
		{
			Assert.IsFalse(StorageKeys.IsValid(null));
			Assert.Throws<ArgumentNullException>(() => StorageKeys.Validate(null));
		}

		[Test]
		public void Scoped_AppendsTheScope_AndANullScopeIsTheDevices()
		{
			Assert.AreEqual("UGFW_FACT_LEDGER@player-42", StorageKeys.Scoped("UGFW_FACT_LEDGER", "player-42"));
			Assert.AreEqual("UGFW_FACT_LEDGER", StorageKeys.Scoped("UGFW_FACT_LEDGER", null));
		}

		[TestCase("")]
		[TestCase("a\nb")]
		public void Scoped_RejectsABadScope(string scope)
		{
			Assert.Throws<ArgumentException>(() => StorageKeys.Scoped("K", scope));
		}

		[Test]
		public void Quarantined_NamesTheKeyAndTheUtcMillisecond()
		{
			var utc = new DateTime(2026, 10, 4, 12, 34, 56, 789, DateTimeKind.Utc);

			Assert.AreEqual("K.corrupt.20261004T123456789Z", StorageKeys.Quarantined("K", utc));
		}

		[Test]
		public void Quarantined_TakesALocalTimeAsTheSameInstant()
		{
			var utc = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

			Assert.AreEqual(StorageKeys.Quarantined("K", utc), StorageKeys.Quarantined("K", utc.ToLocalTime()));
		}

		[TestCase("th-TH")]
		[TestCase("ar-SA")]
		public void Quarantined_IsTheSameUnderAnyCulture(string culture)
		{
			var utc = new DateTime(2026, 10, 4, 12, 34, 56, 789, DateTimeKind.Utc);

			using (new CultureScope(culture))
			{
				Assert.AreEqual("K.corrupt.20261004T123456789Z", StorageKeys.Quarantined("K", utc));
			}
		}

		[TestCase("K.corrupt.20261004T123456789Z", true)]
		[TestCase("K.corrupt.20261004T123456789Z-2", true)]
		[TestCase("K.corrupt.20261004T123456789Z-12", true)]
		[TestCase("K.corrupt.20261004T123456789Z-", false)]
		[TestCase("K.corrupt.20261004T123456789Z-x", false)]
		[TestCase("K.corrupt.20261004T123456789Zx", false)]
		[TestCase("K.corrupt.20261004T123456789", false)]
		[TestCase("K.corrupt.2026100XT123456789Z", false)]
		[TestCase("K.corrupt.20261004X123456789Z", false)]
		[TestCase("K.corrupt.20261004T123456789Z.corrupt.20261004T123456789Z", false, Description = "a copy of a copy belongs to the copy's key")]
		[TestCase("KK.corrupt.20261004T123456789Z", false)]
		[TestCase("K", false)]
		public void IsQuarantineOf_MatchesOnlyTheKeysOwnCopies(string candidate, bool expected)
		{
			Assert.AreEqual(expected, StorageKeys.IsQuarantineOf(candidate, "K"));
		}
	}
}
