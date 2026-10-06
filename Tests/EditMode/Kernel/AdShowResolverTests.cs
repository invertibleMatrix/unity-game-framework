using System;
using System.Collections.Generic;
using AK.Kernel.Ads;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	/// <summary>
	/// Shows resolved from scripted callback orders. Script steps: D displayed, F display failed,
	/// R reward, H hidden (closed), and xD, xC, xR for the Display, Close and Reward timers
	/// running out.
	/// </summary>
	public class AdShowResolverTests
	{
		private static IEnumerable<TestCaseData> RewardedOrders()
		{
			yield return Case("D R H", AdShowEnd.Completed, displayed: true, reward: true).SetName("Rewarded: reward, then close");
			yield return Case("D H R", AdShowEnd.Completed, displayed: true, reward: true).SetName("Rewarded: close, then a late reward within the grace");
			yield return Case("D H xR", AdShowEnd.Cancelled, displayed: true, reward: false).SetName("Rewarded: closed early");
			yield return Case("D H xR R", AdShowEnd.Cancelled, displayed: true, reward: false).SetName("Rewarded: a reward after the grace is not granted");
			yield return Case("R H", AdShowEnd.Completed, displayed: true, reward: true).SetName("Rewarded: no displayed callback, reward first");
			yield return Case("H R", AdShowEnd.Completed, displayed: true, reward: true).SetName("Rewarded: no displayed callback, close first");
			yield return Case("D R R H", AdShowEnd.Completed, displayed: true, reward: true).SetName("Rewarded: a repeated reward is still one reward");
			yield return Case("F", AdShowEnd.DisplayFailed, displayed: false, reward: false).SetName("Rewarded: failed to display");
			yield return Case("D F", AdShowEnd.DisplayFailed, displayed: true, reward: false).SetName("Rewarded: failed after displaying");
			yield return Case("F H R", AdShowEnd.DisplayFailed, displayed: false, reward: false).SetName("Rewarded: callbacks after resolving are ignored");
			yield return Case("xD", AdShowEnd.TimedOut, displayed: false, reward: false).SetName("Rewarded: never displayed");
			yield return Case("D xC", AdShowEnd.TimedOut, displayed: true, reward: false).SetName("Rewarded: never reported closing");
			yield return Case("D R xC", AdShowEnd.Completed, displayed: true, reward: true).SetName("Rewarded: never reported closing, but the reward is kept");
			yield return Case("D xD H xR", AdShowEnd.Cancelled, displayed: true, reward: false).SetName("Rewarded: a stale display timer is ignored");
			yield return Case("D H xC xR", AdShowEnd.Cancelled, displayed: true, reward: false).SetName("Rewarded: a stale close timer is ignored");
		}

		private static IEnumerable<TestCaseData> InterstitialOrders()
		{
			yield return Case("D H", AdShowEnd.Completed, displayed: true, reward: false).SetName("Interstitial: shown and closed");
			yield return Case("H", AdShowEnd.Completed, displayed: true, reward: false).SetName("Interstitial: closed with no displayed callback");
			yield return Case("D R H", AdShowEnd.Completed, displayed: true, reward: false).SetName("Interstitial: a reward callback grants nothing");
			yield return Case("xD", AdShowEnd.TimedOut, displayed: false, reward: false).SetName("Interstitial: never displayed");
			yield return Case("D xC", AdShowEnd.TimedOut, displayed: true, reward: false).SetName("Interstitial: never reported closing");
			yield return Case("F", AdShowEnd.DisplayFailed, displayed: false, reward: false).SetName("Interstitial: failed to display");
		}

		[TestCaseSource(nameof(RewardedOrders))]
		public void Rewarded(string script, AdShowEnd end, bool displayed, bool reward) => Check(true, script, end, displayed, reward);

		[TestCaseSource(nameof(InterstitialOrders))]
		public void Interstitial(string script, AdShowEnd end, bool displayed, bool reward) => Check(false, script, end, displayed, reward);

		[Test]
		public void Waiting_FollowsTheShow()
		{
			var show = new AdShowResolver(rewarded: true);
			Assert.AreEqual(AdShowWait.Display, show.Waiting);
			Assert.IsFalse(show.IsResolved);

			show.OnDisplayed();
			Assert.AreEqual(AdShowWait.Close, show.Waiting);

			show.OnHidden();
			Assert.AreEqual(AdShowWait.Reward, show.Waiting, "closed without the reward: wait for a late one");
			Assert.IsFalse(show.IsResolved);

			show.OnRewardEarned();
			Assert.AreEqual(AdShowWait.None, show.Waiting);
			Assert.IsTrue(show.IsResolved);
		}

		[Test]
		public void AnUnresolvedShow_GrantsNothingYet()
		{
			var show = new AdShowResolver(rewarded: true);
			show.OnRewardEarned();

			Assert.IsTrue(show.RewardEarned);
			Assert.IsFalse(show.RewardGranted, "granted only once the show resolves");
			Assert.AreEqual(AdShowEnd.None, show.End);
		}

		private static TestCaseData Case(string script, AdShowEnd end, bool displayed, bool reward) =>
			new(script, end, displayed, reward);

		private static void Check(bool rewarded, string script, AdShowEnd end, bool displayed, bool reward)
		{
			var show = new AdShowResolver(rewarded);

			foreach (string step in script.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				switch (step)
				{
					case "D":  show.OnDisplayed(); break;
					case "F":  show.OnDisplayFailed(); break;
					case "R":  show.OnRewardEarned(); break;
					case "H":  show.OnHidden(); break;
					case "xD": show.Expire(AdShowWait.Display); break;
					case "xC": show.Expire(AdShowWait.Close); break;
					case "xR": show.Expire(AdShowWait.Reward); break;
					default:   throw new ArgumentException($"Unknown step '{step}'.");
				}
			}

			Assert.AreEqual(end, show.End);
			Assert.IsTrue(show.IsResolved);
			Assert.AreEqual(AdShowWait.None, show.Waiting);
			Assert.AreEqual(displayed, show.Displayed, "displayed");
			Assert.AreEqual(reward, show.RewardGranted, "reward granted");
		}
	}
}
