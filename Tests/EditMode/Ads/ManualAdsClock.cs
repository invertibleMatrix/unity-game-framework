using System;
using System.Collections.Generic;
using System.Threading;
using AK.Services.Ads;
using Cysharp.Threading.Tasks;

namespace AK.Tests.Ads
{
	/// <summary>
	/// An <see cref="IAdsClock"/> that only moves when the test calls <see cref="Advance"/>.
	/// Delays complete synchronously inside <see cref="Advance"/>, in the order they fall due.
	/// The wall clock, <see cref="UtcNow"/>, moves with it, from <see cref="Start"/>.
	/// </summary>
	internal sealed class ManualAdsClock : IAdsClock
	{
		private readonly List<Timer> _timers = new();
		private long _sequence;

		/// <summary>
		/// The wall-clock time when no time has been advanced: noon UTC by default. Moving it
		/// moves the wall clock alone, as a change to the device clock does.
		/// </summary>
		public DateTime Start { get; set; } = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

		/// <summary>Seconds advanced so far.</summary>
		public double Now { get; private set; }

		public DateTime UtcNow => Start.AddSeconds(Now);

		/// <summary>Delays still running.</summary>
		public int Pending => _timers.Count;

		/// <summary>Seconds until the next delay is due, or null when none is running.</summary>
		public double? NextDueIn
		{
			get
			{
				Timer next = Earliest(double.PositiveInfinity);
				return next != null ? next.Due - Now : null;
			}
		}

		public UniTask Delay(double seconds, CancellationToken cancellationToken = default)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return UniTask.FromCanceled(cancellationToken);
			}

			if (!(seconds > 0d))
			{
				return UniTask.CompletedTask;
			}

			var timer = new Timer(Now + seconds, _sequence++);
			_timers.Add(timer);

			if (cancellationToken.CanBeCanceled)
			{
				timer.Registration = cancellationToken.Register(() =>
				{
					_timers.Remove(timer);
					timer.Done.TrySetCanceled(cancellationToken);
				});
			}

			return timer.Done.Task;
		}

		/// <summary>
		/// Moves time forward by <paramref name="seconds"/>, completing each delay that falls due,
		/// earliest first. That includes delays started by earlier ones within the window.
		/// </summary>
		public void Advance(double seconds)
		{
			double end = Now + seconds;

			for (Timer next = Earliest(end); next != null; next = Earliest(end))
			{
				_timers.Remove(next);
				Now = next.Due;
				next.Registration.Dispose();
				next.Done.TrySetResult();
			}

			Now = end;
		}

		private Timer Earliest(double limit)
		{
			Timer earliest = null;
			foreach (Timer timer in _timers)
			{
				if (timer.Due <= limit && (earliest == null || timer.Due < earliest.Due || (timer.Due == earliest.Due && timer.Sequence < earliest.Sequence)))
				{
					earliest = timer;
				}
			}

			return earliest;
		}

		private sealed class Timer
		{
			public readonly double Due;
			public readonly long Sequence;
			public readonly UniTaskCompletionSource Done = new();
			public CancellationTokenRegistration Registration;

			public Timer(double due, long sequence)
			{
				Due      = due;
				Sequence = sequence;
			}
		}
	}
}
