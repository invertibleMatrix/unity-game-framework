using System;
using System.Text;
using AK.Kernel.Analytics;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	/// <summary>
	/// Per-user sampling: the hash every engine must reproduce, and samples that keep whole users
	/// and nest.
	/// </summary>
	public class UserSamplingTests
	{
		// The cross-engine contract. A port that disagrees on any of these puts users in different
		// samples on different engines.
		[TestCase("", 0xAB3E7C0Bu)]
		[TestCase("a", 0x1A80B1B3u)]
		[TestCase("user-1", 0x778F8AECu)]
		[TestCase("player-42", 0x5BDD0682u)]
		[TestCase("d6f2a3c4-5b1e-4f7a-9c8d-0e1f2a3b4c5d", 0x40698819u)]
		[TestCase("\u00fcn\u00ef", 0x36F51365u)]
		[TestCase("\u4e16\u754c", 0x1B335B81u)]
		[TestCase("\U0001F600", 0x3303A80Au)]
		[TestCase("a\uFFFDb", 0xE26229C4u)]
		public void Hash_MatchesTheReferenceVectors(string userId, uint expected)
		{
			Assert.AreEqual(expected, UserSampling.Hash(userId));
		}

		[TestCase("player-42")]
		[TestCase("\u00e9t\u00e9 \u4e16\u754c \U0001F600")]
		public void Hash_IsFnv1aOverUtf8_ThenFmix32(string userId)
		{
			Assert.AreEqual(Reference(userId), UserSampling.Hash(userId));
		}

		// Attribute arguments are stored as UTF-8, which cannot hold a lone surrogate, so these
		// ids live in the test body.
		[Test]
		public void LoneSurrogates_HashAsTheReplacementCharacter()
		{
			Assert.AreEqual(0xE26229C4u, UserSampling.Hash("a\uD800b"));
			Assert.AreEqual(UserSampling.Hash("a\uFFFDb"), UserSampling.Hash("a\uDC00b"));
			Assert.AreEqual(UserSampling.Hash("\uFFFD"), UserSampling.Hash("\uD83D"), "a high surrogate at the end");
			Assert.AreEqual(Reference("lone \uDC00 low and \uD800 high"), UserSampling.Hash("lone \uDC00 low and \uD800 high"));
		}

		[Test]
		public void Bucket_IsTheHashOverTwoToThe32()
		{
			Assert.AreEqual(0x778F8AECu / 4294967296.0, UserSampling.Bucket("user-1"));
			Assert.That(UserSampling.Bucket("user-1"), Is.InRange(0d, 1d));
		}

		[Test]
		public void NullId_Throws()
		{
			Assert.Throws<ArgumentNullException>(() => UserSampling.Hash(null));
			Assert.Throws<ArgumentNullException>(() => UserSampling.Bucket(null));
		}

		[Test]
		public void Includes_TakesBucketsBelowTheRate()
		{
			Assert.IsTrue(UserSampling.Includes(0.25f, 0.2499));
			Assert.IsFalse(UserSampling.Includes(0.25f, 0.25));
			Assert.IsFalse(UserSampling.Includes(0.25f, 0.9));
		}

		[Test]
		public void Includes_FullRateTakesEveryone_ZeroOrNaNTakesNoOne()
		{
			Assert.IsTrue(UserSampling.Includes(1f, 0.9999999));
			Assert.IsTrue(UserSampling.Includes(2f, 0.9999999));
			Assert.IsFalse(UserSampling.Includes(0f, 0d));
			Assert.IsFalse(UserSampling.Includes(-1f, 0d));
			Assert.IsFalse(UserSampling.Includes(float.NaN, 0d));
		}

		[Test]
		public void Samples_HoldTheirShareOfUsers_AndNest()
		{
			int inQuarter = 0;
			int inTenth = 0;
			for (int i = 0; i < 10000; i++)
			{
				double bucket = UserSampling.Bucket("user-" + i);
				bool quarter = UserSampling.Includes(0.25f, bucket);
				bool tenth = UserSampling.Includes(0.1f, bucket);
				if (tenth)
				{
					Assert.IsTrue(quarter, "user-" + i + " is in the 10% sample but not the 25% one");
				}

				inQuarter += quarter ? 1 : 0;
				inTenth += tenth ? 1 : 0;
			}

			Assert.AreEqual(2435, inQuarter);
			Assert.AreEqual(947, inTenth);
		}

		[Test]
		public void Hash_DoesNotAllocate()
		{
			const string userId = "d6f2a3c4-5b1e-4f7a-9c8d-0e1f2a3b4c5d \u00fc \U0001F600";
			uint hash = 0;
			int Window() => GcAllocations.Count(() =>
			{
				for (int i = 0; i < 100; i++)
				{
					hash ^= UserSampling.Hash(userId);
				}
			});

			Window();
			Assert.AreEqual(0, Window());
			Assert.AreEqual(0u, hash, "an even number of equal hashes cancels out");
		}

		// The spec, written the slow way: the .NET UTF-8 encoder's bytes, then FNV-1a and fmix32.
		private static uint Reference(string userId)
		{
			uint hash = 2166136261u;
			foreach (byte b in Encoding.UTF8.GetBytes(userId))
			{
				hash = unchecked((hash ^ b) * 16777619u);
			}

			unchecked
			{
				hash ^= hash >> 16;
				hash *= 0x85EBCA6Bu;
				hash ^= hash >> 13;
				hash *= 0xC2B2AE35u;
				hash ^= hash >> 16;
			}

			return hash;
		}
	}
}
