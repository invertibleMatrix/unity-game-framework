using System;

namespace AK.Kernel.Ads
{
	/// <summary>How often ads may show. A limit of zero or less is no limit.</summary>
	public readonly struct FrequencyCaps : IEquatable<FrequencyCaps>
	{
		public static readonly FrequencyCaps None = default;

		/// <summary>The most impressions per session.</summary>
		public readonly int MaxPerSession;

		/// <summary>The most impressions per UTC day.</summary>
		public readonly int MaxPerDay;

		/// <summary>The least time between two impressions, in seconds.</summary>
		public readonly int CooldownSeconds;

		/// <summary>Negative limits are taken as zero, no limit, since they usually come from remote config.</summary>
		public FrequencyCaps(int maxPerSession, int maxPerDay, int cooldownSeconds)
		{
			MaxPerSession   = Math.Max(0, maxPerSession);
			MaxPerDay       = Math.Max(0, maxPerDay);
			CooldownSeconds = Math.Max(0, cooldownSeconds);
		}

		public bool IsNone => MaxPerSession == 0 && MaxPerDay == 0 && CooldownSeconds == 0;

		public bool Equals(FrequencyCaps other) =>
			MaxPerSession == other.MaxPerSession && MaxPerDay == other.MaxPerDay && CooldownSeconds == other.CooldownSeconds;

		public override bool Equals(object obj) => obj is FrequencyCaps other && Equals(other);

		public override int GetHashCode()
		{
			unchecked
			{
				int hash = MaxPerSession;
				hash = hash * 397 ^ MaxPerDay;
				return hash * 397 ^ CooldownSeconds;
			}
		}

		public override string ToString() => $"{MaxPerSession} per session, {MaxPerDay} per day, {CooldownSeconds}s apart";
	}

	/// <summary>Which limit stops another impression.</summary>
	public enum FrequencyCapBlock : byte
	{
		/// <summary>Nothing: another impression is allowed.</summary>
		None = 0,

		/// <summary>The session's impressions reached <see cref="FrequencyCaps.MaxPerSession"/>.</summary>
		SessionCap = 1,

		/// <summary>Today's impressions reached <see cref="FrequencyCaps.MaxPerDay"/>.</summary>
		DailyCap = 2,

		/// <summary>Less than <see cref="FrequencyCaps.CooldownSeconds"/> has passed since the last impression.</summary>
		Cooldown = 3,
	}

	/// <summary>What <see cref="FrequencyCapPolicy"/> keeps across launches for one key.</summary>
	public readonly struct FrequencyCapRecord
	{
		public readonly string Key;

		/// <summary>Impressions on the UTC day of <see cref="LastShownUtc"/>.</summary>
		public readonly int DayCount;

		/// <summary>When the last impression was, in UTC.</summary>
		public readonly DateTime LastShownUtc;

		public FrequencyCapRecord(string key, int dayCount, DateTime lastShownUtc)
		{
			Key          = key;
			DayCount     = dayCount;
			LastShownUtc = lastShownUtc;
		}

		public override string ToString() => $"{Key}: {DayCount} on {LastShownUtc:yyyy-MM-dd}, last at {LastShownUtc:HH:mm:ss}Z";
	}
}
