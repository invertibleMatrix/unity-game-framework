using System;
using AK.Kernel.Ads;
using AK.Kernel.Retry;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class AdUnitStateMachineTests
	{
		// 10 s between retries, 3 retries, reload 1 s after a show, reload on resume.
		private static readonly AdUnitPolicy Standard = new(BackoffPolicy.Constant(10d, 3), true, true, 1d, true);

		// Loads only when asked; never retries, reloads or resumes.
		private static readonly AdUnitPolicy Manual = new(BackoffPolicy.Constant(10d, 3), false, false, 0d, false);

		// ------------------------------------------------------------------ loading

		[Test]
		public void Demand_FromIdle_Loads_AndAnotherDemandJoinsIt()
		{
			var unit = new AdUnitStateMachine(Standard);

			AdUnitCommand load = unit.Demand(false);
			Assert.AreEqual(AdUnitAction.Load, load.Action);
			Assert.AreEqual(AdUnitState.Loading, unit.State);

			Assert.AreEqual(AdUnitCommand.None, unit.Demand(false), "a second caller waits for the load in flight");
			Assert.AreEqual(AdUnitState.Loading, unit.State);
		}

		[Test]
		public void LoadSucceeded_MakesItReady_AndAStaleOutcomeIsIgnored()
		{
			var unit = new AdUnitStateMachine(Standard);
			AdUnitCommand load = unit.Demand(false);

			Assert.AreEqual(AdUnitCommand.None, unit.LoadSucceeded(load.Ticket + 1));
			Assert.AreEqual(AdUnitState.Loading, unit.State, "another load's outcome");

			unit.LoadSucceeded(load.Ticket);
			Assert.AreEqual(AdUnitState.Ready, unit.State);
			Assert.AreEqual(AdUnitCommand.None, unit.Demand(true), "nothing to load");
		}

		[Test]
		public void FailedLoads_RetryOnTheBackoff_ThenIdle()
		{
			var unit = new AdUnitStateMachine(Standard);
			AdUnitCommand load = unit.Demand(false);

			for (int retry = 1; retry <= 3; retry++)
			{
				AdUnitCommand wait = unit.LoadFailed(load.Ticket, true, 0d);
				Assert.AreEqual(AdUnitAction.Wait, wait.Action, $"retry {retry}");
				Assert.AreEqual(10d, wait.Delay, $"retry {retry}");
				Assert.AreEqual(AdUnitState.Scheduled, unit.State);
				Assert.AreEqual(retry, unit.Retries);

				load = unit.Due(wait.Ticket);
				Assert.AreEqual(AdUnitAction.Load, load.Action);
				Assert.AreEqual(AdUnitState.Loading, unit.State);
			}

			Assert.AreEqual(AdUnitCommand.None, unit.LoadFailed(load.Ticket, true, 0d), "out of retries");
			Assert.AreEqual(AdUnitState.Idle, unit.State);
		}

		[Test]
		public void AnExhaustedUnit_LoadsWhenAskedAgain_WithAFreshBudget()
		{
			var unit = new AdUnitStateMachine(new AdUnitPolicy(BackoffPolicy.Constant(10d, 1), true, true, 0d, true));
			AdUnitCommand load = unit.Demand(false);
			load = unit.Due(unit.LoadFailed(load.Ticket, true, 0d).Ticket);
			unit.LoadFailed(load.Ticket, true, 0d);
			Assert.AreEqual(AdUnitState.Idle, unit.State);

			load = unit.Demand(false);

			Assert.AreEqual(AdUnitAction.Load, load.Action, "a give-up is not forever");
			Assert.AreEqual(0, unit.Retries);
			Assert.AreEqual(AdUnitAction.Wait, unit.LoadFailed(load.Ticket, true, 0d).Action, "the budget is fresh");
		}

		[Test]
		public void Demand_DuringABackoff_LoadsAtOnce_AndVoidsTheTimer()
		{
			var unit = new AdUnitStateMachine(Standard);
			AdUnitCommand wait = unit.LoadFailed(unit.Demand(false).Ticket, true, 0d);

			AdUnitCommand load = unit.Demand(false);

			Assert.AreEqual(AdUnitAction.Load, load.Action, "someone is waiting, so no backoff");
			Assert.AreEqual(0, unit.Retries);
			Assert.AreEqual(AdUnitCommand.None, unit.Due(wait.Ticket), "the old timer is void");
			Assert.AreEqual(AdUnitState.Loading, unit.State);
		}

		[Test]
		public void Prefetch_LoadsFromIdle_ButLeavesAScheduledRetryToItsTimer()
		{
			var unit = new AdUnitStateMachine(Standard);
			AdUnitCommand load = unit.Prefetch(false);
			Assert.AreEqual(AdUnitAction.Load, load.Action);

			AdUnitCommand wait = unit.LoadFailed(load.Ticket, true, 0d);
			Assert.AreEqual(AdUnitCommand.None, unit.Prefetch(false));
			Assert.AreEqual(AdUnitState.Scheduled, unit.State);
			Assert.AreEqual(AdUnitAction.Load, unit.Due(wait.Ticket).Action);
		}

		[Test]
		public void FailuresThatCantBeRetried_GoIdle()
		{
			var unit = new AdUnitStateMachine(Standard);
			Assert.AreEqual(AdUnitCommand.None, unit.LoadFailed(unit.Demand(false).Ticket, false, 0d));
			Assert.AreEqual(AdUnitState.Idle, unit.State, "not retryable");

			var manual = new AdUnitStateMachine(Manual);
			Assert.AreEqual(AdUnitCommand.None, manual.LoadFailed(manual.Demand(false).Ticket, true, 0d));
			Assert.AreEqual(AdUnitState.Idle, manual.State, "retries off");
		}

		[Test]
		public void Backoff_JitterComesFromTheCaller()
		{
			var unit = new AdUnitStateMachine(new AdUnitPolicy(BackoffPolicy.Constant(10d, 3, jitter: 0.5d), true, true, 0d, true));

			AdUnitCommand wait = unit.LoadFailed(unit.Demand(false).Ticket, true, 0.5d);

			Assert.AreEqual(7.5d, wait.Delay, 1e-9);
		}

		// ------------------------------------------------------------------ the network is the truth

		[Test]
		public void AnAdTheNetworkLoadedOnItsOwn_IsReady_AndVoidsAPendingRetry()
		{
			var unit = new AdUnitStateMachine(Standard);
			AdUnitCommand wait = unit.LoadFailed(unit.Demand(false).Ticket, true, 0d);

			Assert.AreEqual(AdUnitCommand.None, unit.Prefetch(true));

			Assert.AreEqual(AdUnitState.Ready, unit.State);
			Assert.AreEqual(0, unit.Retries);
			Assert.AreEqual(AdUnitCommand.None, unit.Due(wait.Ticket));
		}

		[Test]
		public void AnAdThatExpired_IsLoadedAgain()
		{
			var unit = new AdUnitStateMachine(Standard);
			unit.LoadSucceeded(unit.Demand(false).Ticket);

			AdUnitCommand load = unit.Demand(false);

			Assert.AreEqual(AdUnitAction.Load, load.Action, "the network no longer has the ad");
		}

		// ------------------------------------------------------------------ showing

		[Test]
		public void Show_ClaimsTheAd_ThenReloadsAfterTheDelay()
		{
			var unit = new AdUnitStateMachine(Standard);
			unit.LoadSucceeded(unit.Demand(false).Ticket);

			Assert.IsTrue(unit.BeginShow(true));
			Assert.AreEqual(AdUnitState.Showing, unit.State);
			Assert.IsFalse(unit.BeginShow(true), "one show at a time");

			AdUnitCommand wait = unit.EndShow();
			Assert.AreEqual(AdUnitAction.Wait, wait.Action);
			Assert.AreEqual(1d, wait.Delay);
			Assert.AreEqual(AdUnitAction.Load, unit.Due(wait.Ticket).Action);
		}

		[Test]
		public void Show_ReloadsAtOnce_WithNoDelay()
		{
			var unit = new AdUnitStateMachine(new AdUnitPolicy(BackoffPolicy.Constant(10d, 3), true, true, 0d, true));
			unit.BeginShow(true);

			Assert.AreEqual(AdUnitAction.Load, unit.EndShow().Action);
			Assert.AreEqual(AdUnitState.Loading, unit.State);
		}

		[Test]
		public void Show_WithoutReloads_GoesIdle_UnlessTheAdWasAskedForDuringTheShow()
		{
			var unit = new AdUnitStateMachine(Manual);
			unit.BeginShow(true);
			Assert.AreEqual(AdUnitCommand.None, unit.EndShow());
			Assert.AreEqual(AdUnitState.Idle, unit.State);

			unit.BeginShow(true);
			Assert.AreEqual(AdUnitCommand.None, unit.Demand(false), "can't load while showing");
			Assert.AreEqual(AdUnitAction.Load, unit.EndShow().Action, "but loads once the show ends");
		}

		[Test]
		public void BeginShow_NeedsAnAd()
		{
			var unit = new AdUnitStateMachine(Standard);

			Assert.IsFalse(unit.BeginShow(false));
			Assert.AreEqual(AdUnitState.Idle, unit.State);
			Assert.AreEqual(AdUnitCommand.None, unit.EndShow(), "no show to end");
		}

		[Test]
		public void BeginShow_AbandonsALoadInFlight()
		{
			var unit = new AdUnitStateMachine(Standard);
			AdUnitCommand load = unit.Demand(false);

			Assert.IsTrue(unit.BeginShow(true), "the network already has an ad");
			Assert.AreEqual(AdUnitCommand.None, unit.LoadSucceeded(load.Ticket));
			Assert.AreEqual(AdUnitState.Showing, unit.State, "the late load doesn't end the show");
		}

		// ------------------------------------------------------------------ resume and reset

		[Test]
		public void Resume_ReloadsAUnitThatWasAskedFor_WithAFreshBudget()
		{
			var unit = new AdUnitStateMachine(new AdUnitPolicy(BackoffPolicy.Constant(10d, 1), true, true, 0d, true));
			AdUnitCommand load = unit.Demand(false);
			load = unit.Due(unit.LoadFailed(load.Ticket, true, 0d).Ticket);
			unit.LoadFailed(load.Ticket, true, 0d);
			Assert.AreEqual(AdUnitState.Idle, unit.State);

			AdUnitCommand resumed = unit.Resume(false);
			Assert.AreEqual(AdUnitAction.Load, resumed.Action);
			Assert.AreEqual(0, unit.Retries);

			Assert.AreEqual(AdUnitAction.Wait, unit.LoadFailed(resumed.Ticket, true, 0d).Action, "the budget is fresh");
			Assert.AreEqual(AdUnitAction.Load, unit.Resume(false).Action, "a scheduled retry goes at once too");
		}

		[Test]
		public void Resume_LeavesAlone_UnitsNobodyAskedFor_AndUnitsBusyOrLoaded()
		{
			Assert.AreEqual(AdUnitCommand.None, new AdUnitStateMachine(Standard).Resume(false), "never asked for");

			var manual = new AdUnitStateMachine(Manual);
			manual.LoadFailed(manual.Demand(false).Ticket, true, 0d);
			Assert.AreEqual(AdUnitCommand.None, manual.Resume(false), "policy says no");

			var loading = new AdUnitStateMachine(Standard);
			loading.Demand(false);
			Assert.AreEqual(AdUnitCommand.None, loading.Resume(false));

			var ready = new AdUnitStateMachine(Standard);
			ready.LoadSucceeded(ready.Demand(false).Ticket);
			Assert.AreEqual(AdUnitCommand.None, ready.Resume(true));

			var showing = new AdUnitStateMachine(Standard);
			showing.Demand(false);
			showing.BeginShow(true);
			Assert.AreEqual(AdUnitCommand.None, showing.Resume(false));
			Assert.AreEqual(AdUnitState.Showing, showing.State);
		}

		[Test]
		public void Reset_VoidsEveryTicket_AndForgetsTheUnitWasWanted()
		{
			var unit = new AdUnitStateMachine(Standard);
			AdUnitCommand load = unit.Demand(false);

			unit.Reset();

			Assert.AreEqual(AdUnitState.Idle, unit.State);
			Assert.AreEqual(AdUnitCommand.None, unit.LoadSucceeded(load.Ticket));
			Assert.AreEqual(AdUnitState.Idle, unit.State);
			Assert.AreEqual(AdUnitCommand.None, unit.Resume(false));
		}

		[Test]
		public void EveryLoadAndWait_TakesANewTicket()
		{
			var unit = new AdUnitStateMachine(Standard);
			AdUnitCommand first = unit.Demand(false);
			AdUnitCommand wait = unit.LoadFailed(first.Ticket, true, 0d);
			AdUnitCommand second = unit.Due(wait.Ticket);

			Assert.AreNotEqual(first.Ticket, wait.Ticket);
			Assert.AreNotEqual(wait.Ticket, second.Ticket);
			Assert.AreEqual(AdUnitCommand.None, unit.LoadFailed(first.Ticket, true, 0d), "the first load's outcome is stale");
		}

		// ------------------------------------------------------------------ policy

		[Test]
		public void Policies_AreEqualByValue()
		{
			var same      = new AdUnitPolicy(BackoffPolicy.Constant(10d, 3), true, true, 1d, true);
			var noResume  = new AdUnitPolicy(BackoffPolicy.Constant(10d, 3), true, true, 1d, false);
			var moreRetry = new AdUnitPolicy(BackoffPolicy.Constant(10d, 4), true, true, 1d, true);

			Assert.AreEqual(Standard, same);
			Assert.AreEqual(Standard.GetHashCode(), same.GetHashCode());
			Assert.AreNotEqual(Standard, noResume);
			Assert.AreNotEqual(Standard, moreRetry);
		}

		[Test]
		public void Policy_RejectsANegativeOrNaNReloadDelay()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new AdUnitPolicy(BackoffPolicy.Constant(10d, 3), true, true, -1d, true));
			Assert.Throws<ArgumentOutOfRangeException>(() => new AdUnitPolicy(BackoffPolicy.Constant(10d, 3), true, true, double.NaN, true));
		}
	}
}
