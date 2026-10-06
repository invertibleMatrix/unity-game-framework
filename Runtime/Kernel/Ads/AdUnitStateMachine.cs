using System;
using AK.Kernel.Retry;

namespace AK.Kernel.Ads
{
	/// <summary>Where an ad unit is in its life. See <see cref="AdUnitStateMachine"/>.</summary>
	public enum AdUnitState : byte
	{
		/// <summary>No ad and nothing under way: never asked for, shown with no reload, or out of retries.</summary>
		Idle = 0,

		/// <summary>A load is in flight. Anyone else who wants the ad waits for it.</summary>
		Loading = 1,

		/// <summary>An ad is loaded.</summary>
		Ready = 2,

		/// <summary>The ad is on screen, or on its way there.</summary>
		Showing = 3,

		/// <summary>A load is due after a wait: a retry after a failure, or the reload after a show.</summary>
		Scheduled = 4,
	}

	/// <summary>What the shell has to do after a transition.</summary>
	public enum AdUnitAction : byte
	{
		None = 0,

		/// <summary>Start loading now, and report the outcome with the command's ticket.</summary>
		Load = 1,

		/// <summary>Wait for the command's delay, then call <see cref="AdUnitStateMachine.Due"/> with its ticket.</summary>
		Wait = 2,
	}

	/// <summary>A transition's instruction to the shell.</summary>
	public readonly struct AdUnitCommand : IEquatable<AdUnitCommand>
	{
		public static readonly AdUnitCommand None = default;

		public readonly AdUnitAction Action;

		/// <summary>
		/// Names this load or wait. Outcomes and timers report it back, so the late result of
		/// work that has since been superseded is ignored instead of acted on.
		/// </summary>
		public readonly int Ticket;

		/// <summary>Seconds to wait, for <see cref="AdUnitAction.Wait"/>.</summary>
		public readonly double Delay;

		public AdUnitCommand(AdUnitAction action, int ticket, double delay)
		{
			Action = action;
			Ticket = ticket;
			Delay  = delay;
		}

		public bool Equals(AdUnitCommand other) => Action == other.Action && Ticket == other.Ticket && Delay.Equals(other.Delay);

		public override bool Equals(object obj) => obj is AdUnitCommand other && Equals(other);

		public override int GetHashCode() => ((int)Action * 397 ^ Ticket) * 397 ^ Delay.GetHashCode();

		public override string ToString() => Action switch
		{
			AdUnitAction.Load => $"Load #{Ticket}",
			AdUnitAction.Wait => $"Wait {Delay:0.###}s #{Ticket}",
			_                 => "None",
		};
	}

	/// <summary>How an ad unit keeps itself loaded.</summary>
	public readonly struct AdUnitPolicy : IEquatable<AdUnitPolicy>
	{
		/// <summary>The wait before each retry of a failed load, and how many retries a budget holds.</summary>
		public readonly BackoffPolicy Retry;

		/// <summary>Whether a failed load is retried on its own. Off, a failure leaves the unit idle until asked again.</summary>
		public readonly bool RetryFailedLoads;

		/// <summary>Whether the unit loads its next ad once a show ends.</summary>
		public readonly bool ReloadAfterShow;

		/// <summary>Seconds between a show ending and the reload.</summary>
		public readonly double ReloadDelay;

		/// <summary>Whether coming back to the foreground loads a unit that was asked for and has no ad, with a fresh retry budget.</summary>
		public readonly bool ReloadOnResume;

		public AdUnitPolicy(BackoffPolicy retry, bool retryFailedLoads, bool reloadAfterShow, double reloadDelay, bool reloadOnResume)
		{
			if (!(reloadDelay >= 0d))
			{
				throw new ArgumentOutOfRangeException(nameof(reloadDelay), reloadDelay, "Must be zero or more.");
			}

			Retry            = retry;
			RetryFailedLoads = retryFailedLoads;
			ReloadAfterShow  = reloadAfterShow;
			ReloadDelay      = reloadDelay;
			ReloadOnResume   = reloadOnResume;
		}

		public bool Equals(AdUnitPolicy other) =>
			Retry.Equals(other.Retry) && RetryFailedLoads == other.RetryFailedLoads && ReloadAfterShow == other.ReloadAfterShow
			&& ReloadDelay.Equals(other.ReloadDelay) && ReloadOnResume == other.ReloadOnResume;

		public override bool Equals(object obj) => obj is AdUnitPolicy other && Equals(other);

		public override int GetHashCode()
		{
			unchecked
			{
				int hash = Retry.GetHashCode();
				hash = hash * 397 ^ (RetryFailedLoads ? 1 : 0);
				hash = hash * 397 ^ (ReloadAfterShow ? 1 : 0);
				hash = hash * 397 ^ ReloadDelay.GetHashCode();
				return hash * 397 ^ (ReloadOnResume ? 1 : 0);
			}
		}

		public override string ToString() =>
			$"retry {Retry}{(RetryFailedLoads ? "" : " (off)")}, "
			+ (ReloadAfterShow ? $"reload {ReloadDelay:0.###}s after a show" : "no reload after a show")
			+ (ReloadOnResume ? ", reload on resume" : "");
	}

