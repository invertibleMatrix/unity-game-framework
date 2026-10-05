using System;

namespace AK.Kernel.Retry
{
	/// <summary>
	/// When to retry a failing operation, and how many times.
	///
	/// The wait before retry <c>n</c>, counting from 0, is <see cref="BaseDelay"/> times
	/// <see cref="Multiplier"/> to the power <c>n</c>, capped at <see cref="MaxDelay"/>. A
	/// multiplier of 1 keeps the wait constant; 2 doubles it each time. <see cref="Jitter"/>
	/// then takes off a random share of up to that fraction, so operations that failed together
	/// don't all retry at the same moment.
	///
	/// Times are in seconds, on whatever clock the caller waits with.
	/// </summary>
	public readonly struct BackoffPolicy : IEquatable<BackoffPolicy>
	{
		/// <summary>A <see cref="MaxRetries"/> that never runs out.</summary>
		public const int Unlimited = int.MaxValue;

		/// <summary>The wait before the first retry.</summary>
		public readonly double BaseDelay;

		/// <summary>How much each wait grows over the one before. At least 1.</summary>
		public readonly double Multiplier;

		/// <summary>No wait is longer than this, before jitter.</summary>
		public readonly double MaxDelay;

		/// <summary>The largest share of a wait that jitter can take off, from 0 to 1.</summary>
		public readonly double Jitter;

		/// <summary>Retries allowed before giving up. 0 never retries; <see cref="Unlimited"/> never gives up.</summary>
		public readonly int MaxRetries;

		public BackoffPolicy(double baseDelay, double multiplier, double maxDelay, double jitter, int maxRetries)
		{
			// Written as !(x >= y) so that NaN fails too.
			if (!(baseDelay >= 0d))
			{
				throw new ArgumentOutOfRangeException(nameof(baseDelay), baseDelay, "Must be zero or more.");
			}

			if (!(multiplier >= 1d))
			{
				throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "Must be 1 or more.");
			}

			if (!(maxDelay >= 0d))
			{
				throw new ArgumentOutOfRangeException(nameof(maxDelay), maxDelay, "Must be zero or more.");
			}

			if (!(jitter >= 0d && jitter <= 1d))
			{
				throw new ArgumentOutOfRangeException(nameof(jitter), jitter, "Must be between 0 and 1.");
			}

			if (maxRetries < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(maxRetries), maxRetries, "Must be zero or more.");
			}

			BaseDelay  = baseDelay;
			Multiplier = multiplier;
			MaxDelay   = maxDelay;
			Jitter     = jitter;
			MaxRetries = maxRetries;
		}

		/// <summary>The same wait before every retry.</summary>
		public static BackoffPolicy Constant(double delay, int maxRetries, double jitter = 0d) =>
			new(delay, 1d, delay, jitter, maxRetries);

		/// <summary>A wait that starts at <paramref name="baseDelay"/> and multiplies each retry, up to <paramref name="maxDelay"/>.</summary>
		public static BackoffPolicy Exponential(double baseDelay, double maxDelay, int maxRetries, double jitter = 0d, double multiplier = 2d) =>
			new(baseDelay, multiplier, maxDelay, jitter, maxRetries);

		/// <summary>
		/// The wait before retry <paramref name="retry"/>, counting from 0. <paramref name="random"/>
		/// is a uniform sample in [0, 1) and only matters with jitter: 0 gives the full wait.
		/// </summary>
		public double DelayBefore(int retry, double random)
		{
			if (retry < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(retry), retry, "Must be zero or more.");
			}

			if (BaseDelay == 0d)
			{
				return 0d;
			}

			// Pow overflows to infinity long before the retry count runs out; the cap catches it.
			double delay = BaseDelay * Math.Pow(Multiplier, retry);
			if (!(delay <= MaxDelay))
			{
				delay = MaxDelay;
			}

			double share = random > 0d ? Math.Min(random, 1d) : 0d;
			return delay * (1d - Jitter * share);
		}

		public bool Equals(BackoffPolicy other) =>
			BaseDelay.Equals(other.BaseDelay) && Multiplier.Equals(other.Multiplier) && MaxDelay.Equals(other.MaxDelay)
			&& Jitter.Equals(other.Jitter) && MaxRetries == other.MaxRetries;

		public override bool Equals(object obj) => obj is BackoffPolicy other && Equals(other);

		public override int GetHashCode()
		{
			unchecked
			{
				int hash = BaseDelay.GetHashCode();
				hash = hash * 397 ^ Multiplier.GetHashCode();
				hash = hash * 397 ^ MaxDelay.GetHashCode();
				hash = hash * 397 ^ Jitter.GetHashCode();
				return hash * 397 ^ MaxRetries;
			}
		}

		public override string ToString() =>
			$"{BaseDelay:0.###}s x{Multiplier:0.###} up to {MaxDelay:0.###}s, jitter {Jitter:0.##}, "
			+ (MaxRetries == Unlimited ? "unlimited retries" : $"{MaxRetries} retries");
	}
}
