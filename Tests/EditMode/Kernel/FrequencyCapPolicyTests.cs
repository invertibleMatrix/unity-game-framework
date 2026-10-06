using System;
using System.Collections.Generic;
using AK.Kernel.Ads;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class FrequencyCapPolicyTests
	{
		private static readonly DateTime Noon = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

		[Test]
		public void AKeyWithoutImpressions_IsNeverBlocked()
		{
			var policy = new FrequencyCapPolicy();

			Assert.AreEqual(FrequencyCapBlock.None, policy.Check("a", new FrequencyCaps(1, 1, 60), Noon));
			Assert.AreEqual(0, policy.SessionCount("a"));
			Assert.AreEqual(0, policy.DayCount("a", Noon));
			Assert.AreEqual(TimeSpan.Zero, policy.CooldownRemaining("a", new FrequencyCaps(0, 0, 60), Noon));
			Assert.IsFalse(policy.TryGetLastShown("a", out _));
		}

		[Test]
		public void TheSessionCap_StopsTheImpressionAfterTheLastAllowed()
		{
			var policy = new FrequencyCapPolicy();
			var caps = new FrequencyCaps(2, 0, 0);

			policy.Record("a", Noon);
			Assert.AreEqual(FrequencyCapBlock.None, policy.Check("a", caps, Noon));

			policy.Record("a", Noon.AddMinutes(1));
			Assert.AreEqual(FrequencyCapBlock.SessionCap, policy.Check("a", caps, Noon.AddMinutes(2)));
			Assert.AreEqual(FrequencyCapBlock.None, policy.Check("b", caps, Noon), "each key counts on its own");
		}

		[Test]
		public void TheDailyCap_CountsAUtcDay_AndStartsOverAtUtcMidnight()
		{
			var policy = new FrequencyCapPolicy();
			var caps = new FrequencyCaps(0, 2, 0);
			var lastMinute = new DateTime(2026, 10, 5, 23, 59, 0, DateTimeKind.Utc);

			policy.Record("a", lastMinute.AddMinutes(-5));
			policy.Record("a", lastMinute);
			Assert.AreEqual(FrequencyCapBlock.DailyCap, policy.Check("a", caps, lastMinute));
			Assert.AreEqual(2, policy.DayCount("a", lastMinute));

			DateTime nextDay = lastMinute.AddMinutes(2);
			Assert.AreEqual(FrequencyCapBlock.None, policy.Check("a", caps, nextDay));
			Assert.AreEqual(0, policy.DayCount("a", nextDay));

			policy.Record("a", nextDay);
			Assert.AreEqual(1, policy.DayCount("a", nextDay), "the new day counts from its first impression");
			Assert.AreEqual(3, policy.SessionCount("a"), "the session runs on");
		}

		[Test]
		public void TheCooldown_RunsFromTheLastImpression()
		{
			var policy = new FrequencyCapPolicy();
			var caps = new FrequencyCaps(0, 0, 60);
			policy.Record("a", Noon);

			Assert.AreEqual(FrequencyCapBlock.Cooldown, policy.Check("a", caps, Noon.AddSeconds(59)));
			Assert.AreEqual(TimeSpan.FromSeconds(15), policy.CooldownRemaining("a", caps, Noon.AddSeconds(45)));
			Assert.AreEqual(FrequencyCapBlock.None, policy.Check("a", caps, Noon.AddSeconds(60)));
			Assert.AreEqual(TimeSpan.Zero, policy.CooldownRemaining("a", caps, Noon.AddSeconds(60)));
		}

		[Test]
		public void TheLimitThatStopsAnImpression_IsTheSessionCap_ThenTheDailyCap_ThenTheCooldown()
		{
			var policy = new FrequencyCapPolicy();
			policy.Record("a", Noon);

			Assert.AreEqual(FrequencyCapBlock.SessionCap, policy.Check("a", new FrequencyCaps(1, 1, 60), Noon));
			Assert.AreEqual(FrequencyCapBlock.DailyCap, policy.Check("a", new FrequencyCaps(0, 1, 60), Noon));
			Assert.AreEqual(FrequencyCapBlock.Cooldown, policy.Check("a", new FrequencyCaps(0, 0, 60), Noon));
		}

		[Test]
		public void ZeroAndNegativeLimits_AreNoLimit()
		{
			var policy = new FrequencyCapPolicy();
			for (int i = 0; i < 5; i++)
			{
				policy.Record("a", Noon);
			}

			Assert.AreEqual(FrequencyCapBlock.None, policy.Check("a", FrequencyCaps.None, Noon));
			Assert.AreEqual(FrequencyCapBlock.None, policy.Check("a", new FrequencyCaps(-1, -1, -1), Noon));
			Assert.IsTrue(new FrequencyCaps(-1, -5, -60).IsNone, "negative limits are taken as zero");
		}

		[Test]
		public void AnImpressionStampedLaterThanNow_StartsNoCooldown_ButCountsOnItsOwnDay()
		{
			// The device clock was ahead at the impression, then went back.
			var policy = new FrequencyCapPolicy();
			policy.Record("a", Noon.AddHours(2));

			Assert.AreEqual(FrequencyCapBlock.None, policy.Check("a", new FrequencyCaps(0, 0, 3600), Noon));
			Assert.AreEqual(TimeSpan.Zero, policy.CooldownRemaining("a", new FrequencyCaps(0, 0, 3600), Noon));
			Assert.AreEqual(FrequencyCapBlock.DailyCap, policy.Check("a", new FrequencyCaps(0, 1, 0), Noon), "the same UTC day");
			Assert.AreEqual(FrequencyCapBlock.None, policy.Check("a", new FrequencyCaps(0, 1, 0), Noon.AddDays(-1)), "another UTC day");
		}

		[Test]
		public void TimeOfUnspecifiedKind_IsTakenAsUtc()
		{
			var policy = new FrequencyCapPolicy();

			policy.Record("a", DateTime.SpecifyKind(Noon, DateTimeKind.Unspecified));

			Assert.IsTrue(policy.TryGetLastShown("a", out DateTime lastShown));
			Assert.AreEqual(Noon, lastShown);
			Assert.AreEqual(DateTimeKind.Utc, lastShown.Kind);
		}

		// ------------------------------------------------------------------ across launches

		[Test]
		public void Records_CarryTheDayCountsAndCooldowns_ButNotTheSessions()
		{
			var before = new FrequencyCapPolicy();
			before.Record("a", Noon);
			before.Record("a", Noon.AddMinutes(1));
			before.Record("b", Noon.AddMinutes(2));

			var records = new List<FrequencyCapRecord>();
			before.CopyRecordsTo(records);
			Assert.AreEqual(2, records.Count);

			var after = new FrequencyCapPolicy();
			foreach (FrequencyCapRecord record in records)
			{
				after.Restore(record);
			}

			DateTime now = Noon.AddMinutes(3);
			Assert.AreEqual(2, after.DayCount("a", now));
			Assert.AreEqual(1, after.DayCount("b", now));
			Assert.AreEqual(0, after.SessionCount("a"), "a new session");
			Assert.IsTrue(after.TryGetLastShown("a", out DateTime lastShown));
			Assert.AreEqual(Noon.AddMinutes(1), lastShown);
			Assert.AreEqual(FrequencyCapBlock.Cooldown, after.Check("b", new FrequencyCaps(0, 0, 600), now));
		}

		[Test]
		public void Restore_KeepsTheSessionCount_AndSkipsARecordWithoutAnImpression()
		{
			var policy = new FrequencyCapPolicy();
			policy.Record("a", Noon);

			policy.Restore(new FrequencyCapRecord("a", 5, Noon.AddMinutes(-10)));
			policy.Restore(new FrequencyCapRecord("b", 3, default));
			policy.Restore(new FrequencyCapRecord("c", -2, Noon));

			Assert.AreEqual(1, policy.SessionCount("a"));
			Assert.AreEqual(5, policy.DayCount("a", Noon));
			Assert.IsFalse(policy.TryGetLastShown("b", out _));
			Assert.AreEqual(0, policy.DayCount("c", Noon), "a negative count is taken as zero");
			Assert.AreEqual(2, policy.Count);
		}

		[Test]
		public void RemoveAndClear_ForgetImpressions()
		{
			var policy = new FrequencyCapPolicy();
			policy.Record("a", Noon);
			policy.Record("b", Noon);

			Assert.IsTrue(policy.Remove("a"));
			Assert.IsFalse(policy.Remove("a"));
			Assert.AreEqual(0, policy.SessionCount("a"));

			policy.Clear();
			Assert.AreEqual(0, policy.Count);
			Assert.AreEqual(0, policy.SessionCount("b"));
		}

		[Test]
		public void LocalTime_ANullKey_AndANullCollection_Throw()
		{
			var policy = new FrequencyCapPolicy();
			DateTime local = DateTime.SpecifyKind(Noon, DateTimeKind.Local);

			Assert.Throws<ArgumentException>(() => policy.Record("a", local));
			Assert.Throws<ArgumentException>(() => policy.Check("a", FrequencyCaps.None, local));
			Assert.Throws<ArgumentException>(() => policy.Restore(new FrequencyCapRecord("a", 1, local)));
			Assert.Throws<ArgumentNullException>(() => policy.Record(null, Noon));
			Assert.Throws<ArgumentNullException>(() => policy.Check(null, FrequencyCaps.None, Noon));
			Assert.Throws<ArgumentNullException>(() => policy.CopyRecordsTo(null));
			Assert.AreEqual(0, policy.Count);
		}

		// ------------------------------------------------------------------ cost

		[Test]
		public void CheckingAndRecordingAKnownKey_DoesNotAllocate()
		{
			var policy = new FrequencyCapPolicy();
			var caps = new FrequencyCaps(0, 0, 30);
			policy.Record("a", Noon);

			int Window() => GcAllocations.Count(() =>
			{
				for (int i = 0; i < 1000; i++)
				{
					DateTime now = Noon.AddSeconds(i);
					policy.Check("a", caps, now);
					policy.CooldownRemaining("a", caps, now);
					policy.DayCount("a", now);
					policy.Record("a", now);
				}
			});

			Window();
			Assert.AreEqual(0, Window(), "the steady state allocated");
		}
	}
}
