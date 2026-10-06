using System;
using AK.CoreDomain.Ads;
using AK.Services;
using AK.Services.Ads;
using AK.Tests.Support;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace AK.Tests.Ads
{
	/// <summary>
	/// The driver every fullscreen provider runs on, over a fake SDK and a manual clock: joined
	/// loads, watchdogs, callback orders and teardown.
	/// </summary>
	public class FullscreenAdDriverTests
	{
		private const string Unit = "unit-1";
		private const string Placement = "placement-1";

		private static readonly FullscreenAdTimeouts Timeouts = new(load: 60d, display: 15d, close: 180d, rewardGrace: 1d);

		private LogRecorder _log;
		private FakeFullscreenAdSdk _sdk;
		private ManualAdsClock _clock;
		private FullscreenAdDriver _driver;

		[SetUp]
		public void SetUp()
		{
			_log    = new LogRecorder();
			_sdk    = new FakeFullscreenAdSdk();
			_clock  = new ManualAdsClock();
			_driver = new FullscreenAdDriver(_sdk, _clock, Timeouts, "Network");
		}

		[TearDown]
		public void TearDown()
		{
			_driver.Dispose();
			_log.Dispose();
		}

		// ------------------------------------------------------------------ loading

		[Test]
		public void Load_JoinsTheLoadInFlight()
		{
			UniTask<AdLoadResult> first  = _driver.LoadAsync(Placement, AdType.Rewarded, Unit);
			UniTask<AdLoadResult> second = _driver.LoadAsync("placement-2", AdType.Rewarded, Unit);

			Assert.AreEqual(1, _sdk.Loads.Count, "one load per unit");
			Assert.AreEqual(UniTaskStatus.Pending, first.Status);

			_sdk.Loaded.Add(Unit);
			_driver.OnLoaded(Unit);

			Assert.IsTrue(Done(first).Success);
			AdLoadResult joined = Done(second);
			Assert.IsTrue(joined.Success);
			Assert.AreEqual(Placement, joined.PlacementId, "a joined load reports the first caller's placement");
			Assert.IsTrue(_driver.IsReady(AdType.Rewarded, Unit));
			Assert.AreEqual(0, _clock.Pending, "the watchdog stopped");
		}

		[Test]
		public void Load_ThatNeverAnswers_TimesOut()
		{
			UniTask<AdLoadResult> load = _driver.LoadAsync(Placement, AdType.Rewarded, Unit);

			_clock.Advance(59d);
			Assert.AreEqual(UniTaskStatus.Pending, load.Status);

			_clock.Advance(1d);
			AdLoadResult result = Done(load);
			Assert.IsFalse(result.Success);
			Assert.AreEqual(AdErrorType.Timeout, result.ErrorType);
			Assert.AreEqual(1, _log.Count(LogType.Warning, "no load result within 60s"));

			// A late answer changes nothing, and the next request loads again.
			_driver.OnLoaded(Unit);
			_driver.LoadAsync(Placement, AdType.Rewarded, Unit).Forget();
			Assert.AreEqual(2, _sdk.Loads.Count);
		}

		[Test]
		public void Load_ThatFails_KeepsTheSdksErrorType()
		{
			UniTask<AdLoadResult> load = _driver.LoadAsync(Placement, AdType.Rewarded, Unit);
			_driver.OnLoadFailed(Unit, AdErrorType.NetworkError, "offline");

			AdLoadResult result = Done(load);
			Assert.AreEqual(AdErrorType.NetworkError, result.ErrorType);
			Assert.AreEqual("offline", result.FailureReason);
			Assert.AreEqual(0, _clock.Pending, "the watchdog stopped");
		}

		[Test]
		public void Load_AnsweredBeforeTheSdkReturns_Completes()
		{
			_sdk.OnLoad = (_, unit) =>
			{
				_sdk.Loaded.Add(unit);
				_driver.OnLoaded(unit);
			};

			Assert.IsTrue(Done(_driver.LoadAsync(Placement, AdType.Rewarded, Unit)).Success);
			Assert.IsTrue(Done(_driver.LoadAsync(Placement, AdType.Rewarded, Unit)).Success, "an ad is loaded already");
			Assert.AreEqual(1, _sdk.Loads.Count);
		}

		[Test]
		public void AUnitNeverAskedToLoad_IsNotReady()
		{
			_sdk.Loaded.Add(Unit);

			Assert.IsFalse(_driver.IsReady(AdType.Rewarded, Unit), "the SDK isn't even asked");
		}

		[Test]
		public void AUnitThatIsShowing_IsNotReady_AndCannotLoad()
		{
			LoadAd();
			_driver.ShowAsync(Placement, AdType.Rewarded, Unit).Forget();
			_sdk.Loaded.Add(Unit);

			Assert.IsFalse(_driver.IsReady(AdType.Rewarded, Unit));
			Assert.AreEqual(AdErrorType.AlreadyShowing, Done(_driver.LoadAsync(Placement, AdType.Rewarded, Unit)).ErrorType);
		}

		// ------------------------------------------------------------------ showing

		[TestCase(false, TestName = "Show: the reward, then the close")]
		[TestCase(true, TestName = "Show: the close, then the reward (EL #4)")]
		public void Show_GrantsTheReward_InEitherCallbackOrder(bool closeFirst)
		{
			LoadAd();
			UniTask<AdResult> show = _driver.ShowAsync(Placement, AdType.Rewarded, Unit);
			_driver.OnDisplayed(Unit, "AdMob");

			if (closeFirst)
			{
				_driver.OnHidden(Unit);
				Assert.AreEqual(UniTaskStatus.Pending, show.Status, "waits out the grace for the reward");
				_driver.OnRewardEarned(Unit);
			}
			else
			{
				_driver.OnRewardEarned(Unit);
				_driver.OnHidden(Unit);
			}

			AdResult result = Done(show);
			Assert.IsTrue(result.Success);
			Assert.IsTrue(result.Displayed);
			Assert.IsTrue(result.RewardGranted);
			Assert.AreEqual("AdMob", result.NetworkName);
			Assert.IsFalse(_driver.IsShowing);
			Assert.AreEqual(0, _clock.Pending, "no timer left behind");
		}

		[Test]
		public void Show_ClosedBeforeItsReward_EndsCancelled_AndALateRewardIsNotGranted()
		{
			LoadAd();
			UniTask<AdResult> show = _driver.ShowAsync(Placement, AdType.Rewarded, Unit);
			_driver.OnDisplayed(Unit);
			_driver.OnRevenuePaid(Unit, 0.02d);
			_driver.OnHidden(Unit);

			_clock.Advance(1d);

			AdResult result = Done(show);
			Assert.IsFalse(result.Success);
			Assert.IsTrue(result.Displayed, "it was on screen");
			Assert.AreEqual(AdErrorType.UserCancelled, result.ErrorType);
			Assert.IsFalse(result.RewardGranted);
			Assert.AreEqual(0.02d, result.Revenue);
			Assert.AreEqual("Network", result.NetworkName, "the driver's network when the SDK names none");

			_driver.OnRewardEarned(Unit);
			Assert.AreEqual(1, _log.Count(LogType.Warning, "arrived with no show waiting for it"));
		}

		[Test]
		public void Interstitial_CompletesWhenItCloses()
		{
			LoadAd(AdType.Interstitial);
			UniTask<AdResult> show = _driver.ShowAsync(Placement, AdType.Interstitial, Unit);
			_driver.OnDisplayed(Unit);
			_driver.OnHidden(Unit);

			AdResult result = Done(show);
			Assert.IsTrue(result.Success);
			Assert.IsFalse(result.RewardGranted);
		}

		[Test]
		public void Show_ThatNeverReachesTheScreen_TimesOut()
		{
			LoadAd();
			UniTask<AdResult> show = _driver.ShowAsync(Placement, AdType.Rewarded, Unit);

			_clock.Advance(15d);

			AdResult result = Done(show);
			Assert.IsFalse(result.Displayed);
			Assert.AreEqual(AdErrorType.Timeout, result.ErrorType);
			Assert.AreEqual(1, _log.Count(LogType.Warning, "no display callback within 15s"));
			Assert.IsFalse(_driver.IsShowing);
		}

		[TestCase(true, TestName = "Show: never reports closing, but keeps the reward it earned")]
		[TestCase(false, TestName = "Show: never reports closing, with no reward")]
		public void Show_ThatNeverReportsClosing_TimesOut(bool rewardEarned)
		{
			LoadAd();
			UniTask<AdResult> show = _driver.ShowAsync(Placement, AdType.Rewarded, Unit);
			_driver.OnDisplayed(Unit);
			if (rewardEarned)
			{
				_driver.OnRewardEarned(Unit);
			}

			_clock.Advance(180d);

			AdResult result = Done(show);
			Assert.IsTrue(result.Displayed);
			Assert.AreEqual(rewardEarned, result.Success);
			Assert.AreEqual(rewardEarned, result.RewardGranted);
			Assert.AreEqual(rewardEarned ? AdErrorType.None : AdErrorType.Timeout, result.ErrorType);
			Assert.AreEqual(1, _log.Count(LogType.Warning, "no close callback within 180s"));
		}

		[Test]
		public void Show_ThatFailsToDisplay_WasNotDisplayed()
		{
			LoadAd(AdType.Interstitial);
			UniTask<AdResult> show = _driver.ShowAsync(Placement, AdType.Interstitial, Unit);
			_driver.OnDisplayFailed(Unit, AdErrorType.InternalError, "renderer crashed");

			AdResult result = Done(show);
			Assert.IsFalse(result.Displayed);
			Assert.AreEqual(AdErrorType.InternalError, result.ErrorType);
			Assert.AreEqual("renderer crashed", result.FailureReason);
		}

		[Test]
		public void Show_AnsweredBeforeTheSdkReturns_Completes()
		{
			LoadAd();
			_sdk.OnShow = (_, unit) =>
			{
				_driver.OnDisplayed(unit);
				_driver.OnRewardEarned(unit);
				_driver.OnHidden(unit);
			};

			AdResult result = Done(_driver.ShowAsync(Placement, AdType.Rewarded, Unit));
			Assert.IsTrue(result.Success);
			Assert.IsTrue(result.RewardGranted);
			Assert.AreEqual(0, _clock.Pending);
		}

		[Test]
		public void Show_WhenTheSdkThrows_FailsWithInternalError()
		{
			LoadAd();
			_sdk.OnShow = (_, _) => throw new InvalidOperationException("sdk exploded");

			using (ExpectedLog.Exception("sdk exploded"))
			{
				AdResult result = Done(_driver.ShowAsync(Placement, AdType.Rewarded, Unit));
				Assert.AreEqual(AdErrorType.InternalError, result.ErrorType);
				Assert.IsFalse(result.Displayed);
			}

			Assert.IsFalse(_driver.IsShowing);
		}

		[Test]
		public void Show_WhileAnotherIsShowing_IsRefused()
		{
			LoadAd();
			LoadAd(unit: "unit-2");
			UniTask<AdResult> first = _driver.ShowAsync(Placement, AdType.Rewarded, Unit);

			AdResult second = Done(_driver.ShowAsync("placement-2", AdType.Rewarded, "unit-2"));

			Assert.AreEqual(AdErrorType.AlreadyShowing, second.ErrorType);
			Assert.AreEqual(1, _sdk.Shows.Count);
			Assert.AreEqual(UniTaskStatus.Pending, first.Status);
		}

		[Test]
		public void Show_WithNoAdLoaded_IsNotReady()
		{
			AdResult result = Done(_driver.ShowAsync(Placement, AdType.Rewarded, Unit));

			Assert.AreEqual(AdErrorType.NotReady, result.ErrorType);
			Assert.AreEqual(0, _sdk.Shows.Count);
		}

		// ------------------------------------------------------------------ teardown

		[Test]
		public void Dispose_FailsTheLoadsAndTheShowStillRunning()
		{
			LoadAd();
			UniTask<AdResult> show = _driver.ShowAsync(Placement, AdType.Rewarded, Unit);
			_driver.OnDisplayed(Unit);
			UniTask<AdLoadResult> load = _driver.LoadAsync("placement-2", AdType.Interstitial, "unit-2");

			_driver.Dispose();

			AdResult shown = Done(show);
			Assert.IsTrue(shown.Displayed);
			Assert.AreEqual(AdErrorType.NotInitialized, shown.ErrorType);
			Assert.AreEqual(AdErrorType.NotInitialized, Done(load).ErrorType);
			Assert.AreEqual(0, _clock.Pending, "timers stopped");

			_driver.OnHidden(Unit);
			Assert.AreEqual(AdErrorType.NotInitialized, Done(_driver.LoadAsync(Placement, AdType.Rewarded, Unit)).ErrorType);
		}

		[Test]
		public void Timeouts_RejectZeroNegativeAndNaN()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new FullscreenAdTimeouts(0d, 15d, 180d, 1d));
			Assert.Throws<ArgumentOutOfRangeException>(() => new FullscreenAdTimeouts(60d, double.NaN, 180d, 1d));
			Assert.Throws<ArgumentOutOfRangeException>(() => new FullscreenAdTimeouts(60d, 15d, 180d, -1d));
			Assert.DoesNotThrow(() => new FullscreenAdTimeouts(60d, 15d, 180d, 0d));
		}

		/// <summary>Loads the unit, and has the SDK fill it.</summary>
		private void LoadAd(AdType adType = AdType.Rewarded, string unit = Unit)
		{
			UniTask<AdLoadResult> load = _driver.LoadAsync(Placement, adType, unit);
			_sdk.Loaded.Add(unit);
			_driver.OnLoaded(unit);
			Assert.IsTrue(Done(load).Success);
		}

		private static T Done<T>(UniTask<T> task)
		{
			Assert.AreEqual(UniTaskStatus.Succeeded, task.Status, "the task should have completed");
			return task.GetAwaiter().GetResult();
		}
	}
}
