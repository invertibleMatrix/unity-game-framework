using System;
using System.Collections.Generic;

namespace AK.Kernel.Ads
{
	/// <summary>
	/// Counts ad impressions per key, such as a placement or a group of placements, and says
	/// whether <see cref="FrequencyCaps"/> allow another.
	///
	/// <para><b>Sessions</b> last as long as the policy. <b>Days</b> are UTC days: a key's day
	/// count covers the UTC day of its last impression, and starts over on any other day.
	/// <b>Cooldowns</b> run from the last impression on wall-clock time.</para>
	///
	/// <para><b>Clock changes.</b> An impression stamped later than now, because the device
	/// clock went back, starts no cooldown, so a wrong clock can't hold ads back until it catches
	/// up. Its count still applies on its own UTC day.</para>
	///
	/// <para><b>Saving.</b> Session counts aren't kept. <see cref="CopyRecordsTo"/> and
	/// <see cref="Restore"/> carry the rest across launches.</para>
	///
	/// <para>Times are UTC, and passing local time throws. Checks don't allocate. Not thread-safe.</para>
	/// </summary>
	public sealed class FrequencyCapPolicy
	{
		private readonly Dictionary<string, Counter> _counters = new(StringComparer.Ordinal);

		/// <summary>The keys with an impression on record.</summary>
		public int Count => _counters.Count;

		/// <summary>The limit that stops another impression for <paramref name="key"/> now, if any.</summary>
		public FrequencyCapBlock Check(string key, FrequencyCaps caps, DateTime utcNow)
		{
			long now = UtcTicks(utcNow, nameof(utcNow));
			if (!_counters.TryGetValue(KeyOf(key), out Counter counter)) return FrequencyCapBlock.None;

			if (caps.MaxPerSession > 0 && counter.Session >= caps.MaxPerSession) return FrequencyCapBlock.SessionCap;
			if (caps.MaxPerDay > 0 && counter.DayCountOn(now) >= caps.MaxPerDay) return FrequencyCapBlock.DailyCap;
			if (counter.CooldownLeft(caps, now) > 0) return FrequencyCapBlock.Cooldown;

			return FrequencyCapBlock.None;
		}

		/// <summary>How long until the cooldown lets <paramref name="key"/> show again; zero when it already can.</summary>
		public TimeSpan CooldownRemaining(string key, FrequencyCaps caps, DateTime utcNow)
		{
			long now = UtcTicks(utcNow, nameof(utcNow));
			return _counters.TryGetValue(KeyOf(key), out Counter counter)
				? TimeSpan.FromTicks(counter.CooldownLeft(caps, now))
				: TimeSpan.Zero;
		}

		/// <summary>Counts an impression for <paramref name="key"/> at <paramref name="utcNow"/>.</summary>
		public void Record(string key, DateTime utcNow)
		{
			long now = UtcTicks(utcNow, nameof(utcNow));
			key = KeyOf(key);

			if (!_counters.TryGetValue(key, out Counter counter))
			{
				counter = new Counter();
				_counters.Add(key, counter);
			}

			counter.Session++;
			counter.DayCount  = counter.DayCountOn(now) + 1;
			counter.LastShown = now;
		}

		/// <summary>Impressions for <paramref name="key"/> this session.</summary>
		public int SessionCount(string key) =>
			_counters.TryGetValue(KeyOf(key), out Counter counter) ? counter.Session : 0;

		/// <summary>Impressions for <paramref name="key"/> on the UTC day of <paramref name="utcNow"/>.</summary>
		public int DayCount(string key, DateTime utcNow)
		{
			long now = UtcTicks(utcNow, nameof(utcNow));
			return _counters.TryGetValue(KeyOf(key), out Counter counter) ? counter.DayCountOn(now) : 0;
		}

		/// <summary>When <paramref name="key"/>'s last impression was. False when it has had none.</summary>
		public bool TryGetLastShown(string key, out DateTime lastShownUtc)
		{
			if (_counters.TryGetValue(KeyOf(key), out Counter counter) && counter.LastShown > 0)
			{
				lastShownUtc = new DateTime(counter.LastShown, DateTimeKind.Utc);
				return true;
			}

			lastShownUtc = default;
			return false;
		}

		/// <summary>
		/// Brings back a saved day count and last impression. The key's session count, if it has
		/// one, stays. A record without a last impression has nothing to bring back; a negative
		/// day count is taken as zero.
		/// </summary>
		public void Restore(FrequencyCapRecord record)
		{
			long lastShown = UtcTicks(record.LastShownUtc, nameof(record));
			string key = KeyOf(record.Key);
			if (lastShown == 0) return;

			if (!_counters.TryGetValue(key, out Counter counter))
			{
				counter = new Counter();
				_counters.Add(key, counter);
			}

			counter.DayCount  = Math.Max(0, record.DayCount);
			counter.LastShown = lastShown;
		}

		/// <summary>Adds a record for each key with an impression on record, in no particular order.</summary>
		public void CopyRecordsTo(ICollection<FrequencyCapRecord> records)
		{
			if (records == null) throw new ArgumentNullException(nameof(records));

			foreach (KeyValuePair<string, Counter> pair in _counters)
			{
				if (pair.Value.LastShown > 0)
				{
					records.Add(new FrequencyCapRecord(pair.Key, pair.Value.DayCount, new DateTime(pair.Value.LastShown, DateTimeKind.Utc)));
				}
			}
		}

		/// <summary>Forgets <paramref name="key"/>'s impressions. False when it had none.</summary>
		public bool Remove(string key) => _counters.Remove(KeyOf(key));

		/// <summary>Forgets every impression, this session's included.</summary>
		public void Clear() => _counters.Clear();

		private static string KeyOf(string key) => key ?? throw new ArgumentNullException(nameof(key));

		private static long UtcTicks(DateTime time, string parameter)
		{
			if (time.Kind == DateTimeKind.Local)
			{
				throw new ArgumentException("Frequency caps count UTC time; local time was passed.", parameter);
			}

			return time.Ticks;
		}

		private sealed class Counter
		{
			public int Session;

			/// <summary>Impressions on the UTC day of <see cref="LastShown"/>.</summary>
			public int DayCount;

			/// <summary>The last impression, in UTC ticks; zero when there has been none.</summary>
			public long LastShown;

			public int DayCountOn(long now) =>
				LastShown > 0 && LastShown / TimeSpan.TicksPerDay == now / TimeSpan.TicksPerDay ? DayCount : 0;

			/// <summary>Ticks of cooldown left at <paramref name="now"/>. An impression later than now starts none.</summary>
			public long CooldownLeft(FrequencyCaps caps, long now)
			{
				if (caps.CooldownSeconds <= 0 || LastShown <= 0 || LastShown > now) return 0;

				long left = LastShown + caps.CooldownSeconds * TimeSpan.TicksPerSecond - now;
				return left > 0 ? left : 0;
			}
		}
	}
}
