using System;
using AK.Utilities;

namespace AK.Tests.Utilities
{
	/// <summary>Game time, real time and a wall clock that a test sets by hand, all starting at zero seconds.</summary>
	internal sealed class ManualTimerClock : ITimerClock
	{
		public static readonly DateTime Origin = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

		public double GameTime { get; set; }

		public double RealTime { get; set; }

		public DateTime UtcNow { get; set; } = Origin;

		/// <summary>Sets every clock to <paramref name="seconds"/> past its start.</summary>
		public void Set(double seconds)
		{
			GameTime = seconds;
			RealTime = seconds;
			UtcNow   = Origin.AddTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond));
		}

		/// <summary>Moves every clock on by <paramref name="seconds"/>.</summary>
		public void Advance(double seconds)
		{
			GameTime += seconds;
			RealTime += seconds;
			UtcNow   =  UtcNow.AddTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond));
		}
	}
}