	/// <summary>
	/// The life of one ad unit: loading, retrying, showing and reloading. One per unit, since
	/// the unit holds the ad; placements that share a unit share its machine.
	///
	/// The machine only decides. Each call returns an <see cref="AdUnitCommand"/> that the shell
	/// carries out: start a load, or start a timer. Loads and timers report back with the
	/// command's ticket, and every new load or wait takes a new ticket, so the outcome of
	/// superseded work never moves the machine.
	///
	/// The ad network is the truth about whether an ad is loaded: ads expire, and a network can
	/// load one on its own. So each request first takes the network's answer
	/// (<c>adAvailable</c>) and corrects its own state to match.
	///
	/// <list type="bullet">
	/// <item><see cref="Demand"/>: someone is waiting for the ad. Loads at once, even during a
	/// retry wait, with a fresh retry budget. Joins a load in flight.</item>
	/// <item><see cref="Prefetch"/>: keep the ad loaded. Loads only from idle, with a fresh
	/// budget, and leaves a scheduled retry to its timer.</item>
	/// <item><see cref="Resume"/>: back in the foreground. Per policy, a unit that was asked for
	/// and has no ad loads now, with a fresh budget.</item>
	/// <item>A failed load retries after the policy's backoff while its budget lasts, then
	/// idles until asked again. A success resets the budget.</item>
	/// <item>A show always ends with the ad gone, whether it completed, was closed early or
	/// failed to display; the unit then reloads per policy.</item>
	/// </list>
	///
	/// No allocations. Not thread-safe.
	/// </summary>
	public sealed class AdUnitStateMachine
	{
		private readonly RetryBackoff _backoff;
		private int _ticket;
		private bool _wanted;
		private bool _loadAfterShow;

		public AdUnitStateMachine(AdUnitPolicy policy)
		{
			Policy   = policy;
			_backoff = new RetryBackoff(policy.Retry);
		}

		public AdUnitPolicy Policy { get; }

		public AdUnitState State { get; private set; }

		/// <summary>Retries used from the current budget.</summary>
		public int Retries => _backoff.Retries;

		/// <summary>Someone is waiting for this ad now.</summary>
		public AdUnitCommand Demand(bool adAvailable)
		{
			_wanted = true;
			Reconcile(adAvailable);

			switch (State)
			{
				case AdUnitState.Idle:
				case AdUnitState.Scheduled:
					return StartLoad(freshBudget: true);
				case AdUnitState.Showing:
					_loadAfterShow = true;
					return AdUnitCommand.None;
				default:
					return AdUnitCommand.None;
			}
		}

		/// <summary>The ad should be kept loaded.</summary>
		public AdUnitCommand Prefetch(bool adAvailable)
		{
			_wanted = true;
			Reconcile(adAvailable);

			switch (State)
			{
				case AdUnitState.Idle:
					return StartLoad(freshBudget: true);
				case AdUnitState.Showing:
					_loadAfterShow = true;
					return AdUnitCommand.None;
				default:
					return AdUnitCommand.None;
			}
		}

