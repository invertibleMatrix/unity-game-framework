namespace AK.Kernel.Ads
{
	/// <summary>What an unresolved show is waiting for. The shell runs one timer for the current wait.</summary>
	public enum AdShowWait : byte
	{
		/// <summary>Resolved: nothing to wait for.</summary>
		None = 0,

		/// <summary>Asked to show; the ad hasn't reported reaching the screen.</summary>
		Display = 1,

		/// <summary>The ad is on screen; waiting for it to close.</summary>
		Close = 2,

		/// <summary>A rewarded ad closed without its reward; waiting a moment for a late reward.</summary>
		Reward = 3,
	}

	/// <summary>How a show ended.</summary>
	public enum AdShowEnd : byte
	{
		/// <summary>Not resolved yet.</summary>
		None = 0,

		/// <summary>The ad was shown and closed; a rewarded ad earned its reward.</summary>
		Completed = 1,

		/// <summary>A rewarded ad closed without its reward.</summary>
		Cancelled = 2,

		/// <summary>The network reported that the ad couldn't be shown.</summary>
		DisplayFailed = 3,

		/// <summary>A wait ran out: the ad never reached the screen, or never reported closing.</summary>
		TimedOut = 4,
	}

	/// <summary>
	/// Resolves one fullscreen ad show from the network's callbacks, whatever order they come
	/// in. Networks don't agree on whether a rewarded ad's reward arrives before or after it
	/// closes, so a close without a reward waits a short grace period for one.
	///
	/// The show resolves exactly once, and inputs after that are ignored, so a reward is
	/// granted at most once. A show that has a reward is never lost to a timeout: if the ad
	/// never reports closing, it still completes with the reward.
	///
	/// The resolver keeps no time. <see cref="Waiting"/> says what it waits for; the shell
	/// runs a timer for that and calls <see cref="Expire"/> when the timer runs out.
	///
	/// No allocations. Not thread-safe.
	/// </summary>
	public sealed class AdShowResolver
	{
		public AdShowResolver(bool rewarded)
		{
			Rewarded = rewarded;
			Waiting  = AdShowWait.Display;
		}

		public bool Rewarded { get; }

		public AdShowWait Waiting { get; private set; }

		public AdShowEnd End { get; private set; }

		public bool IsResolved => End != AdShowEnd.None;

		/// <summary>The ad reached the screen, as far as the callbacks have shown.</summary>
		public bool Displayed { get; private set; }

		/// <summary>The reward arrived before the show resolved.</summary>
		public bool RewardEarned { get; private set; }

		/// <summary>Resolved, and the player gets the reward.</summary>
		public bool RewardGranted => End == AdShowEnd.Completed && RewardEarned;

		public void OnDisplayed()
		{
			if (IsResolved)
			{
				return;
			}

			Displayed = true;
			if (Waiting == AdShowWait.Display)
			{
				Waiting = AdShowWait.Close;
			}
		}

		public void OnDisplayFailed()
		{
			if (!IsResolved)
			{
				Resolve(AdShowEnd.DisplayFailed);
			}
		}

		public void OnRewardEarned()
		{
			if (IsResolved || !Rewarded)
			{
				return;
			}

			RewardEarned = true;
			Displayed    = true;

			if (Waiting == AdShowWait.Reward)
			{
				Resolve(AdShowEnd.Completed);
			}
			else if (Waiting == AdShowWait.Display)
			{
				Waiting = AdShowWait.Close;
			}
		}

		public void OnHidden()
		{
			if (IsResolved)
			{
				return;
			}

			Displayed = true;

			if (!Rewarded || RewardEarned)
			{
				Resolve(AdShowEnd.Completed);
			}
			else
			{
				Waiting = AdShowWait.Reward;
			}
		}

		/// <summary>The timer for <paramref name="wait"/> ran out. Ignored unless the show is still waiting for exactly that.</summary>
		public void Expire(AdShowWait wait)
		{
			if (IsResolved || wait == AdShowWait.None || wait != Waiting)
			{
				return;
			}

			if (wait == AdShowWait.Reward)
			{
				Resolve(AdShowEnd.Cancelled);
			}
			else
			{
				Resolve(RewardEarned ? AdShowEnd.Completed : AdShowEnd.TimedOut);
			}
		}

		private void Resolve(AdShowEnd end)
		{
			End     = end;
			Waiting = AdShowWait.None;
		}
	}
}
