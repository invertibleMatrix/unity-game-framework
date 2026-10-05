using System;
using System.Collections.Generic;
using System.Threading;
using AK.CoreDomain.Ads;
using AK.Kernel.Ads;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Services.Ads
{
	/// <summary>
	/// The calls a fullscreen ad SDK takes (rewarded, interstitial and app-open ads), for a
	/// <see cref="FullscreenAdDriver"/>. Each provider implements it over its SDK. Called on the
	/// main thread.
	/// </summary>
	public interface IFullscreenAdSdk
	{
		/// <summary>Whether the SDK has an ad loaded for the unit.</summary>
		bool IsLoaded(AdType adType, string adUnitId);

		/// <summary>Starts loading the unit. The SDK answers through the driver's load callbacks.</summary>
		void Load(AdType adType, string adUnitId);

		/// <summary>Shows the unit's loaded ad. The SDK answers through the driver's show callbacks.</summary>
		void Show(AdType adType, string adUnitId, string placementId);
	}

	/// <summary>How long a <see cref="FullscreenAdDriver"/> waits for the SDK, in seconds of its clock.</summary>
	public readonly struct FullscreenAdTimeouts : IEquatable<FullscreenAdTimeouts>
	{
		public static readonly FullscreenAdTimeouts Default = new(load: 60d, display: 15d, close: 180d, rewardGrace: 1d);

		/// <summary>For a load to report back.</summary>
		public readonly double Load;

		/// <summary>For an ad asked to show to report reaching the screen.</summary>
		public readonly double Display;

		/// <summary>For an ad on screen to report closing: a rewarded video, its end card and a player taking their time.</summary>
		public readonly double Close;

		/// <summary>
		/// For the reward after a rewarded ad closed without it. Networks don't agree on whether
		/// the reward or the close comes first.
		/// </summary>
		public readonly double RewardGrace;

		public FullscreenAdTimeouts(double load, double display, double close, double rewardGrace)
		{
			// Written as !(x > y) so that NaN fails too.
			if (!(load > 0d))
			{
				throw new ArgumentOutOfRangeException(nameof(load), load, "Must be more than zero.");
			}

			if (!(display > 0d))
			{
				throw new ArgumentOutOfRangeException(nameof(display), display, "Must be more than zero.");
			}

			if (!(close > 0d))
			{
				throw new ArgumentOutOfRangeException(nameof(close), close, "Must be more than zero.");
			}

			if (!(rewardGrace >= 0d))
			{
				throw new ArgumentOutOfRangeException(nameof(rewardGrace), rewardGrace, "Must be zero or more.");
			}

			Load        = load;
			Display     = display;
			Close       = close;
			RewardGrace = rewardGrace;
		}

		public bool Equals(FullscreenAdTimeouts other) =>
			Load.Equals(other.Load) && Display.Equals(other.Display) && Close.Equals(other.Close) && RewardGrace.Equals(other.RewardGrace);

		public override bool Equals(object obj) => obj is FullscreenAdTimeouts other && Equals(other);

		public override int GetHashCode()
		{
			unchecked
			{
				int hash = Load.GetHashCode();
				hash = hash * 397 ^ Display.GetHashCode();
				hash = hash * 397 ^ Close.GetHashCode();
				return hash * 397 ^ RewardGrace.GetHashCode();
			}
		}

		public override string ToString() =>
			$"load {Load:0.###}s, display {Display:0.###}s, close {Close:0.###}s, reward grace {RewardGrace:0.###}s";
	}

	/// <summary>
	/// Runs a fullscreen ad SDK's loads and shows for an <see cref="IAdProvider"/>, so that
	/// every provider keeps the same promises:
	/// <list type="bullet">
	/// <item>One load per unit at a time. Asking again joins it.</item>
	/// <item>Every load and show completes, whatever the SDK does. A load that never reports
	/// back fails with <see cref="AdErrorType.Timeout"/>, and so does a show that never
	/// reaches the screen or never reports closing. A show that earned its reward keeps it.</item>
	/// <item>One show at a time, resolved by <see cref="AdShowResolver"/> from the callbacks in
	/// whatever order they arrive. A reward is granted at most once, and only to the show
	/// waiting for it.</item>
	/// </list>
	/// The provider forwards its SDK's callbacks to the <c>On…</c> methods on the main thread;
	/// <see cref="AK.Core.Threading.MainThreadInbox"/> gets them there in order. Main thread only.
	/// </summary>
	public sealed class FullscreenAdDriver : IDisposable
	{
		private readonly IFullscreenAdSdk _sdk;
		private readonly IAdsClock _clock;
		private readonly FullscreenAdTimeouts _timeouts;
		private readonly string _networkName;
		private readonly string _tag;
		private readonly Dictionary<string, PendingLoad> _loads = new();
		private readonly HashSet<string> _requested = new();
		private ActiveShow _show;
		private bool _disposed;

		/// <param name="networkName">Reported as <see cref="AdResult.NetworkName"/> when the SDK names no network.</param>
		public FullscreenAdDriver(IFullscreenAdSdk sdk, IAdsClock clock, FullscreenAdTimeouts timeouts, string networkName)
		{
			_sdk         = sdk ?? throw new ArgumentNullException(nameof(sdk));
			_clock       = clock ?? throw new ArgumentNullException(nameof(clock));
			_timeouts    = timeouts;
			_networkName = networkName;
			_tag         = $"[{networkName}]";
		}

		/// <summary>An ad is on screen, or on its way there.</summary>
		public bool IsShowing => _show != null;

		/// <summary>Whether the unit has an ad loaded that can show now.</summary>
		public bool IsReady(AdType adType, string adUnitId) =>
			!_disposed
			&& !string.IsNullOrEmpty(adUnitId)
			// A unit never asked to load has nothing loaded, and some SDKs (MAX's editor stub)
			// warn every time one is queried.
			&& _requested.Contains(adUnitId)
			&& (_show == null || _show.AdUnitId != adUnitId)
			&& _sdk.IsLoaded(adType, adUnitId);

		/// <summary>
		/// Loads the unit, joining a load already running for it, and completing at once when an
		/// ad is loaded. A load the SDK doesn't answer within <see cref="FullscreenAdTimeouts.Load"/>
		/// fails with <see cref="AdErrorType.Timeout"/>. A joined load reports the first caller's
		/// placement.
		/// </summary>
		public UniTask<AdLoadResult> LoadAsync(string placementId, AdType adType, string adUnitId)
		{
			if (_disposed)
			{
				return UniTask.FromResult(AdLoadResult.Failed(placementId, adType, AdErrorType.NotInitialized, "The ad provider was disposed."));
			}

			if (string.IsNullOrEmpty(adUnitId))
			{
				return UniTask.FromResult(AdLoadResult.Failed(placementId, adType, AdErrorType.InvalidPlacement, "Ad unit ID is empty."));
			}

			if (_loads.TryGetValue(adUnitId, out PendingLoad pending))
			{
				return pending.Done.Task;
			}

			if (_show != null && _show.AdUnitId == adUnitId)
			{
				return UniTask.FromResult(AdLoadResult.Failed(placementId, adType, AdErrorType.AlreadyShowing, "The unit's ad is showing; load it once the show ends."));
			}

			if (IsReady(adType, adUnitId))
			{
				return UniTask.FromResult(AdLoadResult.Succeeded(placementId, adType));
			}

			var load = new PendingLoad(placementId, adType);
			_loads.Add(adUnitId, load);
			_requested.Add(adUnitId);

			// Taken before calling the SDK, which may answer before Load returns.
			UniTask<AdLoadResult> task = load.Done.Task;
			ExpireLoadAsync(adUnitId, load).Forget();

			try
			{
				_sdk.Load(adType, adUnitId);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				if (_loads.TryGetValue(adUnitId, out PendingLoad current) && current == load)
				{
					FinishLoad(adUnitId, load, AdLoadResult.Failed(placementId, adType, AdErrorType.InternalError, e.Message));
				}
			}

			return task;
		}

		/// <summary>
		/// Shows the unit's loaded ad, and completes when the show resolves. Fails at once with
		/// <see cref="AdErrorType.AlreadyShowing"/> while another show runs, and with
		/// <see cref="AdErrorType.NotReady"/> when the unit has no ad.
		/// </summary>
		public UniTask<AdResult> ShowAsync(string placementId, AdType adType, string adUnitId)
		{
			if (_disposed)
			{
				return UniTask.FromResult(AdResult.Failed(placementId, adType, AdErrorType.NotInitialized, "The ad provider was disposed."));
			}

			if (_show != null)
			{
				return UniTask.FromResult(AdResult.Failed(placementId, adType, AdErrorType.AlreadyShowing, "Another fullscreen ad is showing."));
			}

			if (!IsReady(adType, adUnitId))
			{
				return UniTask.FromResult(AdResult.Failed(placementId, adType, AdErrorType.NotReady, $"No {adType} ad is loaded."));
			}

			var show = new ActiveShow(placementId, adType, adUnitId, rewarded: adType is AdType.Rewarded or AdType.RewardedInterstitial);
			_show = show;

			// Taken before calling the SDK, which may answer before Show returns.
			UniTask<AdResult> task = show.Done.Task;
			ArmTimer(show);

			try
			{
				_sdk.Show(adType, adUnitId, placementId);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				if (_show == show)
				{
					Fail(show, AdErrorType.InternalError, e.Message);
				}
			}

			return task;
		}

		/// <summary>The SDK loaded an ad for the unit.</summary>
		public void OnLoaded(string adUnitId)
		{
			if (adUnitId != null && _loads.TryGetValue(adUnitId, out PendingLoad load))
			{
				FinishLoad(adUnitId, load, AdLoadResult.Succeeded(load.PlacementId, load.AdType));
			}
		}

		/// <summary>The SDK couldn't load an ad for the unit.</summary>
		public void OnLoadFailed(string adUnitId, AdErrorType errorType, string message)
		{
			if (adUnitId != null && _loads.TryGetValue(adUnitId, out PendingLoad load))
			{
				FinishLoad(adUnitId, load, AdLoadResult.Failed(load.PlacementId, load.AdType, errorType, message ?? "The ad failed to load."));
			}
		}

		/// <summary>The unit's ad reached the screen.</summary>
		public void OnDisplayed(string adUnitId, string networkName = null)
		{
			ActiveShow show = ShowOf(adUnitId);
			if (show == null)
			{
				return;
			}

			show.Note(networkName);
			AdShowWait before = show.Resolver.Waiting;
			show.Resolver.OnDisplayed();
			Advance(show, before);
		}

		/// <summary>The unit's ad couldn't be shown.</summary>
		public void OnDisplayFailed(string adUnitId, AdErrorType errorType, string message)
		{
			ActiveShow show = ShowOf(adUnitId);
			if (show != null)
			{
				Fail(show, errorType, message);
			}
		}

		/// <summary>The unit's ad closed.</summary>
		public void OnHidden(string adUnitId, string networkName = null)
		{
			ActiveShow show = ShowOf(adUnitId);
			if (show == null)
			{
				return;
			}

			show.Note(networkName);
			AdShowWait before = show.Resolver.Waiting;
			show.Resolver.OnHidden();
			Advance(show, before);
		}

		/// <summary>The unit's rewarded ad earned its reward.</summary>
		public void OnRewardEarned(string adUnitId)
		{
			ActiveShow show = ShowOf(adUnitId);
			if (show == null)
			{
				Debug.LogWarning($"{_tag} A reward for {adUnitId} arrived with no show waiting for it; it isn't granted.");
				return;
			}

			AdShowWait before = show.Resolver.Waiting;
			show.Resolver.OnRewardEarned();
			Advance(show, before);
		}

		/// <summary>The unit's ad earned revenue: attached to the show it belongs to.</summary>
		public void OnRevenuePaid(string adUnitId, double revenue, string networkName = null)
		{
			ActiveShow show = ShowOf(adUnitId);
			if (show == null)
			{
				return;
			}

			show.Revenue = revenue;
			show.Note(networkName);
		}

		/// <summary>
		/// Fails every load and show still running, and refuses new ones. Later callbacks are
		/// ignored.
		/// </summary>
		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			if (_loads.Count > 0)
			{
				// Copied first: completing a load runs its waiters, which may call back in.
				var loads = new List<PendingLoad>(_loads.Values);
				_loads.Clear();

				foreach (PendingLoad load in loads)
				{
					load.StopWatchdog();
					load.Done.TrySetResult(AdLoadResult.Failed(load.PlacementId, load.AdType, AdErrorType.NotInitialized, "The ad provider was disposed."));
				}
			}

			ActiveShow show = _show;
			if (show != null)
			{
				_show = null;
				show.StopTimer();
				show.Done.TrySetResult(show.Resolver.Displayed
					? AdResult.Incomplete(show.PlacementId, show.AdType, AdErrorType.NotInitialized, "The ad provider was disposed.", show.NetworkName ?? _networkName, show.Revenue)
					: AdResult.Failed(show.PlacementId, show.AdType, AdErrorType.NotInitialized, "The ad provider was disposed."));
			}
		}

		private ActiveShow ShowOf(string adUnitId) => _show != null && _show.AdUnitId == adUnitId ? _show : null;

		private void Fail(ActiveShow show, AdErrorType errorType, string message)
		{
			show.FailureType   = errorType;
			show.FailureReason = message;

			AdShowWait before = show.Resolver.Waiting;
			show.Resolver.OnDisplayFailed();
			Advance(show, before);
		}

		/// <summary>After each input: finish a resolved show, or time the new wait.</summary>
		private void Advance(ActiveShow show, AdShowWait before)
		{
			if (show.Resolver.IsResolved)
			{
				Finish(show);
			}
			else if (show.Resolver.Waiting != before)
			{
				ArmTimer(show);
			}
		}

		private void Finish(ActiveShow show)
		{
			show.StopTimer();

			// Cleared first, so whoever the result wakes can load or show this unit again.
			if (_show == show)
			{
				_show = null;
			}

			show.Done.TrySetResult(ResultOf(show));
		}

		private void ArmTimer(ActiveShow show)
		{
			show.StopTimer();

			AdShowWait wait  = show.Resolver.Waiting;
			var        timer = new CancellationTokenSource();
			show.Timer = timer;

			ExpireShowAsync(show, wait, timer).Forget();
		}

		private async UniTaskVoid ExpireShowAsync(ActiveShow show, AdShowWait wait, CancellationTokenSource timer)
		{
			double seconds = wait switch
			{
				AdShowWait.Display => _timeouts.Display,
				AdShowWait.Close   => _timeouts.Close,
				_                  => _timeouts.RewardGrace,
			};

			if (await _clock.Delay(seconds, timer.Token).SuppressCancellationThrow())
			{
				return;
			}

			if (show.Timer != timer || _show != show)
			{
				return;
			}

			if (wait != AdShowWait.Reward)
			{
				Debug.LogWarning($"{_tag} {show.AdUnitId}: no {(wait == AdShowWait.Display ? "display" : "close")} callback within {seconds:0.#}s.");
			}

			AdShowWait before = show.Resolver.Waiting;
			show.Resolver.Expire(wait);
			Advance(show, before);
		}

		private async UniTaskVoid ExpireLoadAsync(string adUnitId, PendingLoad load)
		{
			if (await _clock.Delay(_timeouts.Load, load.Watchdog.Token).SuppressCancellationThrow())
			{
				return;
			}

			if (_loads.TryGetValue(adUnitId, out PendingLoad current) && current == load)
			{
				Debug.LogWarning($"{_tag} {adUnitId}: no load result within {_timeouts.Load:0.#}s.");
				FinishLoad(adUnitId, load, AdLoadResult.Failed(load.PlacementId, load.AdType, AdErrorType.Timeout, $"No load result within {_timeouts.Load:0.#}s."));
			}
		}

		private void FinishLoad(string adUnitId, PendingLoad load, AdLoadResult result)
		{
			_loads.Remove(adUnitId);
			load.StopWatchdog();
			load.Done.TrySetResult(result);
		}

		private AdResult ResultOf(ActiveShow show)
		{
			AdShowResolver resolver = show.Resolver;
			string         network  = show.NetworkName ?? _networkName;

			switch (resolver.End)
			{
				case AdShowEnd.Completed:
					return AdResult.Succeeded(show.PlacementId, show.AdType, network, show.Revenue, resolver.RewardGranted);

				case AdShowEnd.Cancelled:
					return AdResult.Incomplete(show.PlacementId, show.AdType, AdErrorType.UserCancelled,
						"The ad closed before its reward was earned.", network, show.Revenue);

				case AdShowEnd.DisplayFailed:
				{
					string reason = show.FailureReason ?? "The ad failed to display.";
					return resolver.Displayed
						? AdResult.Incomplete(show.PlacementId, show.AdType, show.FailureType, reason, network, show.Revenue)
						: AdResult.Failed(show.PlacementId, show.AdType, show.FailureType, reason);
				}

				case AdShowEnd.TimedOut:
					return resolver.Displayed
						? AdResult.Incomplete(show.PlacementId, show.AdType, AdErrorType.Timeout,
							$"The ad never reported closing within {_timeouts.Close:0.#}s.", network, show.Revenue)
						: AdResult.Failed(show.PlacementId, show.AdType, AdErrorType.Timeout,
							$"The ad never reached the screen within {_timeouts.Display:0.#}s.");

				default:
					return AdResult.Failed(show.PlacementId, show.AdType, AdErrorType.InternalError, "The show ended unresolved.");
			}
		}

		private sealed class PendingLoad
		{
			public readonly string PlacementId;
			public readonly AdType AdType;
			public readonly UniTaskCompletionSource<AdLoadResult> Done = new();
			public readonly CancellationTokenSource Watchdog = new();

			public PendingLoad(string placementId, AdType adType)
			{
				PlacementId = placementId;
				AdType      = adType;
			}

			public void StopWatchdog()
			{
				Watchdog.Cancel();
				Watchdog.Dispose();
			}
		}

		private sealed class ActiveShow
		{
			public readonly string PlacementId;
			public readonly AdType AdType;
			public readonly string AdUnitId;
			public readonly AdShowResolver Resolver;
			public readonly UniTaskCompletionSource<AdResult> Done = new();

			/// <summary>Runs out the current wait; replaced whenever the wait changes.</summary>
			public CancellationTokenSource Timer;

			public AdErrorType FailureType = AdErrorType.InternalError;
			public string FailureReason;
			public string NetworkName;
			public double Revenue;

			public ActiveShow(string placementId, AdType adType, string adUnitId, bool rewarded)
			{
				PlacementId = placementId;
				AdType      = adType;
				AdUnitId    = adUnitId;
				Resolver    = new AdShowResolver(rewarded);
			}

			public void Note(string networkName)
			{
				if (!string.IsNullOrEmpty(networkName))
				{
					NetworkName = networkName;
				}
			}

			public void StopTimer()
			{
				CancellationTokenSource timer = Timer;
				if (timer == null)
				{
					return;
				}

				Timer = null;
				timer.Cancel();
				timer.Dispose();
			}
		}
	}
}