		/// <summary>The app is back in the foreground.</summary>
		public AdUnitCommand Resume(bool adAvailable)
		{
			Reconcile(adAvailable);

			if (!Policy.ReloadOnResume || !_wanted)
			{
				return AdUnitCommand.None;
			}

			return State is AdUnitState.Idle or AdUnitState.Scheduled
				? StartLoad(freshBudget: true)
				: AdUnitCommand.None;
		}

		/// <summary>The load with this ticket loaded an ad.</summary>
		public AdUnitCommand LoadSucceeded(int ticket)
		{
			if (State != AdUnitState.Loading || ticket != _ticket)
			{
				return AdUnitCommand.None;
			}

			State = AdUnitState.Ready;
			_backoff.Reset();
			return AdUnitCommand.None;
		}

		/// <summary>
		/// The load with this ticket failed. A <paramref name="retryable"/> failure is retried
		/// after a backoff while the budget lasts; <paramref name="random"/> is a uniform sample
		/// in [0, 1) for jitter.
		/// </summary>
		public AdUnitCommand LoadFailed(int ticket, bool retryable, double random)
		{
			if (State != AdUnitState.Loading || ticket != _ticket)
			{
				return AdUnitCommand.None;
			}

			if (retryable && Policy.RetryFailedLoads && _backoff.TryNext(random, out double delay))
			{
				return Schedule(delay);
			}

			State = AdUnitState.Idle;
			return AdUnitCommand.None;
		}

		/// <summary>The wait with this ticket is over.</summary>
		public AdUnitCommand Due(int ticket)
		{
			if (State != AdUnitState.Scheduled || ticket != _ticket)
			{
				return AdUnitCommand.None;
			}

			return StartLoad(freshBudget: false);
		}

		/// <summary>
		/// Claims the ad for a show. False when the unit is already showing or has no ad. A
		/// load in flight or a pending wait is abandoned: the ad is going now.
		/// </summary>
		public bool BeginShow(bool adAvailable)
		{
			Reconcile(adAvailable);

			if (State == AdUnitState.Showing || !adAvailable)
			{
				return false;
			}

			State = AdUnitState.Showing;
			_ticket++;
			return true;
		}

		/// <summary>The show is over and its ad is gone, however it ended.</summary>
		public AdUnitCommand EndShow()
		{
			if (State != AdUnitState.Showing)
			{
				return AdUnitCommand.None;
			}

			bool reload = Policy.ReloadAfterShow || _loadAfterShow;
			_loadAfterShow = false;

			if (!reload)
			{
				State = AdUnitState.Idle;
				return AdUnitCommand.None;
			}

			_backoff.Reset();
			return Policy.ReloadDelay > 0d ? Schedule(Policy.ReloadDelay) : StartLoad(freshBudget: false);
		}

		/// <summary>Forgets everything: idle, nothing wanted, and every outstanding ticket is void.</summary>
		public void Reset()
		{
			State = AdUnitState.Idle;
			_ticket++;
			_backoff.Reset();
			_wanted        = false;
			_loadAfterShow = false;
		}

		private void Reconcile(bool adAvailable)
		{
			if (adAvailable && State is AdUnitState.Idle or AdUnitState.Scheduled)
			{
				State = AdUnitState.Ready;
				_ticket++;
				_backoff.Reset();
			}
			else if (!adAvailable && State == AdUnitState.Ready)
			{
				State = AdUnitState.Idle;
			}
		}

		private AdUnitCommand StartLoad(bool freshBudget)
		{
			if (freshBudget)
			{
				_backoff.Reset();
			}

			State = AdUnitState.Loading;
			return new AdUnitCommand(AdUnitAction.Load, ++_ticket, 0d);
		}

		private AdUnitCommand Schedule(double delay)
		{
			State = AdUnitState.Scheduled;
			return new AdUnitCommand(AdUnitAction.Wait, ++_ticket, delay);
		}
	}
}
