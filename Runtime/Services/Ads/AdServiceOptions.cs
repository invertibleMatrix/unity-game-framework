using System;

namespace AK.Services.Ads
{
	/// <summary>
	/// How long <see cref="AdService"/> waits for providers and loads, and how it spreads out
	/// retries. Times are in seconds on the service's <see cref="IAdsClock"/>.
	/// </summary>
	public readonly struct AdServiceOptions : IEquatable<AdServiceOptions>
	{
		public static readonly AdServiceOptions Default = new(providerInitSeconds: 10d, showLoadSeconds: 15d, retryJitter: 0.2d);

		/// <summary>
		/// The longest <see cref="AdService.InitializeAsync"/> waits for the providers to start.
		/// A provider that starts later is picked up then and loads its ads.
		/// </summary>
		public readonly double ProviderInitSeconds;

		/// <summary>The longest a show waits for a load when its ad isn't loaded yet.</summary>
		public readonly double ShowLoadSeconds;

		/// <summary>
		/// The largest share of a retry wait taken off at random, from 0 to 1, so clients that
		/// failed together don't retry together.
		/// </summary>
		public readonly double RetryJitter;

		public AdServiceOptions(double providerInitSeconds, double showLoadSeconds, double retryJitter)
		{
			// Written as !(x >= y) so that NaN fails too.
			if (!(providerInitSeconds >= 0d))
			{
				throw new ArgumentOutOfRangeException(nameof(providerInitSeconds), providerInitSeconds, "Must be zero or more.");
			}

			if (!(showLoadSeconds >= 0d))
			{
				throw new ArgumentOutOfRangeException(nameof(showLoadSeconds), showLoadSeconds, "Must be zero or more.");
			}

			if (!(retryJitter >= 0d && retryJitter <= 1d))
			{
				throw new ArgumentOutOfRangeException(nameof(retryJitter), retryJitter, "Must be between 0 and 1.");
			}

			ProviderInitSeconds = providerInitSeconds;
			ShowLoadSeconds     = showLoadSeconds;
			RetryJitter         = retryJitter;
		}

		public bool Equals(AdServiceOptions other) =>
			ProviderInitSeconds.Equals(other.ProviderInitSeconds) && ShowLoadSeconds.Equals(other.ShowLoadSeconds)
			&& RetryJitter.Equals(other.RetryJitter);

		public override bool Equals(object obj) => obj is AdServiceOptions other && Equals(other);

		public override int GetHashCode()
		{
			unchecked
			{
				int hash = ProviderInitSeconds.GetHashCode();
				hash = hash * 397 ^ ShowLoadSeconds.GetHashCode();
				return hash * 397 ^ RetryJitter.GetHashCode();
			}
		}

		public override string ToString() =>
			$"provider init {ProviderInitSeconds:0.###}s, show load {ShowLoadSeconds:0.###}s, retry jitter {RetryJitter:0.##}";
	}
}
