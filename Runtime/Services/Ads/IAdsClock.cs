using System;
using System.Threading;
using AK.Core.Extensions;
using AK.Kernel.Timing;
using Cysharp.Threading.Tasks;

namespace AK.Services.Ads
{
	/// <summary>
	/// The time ads run on: the waits for retries and reloads, the watchdogs that bound loads
	/// and shows, and the wall-clock time that frequency caps count days and cooldowns in.
	/// Injected so tests can drive time by hand.
	/// </summary>
	public interface IAdsClock
	{
		/// <summary>The current wall-clock time, in UTC (<see cref="DateTimeKind.Utc"/>).</summary>
		DateTime UtcNow { get; }

		/// <summary>
		/// Completes after <paramref name="seconds"/> of this clock's time, at once for zero or
		/// less. Cancelling ends it as cancelled.
		/// </summary>
		UniTask Delay(double seconds, CancellationToken cancellationToken = default);
	}

	/// <summary>
	/// Waits count unscaled time: real time, frame by frame, while the app runs in the
	/// foreground (<see cref="TimeDomainExt.Delay"/>). Time scale doesn't slow them, and time
	/// spent suspended doesn't count: a frame adds at most <see cref="MaxFrameGapSeconds"/>. So a
	/// watchdog doesn't run out while the app is in the background, or while a fullscreen ad has
	/// the player loop paused, and the ad's own callbacks still get their turn when the app comes
	/// back. <see cref="UtcNow"/> is the system clock.
	/// </summary>
	public sealed class ForegroundAdsClock : IAdsClock
	{
		/// <summary>The most time one frame counts for: <see cref="ForegroundTime.MaxFrameSeconds"/>.</summary>
		public const double MaxFrameGapSeconds = ForegroundTime.MaxFrameSeconds;

		public DateTime UtcNow => DateTime.UtcNow;

		public UniTask Delay(double seconds, CancellationToken cancellationToken = default) =>
			TimeDomain.Unscaled.Delay(seconds, cancellationToken);
	}
}
