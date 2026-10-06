using System;
using System.Collections.Generic;
using AK.Kernel.Retry;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class RetryBackoffTests
	{
		private const double Tolerance = 1e-9;

		// Each case: the policy, then the waits it hands out with no jitter, until it gives up.
		private static IEnumerable<TestCaseData> Schedules()
		{
			yield return new TestCaseData(BackoffPolicy.Constant(10d, 3), new[] { 10d, 10d, 10d })
				.SetName("Constant: the same wait, then gives up");
			yield return new TestCaseData(BackoffPolicy.Exponential(2d, 60d, 7), new[] { 2d, 4d, 8d, 16d, 32d, 60d, 60d })
				.SetName("Exponential: doubles up to the cap");
			yield return new TestCaseData(BackoffPolicy.Exponential(1d, 100d, 4, multiplier: 3d), new[] { 1d, 3d, 9d, 27d })
				.SetName("Exponential: any multiplier");
			yield return new TestCaseData(BackoffPolicy.Exponential(120d, 60d, 2), new[] { 60d, 60d })
				.SetName("Exponential: a base above the cap starts at the cap");
			yield return new TestCaseData(BackoffPolicy.Constant(5d, 0), Array.Empty<double>())
				.SetName("No retries: gives up at once");
			yield return new TestCaseData(BackoffPolicy.Constant(0d, 2), new[] { 0d, 0d })
				.SetName("Zero wait: retries at once");
		}

		[TestCaseSource(nameof(Schedules))]
		public void HandsOutTheSchedule_ThenGivesUp(BackoffPolicy policy, double[] expected)
		{
			var backoff = new RetryBackoff(policy);

			for (int i = 0; i < expected.Length; i++)
			{
				Assert.IsTrue(backoff.TryNext(0d, out double delay), $"retry {i}");
				Assert.AreEqual(expected[i], delay, Tolerance, $"retry {i}");
			}

			Assert.IsTrue(backoff.IsExhausted);
			Assert.IsFalse(backoff.TryNext(0d, out _));
			Assert.AreEqual(expected.Length, backoff.Retries, "a refusal uses nothing");
		}

		[Test]
		public void Reset_StartsAFreshBudget_FromTheFirstWait()
		{
			var backoff = new RetryBackoff(BackoffPolicy.Exponential(2d, 60d, 2));
			backoff.TryNext(0d, out _);
			backoff.TryNext(0d, out _);
			Assert.IsTrue(backoff.IsExhausted);

			backoff.Reset();

			Assert.AreEqual(0, backoff.Retries);
			Assert.IsTrue(backoff.TryNext(0d, out double delay));
			Assert.AreEqual(2d, delay, Tolerance);
		}

		[Test]
		public void Unlimited_NeverGivesUp_AndStaysCapped()
		{
			var backoff = new RetryBackoff(BackoffPolicy.Exponential(1d, 64d, BackoffPolicy.Unlimited));
			double delay = 0d;

			for (int i = 0; i < 5000; i++)
			{
				Assert.IsTrue(backoff.TryNext(0d, out delay));
			}

			Assert.AreEqual(64d, delay, Tolerance, "2^4999 overflows to infinity, which the cap catches");
			Assert.IsFalse(backoff.IsExhausted);
		}

		[TestCase(0d, 10d)]
		[TestCase(0.5d, 9d)]
		[TestCase(0.999999d, 8.00000200000)]
		[TestCase(1d, 8d)]
		[TestCase(-3d, 10d)]
		[TestCase(7d, 8d)]
		public void Jitter_TakesOffUpToItsShare(double random, double expected)
		{
			BackoffPolicy policy = BackoffPolicy.Constant(10d, 1, jitter: 0.2d);

			Assert.AreEqual(expected, policy.DelayBefore(0, random), 1e-6, "out-of-range samples are clamped");
		}

		[Test]
		public void InvalidPolicies_Throw()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new BackoffPolicy(-1d, 1d, 10d, 0d, 1));
			Assert.Throws<ArgumentOutOfRangeException>(() => new BackoffPolicy(double.NaN, 1d, 10d, 0d, 1));
			Assert.Throws<ArgumentOutOfRangeException>(() => new BackoffPolicy(1d, 0.5d, 10d, 0d, 1));
			Assert.Throws<ArgumentOutOfRangeException>(() => new BackoffPolicy(1d, 1d, -1d, 0d, 1));
			Assert.Throws<ArgumentOutOfRangeException>(() => new BackoffPolicy(1d, 1d, 10d, 1.5d, 1));
			Assert.Throws<ArgumentOutOfRangeException>(() => new BackoffPolicy(1d, 1d, 10d, 0d, -1));
			Assert.Throws<ArgumentOutOfRangeException>(() => BackoffPolicy.Constant(1d, 1).DelayBefore(-1, 0d));
		}
	}
}
