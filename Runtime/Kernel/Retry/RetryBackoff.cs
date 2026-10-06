namespace AK.Kernel.Retry
{
	/// <summary>
	/// Counts the retries of one failing operation against a <see cref="BackoffPolicy"/>.
	///
	/// After each failure, <see cref="TryNext"/> gives the wait before the next retry, or false
	/// once the policy's retries are spent. <see cref="Reset"/> starts a fresh budget: after a
	/// success, or whenever a fresh start is worth it, such as the app coming back to the
	/// foreground or a user asking again.
	///
	/// No allocations. Not thread-safe.
	/// </summary>
	public sealed class RetryBackoff
	{
		public RetryBackoff(BackoffPolicy policy)
		{
			Policy = policy;
		}

		public BackoffPolicy Policy { get; }

		/// <summary>Retries handed out since the last reset.</summary>
		public int Retries { get; private set; }

		public bool IsExhausted => Retries >= Policy.MaxRetries;

		/// <summary>
		/// Hands out the next retry: true with its wait, or false when the budget is spent.
		/// <paramref name="random"/> is a uniform sample in [0, 1), used for jitter.
		/// </summary>
		public bool TryNext(double random, out double delay)
		{
			if (IsExhausted)
			{
				delay = 0d;
				return false;
			}

			delay = Policy.DelayBefore(Retries, random);
			Retries++;
			return true;
		}

		public void Reset() => Retries = 0;
	}
}
