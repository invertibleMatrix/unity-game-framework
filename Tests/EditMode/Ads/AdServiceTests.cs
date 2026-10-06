using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Ads;
using AK.CoreDomain.RemoteConfig;
using AK.Kernel.Persistence;
using AK.Services;
using AK.Services.Ads;
using AK.Services.Ads.Providers;
using AK.Tests.Support;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests.Ads
{
	/// <summary>
	/// AdService over fake ad networks, a manual clock and an in-memory cap store, with no ad
	/// SDK: loading, joining, retries, showing, impressions, the rules and caps, and teardown.
	/// </summary>
	public class AdServiceTests
	{
		private const string UnitA = "unit-a";
		private const string UnitB = "unit-b";

		private static readonly AdServiceOptions Options = new(providerInitSeconds: 10d, showLoadSeconds: 15d, retryJitter: 0d);

		private readonly List<Object> _assets = new();
		private LogRecorder _log;
		private ManualAdsClock _clock;
		private InMemoryKeyValueStore _backend;
		private PrefsStore _store;
		private AdService _service;

		[SetUp]
		public void SetUp()
		{
			_log     = new LogRecorder();
			_clock   = new ManualAdsClock();
			_backend = new InMemoryKeyValueStore();
			_store   = new PrefsStore(_backend);
		}

		[TearDown]
		public void TearDown()
		{
			_service?.Dispose();
			_service = null;

			foreach (Object asset in _assets)
			{
				Object.DestroyImmediate(asset);
			}

			_assets.Clear();
			_log.Dispose();
		}

		// ------------------------------------------------------------------ starting

		[Test]
		public void Initialize_StartsThePreloads_WithoutWaitingForThem()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);

			AdService service = Start(network, placement);

			Assert.IsTrue(network.IsLoading(UnitA), "the preload is under way");
			Assert.IsFalse(service.IsAdReady(placement));

			network.Fill(UnitA);
			Assert.IsTrue(service.IsAdReady(placement));
		}

		[Test]
		public void Initialize_WaitsABoundedTimeForProviders_AndALateOneGetsThePreloads()
		{
			var network = new FakeAdProvider(deferInit: true);
			AdPlacementDefinition placement = Placement("reward", UnitA);
			_service = new AdService(Options, _clock, _store, network);

			UniTask<bool> init = _service.InitializeAsync(Meta(placement), playerLevel: 0);
			Assert.AreEqual(UniTaskStatus.Pending, init.Status);
			Assert.IsTrue(_service.IsInitialized, "set up, with its provider still starting");

			_clock.Advance(10d);
			Assert.IsFalse(Done(init), "no provider is up yet");
			Assert.AreEqual(0, network.LoadCalls, "nothing loads before a provider is up");
			Assert.AreEqual(1, _log.Count(LogType.Warning, "still starting after 10s"));

			network.CompleteInit();
			Assert.IsTrue(network.IsLoading(UnitA), "the late provider gets the preloads");
		}

		[Test]
		public void RefreshPlacementsForLevel_StartsNewlyAvailablePlacements_WithoutWaiting()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition later = Placement("later", UnitA, minLevel: 5);
			AdService service = Start(network, later);
			Assert.AreEqual(0, network.LoadCalls, "not available at level 0");

			Assert.IsFalse(service.RefreshPlacementsForLevel(5), "nothing is loaded yet");
			Assert.AreEqual(5, service.CurrentPlayerLevel);
			Assert.IsTrue(network.IsLoading(UnitA));

			network.Fill(UnitA);
			Assert.IsTrue(service.RefreshPlacementsForLevel(), "loaded now");
			Assert.AreEqual(1, network.LoadCalls, "nothing more to load");
		}

		// ------------------------------------------------------------------ loading

		[Test]
		public void FailedLoads_RetryOnTheirSchedule_ThenWait_UntilTheAdIsAskedFor()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA, strategy: Strategy(maxRetries: 2));
			AdService service = Start(network, placement);

			network.FailLoad(UnitA);
			Assert.AreEqual(10d, _clock.NextDueIn, "first retry in 10 s");
			_clock.Advance(10d);
			network.FailLoad(UnitA);
			_clock.Advance(10d);
			network.FailLoad(UnitA);
			Assert.IsNull(_clock.NextDueIn, "two retries, and the budget is spent");
			Assert.AreEqual(3, network.LoadCalls);

			_clock.Advance(600d);
			Assert.AreEqual(3, network.LoadCalls, "it waits to be asked");

			UniTask<AdLoadResult> load = service.LoadAdAsync(placement);
			Assert.AreEqual(4, network.LoadCalls, "asking loads at once");
			network.FailLoad(UnitA);
			Assert.AreEqual(AdErrorType.NoFill, Done(load).ErrorType);
			Assert.AreEqual(10d, _clock.NextDueIn, "with a fresh budget of retries");
		}

		[Test]
		public void PlacementsThatShareAnAdUnit_ShareItsAdAndItsLoad()
		{
			// Like Extra Life's rename and avatar_feature placements.
			var network = new FakeAdProvider();
			AdPlacementDefinition rename = Placement("rename", UnitA);
			AdPlacementDefinition avatar = Placement("avatar_feature", UnitA);
			AdService service = Start(network, rename, avatar);

			Assert.AreEqual(1, network.LoadCalls, "one unit, one load");
			network.Fill(UnitA);
			Assert.IsTrue(service.IsAdReady(rename));
			Assert.IsTrue(service.IsAdReady(avatar));

			UniTask<AdResult> show = service.ShowAdAsync(avatar);
			network.Complete();

			Assert.IsTrue(Done(show).Success);
			Assert.IsFalse(service.IsAdReady(rename), "the shared ad is used up");
			Assert.AreEqual(2, network.LoadCalls, "one reload for both");
			Assert.AreEqual(1, service.GetSessionShowCount("avatar_feature"));
			Assert.AreEqual(0, service.GetSessionShowCount("rename"), "caps stay per placement");
		}

		[Test]
		public void PlacementsThatShareAnAdUnit_ButLoadItDifferently_AreWarnedAbout()
		{
			Start(new FakeAdProvider(), Placement("first", UnitA), Placement("second", UnitA, strategy: Strategy(maxRetries: 5)));

			Assert.AreEqual(1, _log.Count(LogType.Warning, "'first' and 'second' share ad unit unit-a but load it differently; 'first' decides"));
		}

		[Test]
		public void CancelAllTasks_DropsPendingRetries_AndTheServiceStaysUsable()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			AdService service = Start(network, placement);
			network.FailLoad(UnitA);
			Assert.AreEqual(1, _clock.Pending, "a retry is waiting");

			service.CancelAllTasks();
			Assert.AreEqual(0, _clock.Pending);
			_clock.Advance(60d);
			Assert.AreEqual(1, network.LoadCalls);

			service.LoadAdAsync(placement).Forget();
			Assert.AreEqual(2, network.LoadCalls, "asking loads again");
		}

		[TestCase(true, TestName = "Resume: reloads a unit that gave up loading")]
		[TestCase(false, TestName = "Resume: leaves it when its strategy says so")]
		public void Resume_ReloadsAUnitThatGaveUp_PerItsStrategy(bool reloadOnResume)
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA, strategy: Strategy(retry: false, reloadOnResume: reloadOnResume));
			AdService service = Start(network, placement);
			network.FailLoad(UnitA);
			Assert.IsNull(_clock.NextDueIn, "no retries");

			service.OnApplicationPause(true);
			service.OnApplicationPause(false);

			Assert.AreEqual(reloadOnResume ? 2 : 1, network.LoadCalls);
		}

		[Test]
		public void DisablingAds_StopsBackgroundLoading_AndEnablingThemPreloadsAgain()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			AdService service = Start(network, placement);
			network.FailLoad(UnitA);

			service.AdsDisabled = true;
			Assert.AreEqual(0, _clock.Pending, "the retry is dropped");
			Assert.AreEqual(AdErrorType.AdsDisabled, Done(service.ShowAdAsync(placement)).ErrorType);

			service.AdsDisabled = false;
			Assert.AreEqual(2, network.LoadCalls, "preloading again");
		}

		// ------------------------------------------------------------------ showing

		[Test]
		public void AShowWhileItsAdLoads_JoinsThatLoad_ThenShows()
		{
			// EL #3: the tap used to fail on MAX's "ad already loading".
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			AdService service = Start(network, placement);
			int granted = 0;
			service.OnAdRewardGranted += _ => granted++;

			UniTask<AdResult> show = service.ShowAdAsync(placement);
			Assert.AreEqual(1, network.LoadCalls, "joined the preload");
			Assert.IsFalse(network.IsShowing);

			network.Fill(UnitA);
			Assert.IsTrue(network.IsShowing, "shown as soon as it loaded");

			network.Complete();
			AdResult result = Done(show);
			Assert.IsTrue(result.Success);
			Assert.IsTrue(result.RewardGranted);
			Assert.AreEqual(1, granted);
			Assert.IsTrue(network.IsLoading(UnitA), "the next ad loads after the show");
		}

		[Test]
		public void AShow_WaitsABoundedTimeForItsAd_WhileTheLoadCarriesOn()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			AdService service = Start(network, placement);
			AdErrorType failure = AdErrorType.None;
			service.OnAdFailed += (_, error, _) => failure = error;

			UniTask<AdResult> show = service.ShowAdAsync(placement);
			_clock.Advance(15d);

			AdResult result = Done(show);
			Assert.AreEqual(AdErrorType.Timeout, result.ErrorType);
			Assert.IsFalse(result.Displayed);
			Assert.AreEqual(AdErrorType.Timeout, failure);
			Assert.IsTrue(network.IsLoading(UnitA), "the load carries on");

			UniTask<AdResult> again = service.ShowAdAsync(placement);
			network.Fill(UnitA);
			network.Complete();
			Assert.IsTrue(Done(again).Success, "the next tap joined the same load");
			Assert.AreEqual(2, network.LoadCalls, "the preload, then the reload after the show");
		}

		[Test]
		public void AShowsFailedLoad_KeepsTheNetworksErrorType()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			AdService service = Start(network, placement);
			UniTask<AdResult> show = service.ShowAdAsync(placement);

			network.FailLoad(UnitA, AdErrorType.NetworkError);

			Assert.AreEqual(AdErrorType.NetworkError, Done(show).ErrorType);
		}

		[Test]
		public void AnExpiredAd_LoadsAgainWhenAskedFor()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			AdService service = Start(network, placement);
			network.Fill(UnitA);
			network.Expire(UnitA);
			Assert.IsFalse(service.IsAdReady(placement));

			UniTask<AdResult> show = service.ShowAdAsync(placement);
			Assert.AreEqual(2, network.LoadCalls, "the network says there's no ad, so it loads");

			network.Fill(UnitA);
			network.Complete();
			Assert.IsTrue(Done(show).Success);
		}

		[TestCase(false, TestName = "Closed early: the ad was an impression and isn't shown elsewhere")]
		[TestCase(true, TestName = "Closed early, as an older provider reports it")]
		public void ARewardedAdClosedEarly_IsAnImpression_GrantsNothing_AndIsNotRetriedElsewhere(bool olderProvider)
		{
			var max    = new FakeAdProvider("MAX", priority: 100);
			var backup = new FakeAdProvider("Backup");
			AdPlacementDefinition placement = Placement("reward", UnitA);
			AdService service = Start(new IAdProvider[] { max, backup }, placement);
			max.Fill(UnitA);
			backup.Stock(UnitA);
			int shown = 0, granted = 0;
			service.OnAdShown += _ => shown++;
			service.OnAdRewardGranted += _ => granted++;

			UniTask<AdResult> show = service.ShowAdAsync(placement);
			if (olderProvider)
			{
				// Older providers report an early close as a plain failure.
				max.FinishShow(AdResult.Failed("reward", AdType.Rewarded, AdErrorType.UserCancelled, "closed"));
			}
			else
			{
				max.Close();
			}

			AdResult result = Done(show);
			Assert.IsFalse(result.Success);
			Assert.IsTrue(result.Displayed);
			Assert.AreEqual(AdErrorType.UserCancelled, result.ErrorType);
			Assert.AreEqual(0, backup.ShowCalls, "an ad that was on screen isn't shown again by another network");
			Assert.AreEqual(1, shown);
			Assert.AreEqual(0, granted);
			Assert.AreEqual(1, service.GetSessionShowCount("reward"), "caps count impressions");
			Assert.IsTrue(max.IsLoading(UnitA), "the unit reloads");
		}

		[Test]
		public void AnAdThatNeverReachedTheScreen_IsShownByTheNextNetwork()
		{
			var max    = new FakeAdProvider("MAX", priority: 100);
			var backup = new FakeAdProvider("Backup");
			AdPlacementDefinition placement = Placement("reward", UnitA);
			AdService service = Start(new IAdProvider[] { max, backup }, placement);
			max.Fill(UnitA);
			backup.Stock(UnitA);

			UniTask<AdResult> show = service.ShowAdAsync(placement);
			max.FailDisplay();
			Assert.IsTrue(backup.IsShowing, "the backup network got the show");
			backup.Complete();

			AdResult result = Done(show);
			Assert.IsTrue(result.Success);
			Assert.AreEqual("Backup", result.NetworkName);
			Assert.AreEqual(1, service.GetSessionShowCount("reward"), "one impression");
			Assert.AreEqual(1, _log.Count(LogType.Warning, "'MAX' could not show 'reward'"));
		}

		[Test]
		public void AShowThatFailsToDisplay_IsNotAnImpression_AndTheUnitReloads()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			AdService service = Start(network, placement);
			network.Fill(UnitA);

			UniTask<AdResult> show = service.ShowAdAsync(placement);
			network.FailDisplay();

			AdResult result = Done(show);
			Assert.IsFalse(result.Displayed);
			Assert.AreEqual(AdErrorType.InternalError, result.ErrorType);
			Assert.AreEqual(0, service.GetSessionShowCount("reward"));
			Assert.IsTrue(network.IsLoading(UnitA), "a failed show still reloads");
		}

		[Test]
		public void ASecondShow_IsRefused_WhileTheFirstRuns()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition first  = Placement("reward", UnitA);
			AdPlacementDefinition second = Placement("bonus", UnitB);
			AdService service = Start(network, first, second);
			network.Fill(UnitA);
			network.Fill(UnitB);

			UniTask<AdResult> showing = service.ShowAdAsync(first);

			Assert.AreEqual(AdErrorType.AlreadyShowing, Done(service.ShowAdAsync(second)).ErrorType);
			Assert.AreEqual(1, network.ShowCalls);

			network.Complete();
			Assert.IsTrue(Done(showing).Success);

			UniTask<AdResult> next = service.ShowAdAsync(second);
			Assert.IsTrue(network.IsShowing, "the slot is free again");
			network.Complete();
			Assert.IsTrue(Done(next).Success);
		}

		[Test]
		public void AHandlerThatThrows_CannotLoseTheReward()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			AdService service = Start(network, placement);
			network.Fill(UnitA);
			int granted = 0;
			service.OnAdShown += _ => throw new InvalidOperationException("analytics broke");
			service.OnAdRewardGranted += _ => granted++;

			UniTask<AdResult> show = service.ShowAdAsync(placement);
			using (ExpectedLog.Exception("analytics broke"))
			{
				network.Complete();
			}

			Assert.IsTrue(Done(show).RewardGranted);
			Assert.AreEqual(1, granted);
		}

		[Test]
		public void CancellingTheCaller_Throws_WithNoEvents_AndFreesTheShowSlot()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			AdService service = Start(network, placement);
			int finished = 0;
			service.OnAdShowFinished += (_, _) => finished++;

			using var cancellation = new CancellationTokenSource();
			UniTask<AdResult> show = service.ShowAdAsync(placement, cancellation.Token);
			cancellation.Cancel();

			Assert.AreEqual(UniTaskStatus.Canceled, show.Status);
			Assert.AreEqual(0, finished, "a cancelled caller gets no events");
			Assert.IsTrue(network.IsLoading(UnitA), "the load carries on");

			UniTask<AdResult> next = service.ShowAdAsync(placement);
			network.Fill(UnitA);
			network.Complete();
			Assert.IsTrue(Done(next).Success, "the slot was freed");
		}

		[Test]
		public void AnAppOpenAd_IsNotShown_WhenTheAppComesBackFromOurOwnAd()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition reward  = Placement("reward", UnitA);
			AdPlacementDefinition appOpen = Placement("app_open", UnitB, AdType.AppOpen);
			AdService service = Start(network, reward, appOpen);
			network.Fill(UnitA);
			network.Fill(UnitB);

			// On Android, our own fullscreen ad pauses the app and resumes it when it closes.
			UniTask<AdResult> show = service.ShowAdAsync(reward);
			service.OnApplicationPause(true);
			network.Complete();
			service.OnApplicationPause(false);

			Assert.IsFalse(Done(service.TryShowAppOpenAdAsync()), "coming back from our own ad");
			Assert.IsTrue(Done(show).Success);

			// A real trip to the background shows it.
			service.OnApplicationPause(true);
			service.OnApplicationPause(false);
			UniTask<bool> opened = service.TryShowAppOpenAdAsync();
			Assert.IsTrue(network.IsShowing);
			network.Complete();
			Assert.IsTrue(Done(opened));
		}

		// ------------------------------------------------------------------ rules

		[Test]
		public void CanShowPlacement_IsFalse_BeforeInitialization()
		{
			_service = new AdService(Options, _clock, _store, new FakeAdProvider());

			Assert.IsFalse(_service.CanShowPlacement(Placement("reward", UnitA)));
		}

		[Test]
		public void APlacementSwitchedOff_IsRefused_AndItsAdIsNotLoaded()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			placement.IsEnabled = false;

			AdService service = Start(network, placement);

			Assert.AreEqual(0, network.LoadCalls, "nothing loads for it");
			Assert.IsFalse(service.CanShowPlacement(placement));
			AdResult refused = Done(service.ShowAdAsync(placement));
			Assert.AreEqual(AdErrorType.PlacementDisabled, refused.ErrorType);
			Assert.AreEqual("The placement is switched off", refused.FailureReason);
		}

		[Test]
		public void ARemoteSwitch_WinsOverThePlacementsOwn_EitherWay()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition switchedOff = Placement("off", UnitA);
			AdPlacementDefinition switchedOn  = Placement("on", UnitB);
			switchedOff.EnabledRemote = RemoteSwitch(false);
			switchedOn.IsEnabled      = false;
			switchedOn.EnabledRemote  = RemoteSwitch(true);

			AdService service = Start(network, switchedOff, switchedOn);

			Assert.IsFalse(service.CanShowPlacement(switchedOff), "switched off remotely");
			Assert.IsTrue(service.CanShowPlacement(switchedOn), "switched on remotely");
			Assert.IsFalse(network.IsLoading(UnitA));
			Assert.IsTrue(network.IsLoading(UnitB));
		}

		[Test]
		public void ARemoteSwitchWithoutARemoteValue_LeavesThePlacementsOwn()
		{
			// Its default reads false, but a default isn't a remote value.
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			placement.EnabledRemote = Remote<RemoteBool>();

			AdService service = Start(network, placement);

			Assert.IsFalse(placement.EnabledRemote.Value);
			Assert.IsTrue(service.CanShowPlacement(placement));
			Assert.IsTrue(network.IsLoading(UnitA));
		}

		[Test]
		public void SwitchingAnAdTypeOff_RefusesItsPlacements_AndSwitchingAdsOff_RefusesEveryOne()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition reward       = Placement("reward", UnitA);
			AdPlacementDefinition interstitial = Placement("interstitial", UnitB, AdType.Interstitial);
			AdsMeta meta = Meta(reward, interstitial);
			meta.RewardedAdsEnabled = RemoteSwitch(false);

			AdService service = Start(network, meta);

			AdResult refused = Done(service.ShowAdAsync(reward));
			Assert.AreEqual(AdErrorType.PlacementDisabled, refused.ErrorType);
			Assert.AreEqual("Rewarded ads are switched off", refused.FailureReason);
			Assert.IsTrue(service.CanShowPlacement(interstitial));

			meta.AdsEnabledGlobal = RemoteSwitch(false);

			refused = Done(service.ShowAdAsync(interstitial));
			Assert.AreEqual(AdErrorType.PlacementDisabled, refused.ErrorType);
			Assert.AreEqual("Ads are switched off", refused.FailureReason);
		}

		[Test]
		public void TheAdTypesLevel_AndThePlacementsLevelRange_BothApply()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			placement.MaxPlayerLevel = 10;
			AdsMeta meta = Meta(placement);
			meta.RewardedMinLevel = RemoteNumber(3);

			AdService service = Start(network, meta, level: 2);

			Assert.AreEqual(0, network.LoadCalls, "not loaded below the ad type's level");
			AdResult refused = Done(service.ShowAdAsync(placement));
			Assert.AreEqual(AdErrorType.LevelRestricted, refused.ErrorType);
			Assert.AreEqual("Not shown at level 2", refused.FailureReason);

			service.RefreshPlacementsForLevel(3);
			Assert.IsTrue(service.CanShowPlacement(placement));
			Assert.IsTrue(network.IsLoading(UnitA));

			service.RefreshPlacementsForLevel(11);
			Assert.AreEqual(AdErrorType.LevelRestricted, Done(service.ShowAdAsync(placement)).ErrorType, "above the placement's range");
		}

		// ------------------------------------------------------------------ caps

		[Test]
		public void ARemoteSessionCap_OverridesThePlacementsOwn()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			placement.MaxPerSession       = 5;
			placement.MaxPerSessionRemote = RemoteNumber(1);
			AdService service = Start(network, placement);

			Assert.IsTrue(ShowToTheEnd(service, network, placement).Success);

			AdResult refused = Done(service.ShowAdAsync(placement));
			Assert.AreEqual(AdErrorType.FrequencyCapReached, refused.ErrorType);
			Assert.AreEqual("Session cap reached", refused.FailureReason);
			Assert.IsFalse(service.IsAdReady(placement));
		}

		[Test]
		public void ACooldown_CountsWallClockTime()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			placement.CooldownSeconds = 60;
			AdService service = Start(network, placement);
			ShowToTheEnd(service, network, placement);

			_clock.Advance(59d);
			AdResult refused = Done(service.ShowAdAsync(placement));
			Assert.AreEqual(AdErrorType.CooldownActive, refused.ErrorType);
			Assert.AreEqual("Cooldown active", refused.FailureReason);

			_clock.Advance(1d);
			Assert.IsTrue(service.CanShowPlacement(placement));
		}

		[Test]
		public void DayCountsAndCooldowns_OutliveTheService_ButSessionCountsDoNot()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			placement.MaxPerDay       = 2;
			placement.CooldownSeconds = 600;
			AdService service = Start(network, placement);
			ShowToTheEnd(service, network, placement);
			_clock.Advance(600d);
			ShowToTheEnd(service, network, placement);
			service.Dispose();

			// The next launch, a minute later, over the same store.
			_clock.Advance(60d);
			AdService relaunched = Start(new FakeAdProvider(), placement);

			Assert.AreEqual(0, relaunched.GetSessionShowCount("reward"));
			Assert.AreEqual(2, relaunched.GetDailyShowCount("reward"));
			AdResult refused = Done(relaunched.ShowAdAsync(placement));
			Assert.AreEqual(AdErrorType.FrequencyCapReached, refused.ErrorType);
			Assert.AreEqual("Daily cap reached", refused.FailureReason);

			placement.MaxPerDay = 0;
			Assert.AreEqual(AdErrorType.CooldownActive, Done(relaunched.ShowAdAsync(placement)).ErrorType, "the cooldown came back too");
			_clock.Advance(540d);
			Assert.IsTrue(relaunched.CanShowPlacement(placement));
		}

		[Test]
		public void TheDailyCap_LiftsAtUtcMidnight()
		{
			_clock.Start = new DateTime(2026, 10, 5, 23, 59, 0, DateTimeKind.Utc);
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			placement.MaxPerDay = 1;
			AdService service = Start(network, placement);
			ShowToTheEnd(service, network, placement);
			Assert.AreEqual(AdErrorType.FrequencyCapReached, Done(service.ShowAdAsync(placement)).ErrorType);

			_clock.Advance(60d);

			Assert.AreEqual(0, service.GetDailyShowCount("reward"));
			Assert.AreEqual(1, service.GetSessionShowCount("reward"), "the session runs on");
			Assert.IsTrue(service.CanShowPlacement(placement));
		}

		[Test]
		public void TurningTheDeviceClockBack_DoesNotHoldAdsBack()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			placement.CooldownSeconds = 3600;
			AdService service = Start(network, placement);
			ShowToTheEnd(service, network, placement);
			Assert.IsFalse(service.CanShowPlacement(placement));

			_clock.Start = _clock.Start.AddHours(-2);

			Assert.IsTrue(service.CanShowPlacement(placement), "an impression from the future starts no cooldown");
		}

		[Test]
		public void TheRewardedSessionCap_CountsAcrossPlacements_RewardedInterstitialsIncluded()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition rewarded = Placement("rewarded", UnitA);
			AdPlacementDefinition mixed    = Placement("mixed", UnitB, AdType.RewardedInterstitial);
			AdsMeta meta = Meta(rewarded, mixed);
			meta.DefaultMaxRewardedPerSession = 1;
			AdService service = Start(network, meta);

			Assert.IsTrue(ShowToTheEnd(service, network, rewarded).Success);

			AdResult refused = Done(service.ShowAdAsync(mixed));
			Assert.AreEqual(AdErrorType.FrequencyCapReached, refused.ErrorType);
			Assert.AreEqual("Session cap reached for RewardedInterstitial ads", refused.FailureReason);
		}

		[Test]
		public void TheInterstitialCooldown_SpansPlacements_UntilARemoteValueLiftsIt()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition first  = Placement("first", UnitA, AdType.Interstitial);
			AdPlacementDefinition second = Placement("second", UnitB, AdType.Interstitial);
			AdsMeta meta = Meta(first, second);
			meta.DefaultInterstitialCooldown = 60;
			AdService service = Start(network, meta);
			ShowToTheEnd(service, network, first);

			AdResult refused = Done(service.ShowAdAsync(second));
			Assert.AreEqual(AdErrorType.CooldownActive, refused.ErrorType);
			Assert.AreEqual("Cooldown active for Interstitial ads", refused.FailureReason);

			meta.InterstitialCooldownOverride = RemoteNumber(0);
			Assert.IsTrue(service.CanShowPlacement(second));
		}

		[Test]
		public void DeletingTheStoresData_ForgetsEveryImpression()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			placement.MaxPerSession = 1;
			AdService service = Start(network, placement);
			ShowToTheEnd(service, network, placement);
			Assert.IsTrue(_store.Has("UGFW_AD_CAPS"));

			_store.DeleteAll();

			Assert.AreEqual(0, service.GetSessionShowCount("reward"));
			Assert.IsTrue(service.CanShowPlacement(placement));
			Assert.IsFalse(_store.Has("UGFW_AD_CAPS"), "nothing written back");
		}

		[Test]
		public void AnUnreadableCapsRecord_IsSetAside_AndTheServiceStartsFresh()
		{
			_backend.Set("UGFW_AD_CAPS", "{\"Data\":{\"Placements\":[");
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			placement.MaxPerDay = 1;

			AdService service;
			using (ExpectedLog.Error("'UGFW_AD_CAPS' can't be used"))
			{
				service = Start(network, placement);
			}

			Assert.IsTrue(service.CanShowPlacement(placement));
			Assert.IsTrue(ShowToTheEnd(service, network, placement).Success);
			Assert.AreEqual(1, _backend.Keys.Count(key => key.StartsWith("UGFW_AD_CAPS.corrupt.", StringComparison.Ordinal)), "the unreadable record is kept aside");
		}

		// ------------------------------------------------------------------ teardown

		[Test]
		public void Dispose_ReleasesAShowWaitingForItsAd_AndDisposesTheProviders()
		{
			var network = new FakeAdProvider();
			AdPlacementDefinition placement = Placement("reward", UnitA);
			AdService service = Start(network, placement);
			UniTask<AdResult> show = service.ShowAdAsync(placement);

			service.Dispose();

			AdResult result = Done(show);
			Assert.IsFalse(result.Success);
			Assert.AreEqual(AdErrorType.NotInitialized, result.ErrorType);
			Assert.IsTrue(network.Disposed);
			Assert.IsFalse(service.IsInitialized);
			Assert.AreEqual(0, _clock.Pending, "no timer left behind");
			Assert.AreEqual(AdErrorType.NotInitialized, Done(service.ShowAdAsync(placement)).ErrorType);

			UniTask<bool> again = service.InitializeAsync(Meta(placement), playerLevel: 0);
			Assert.Throws<ObjectDisposedException>(() => again.GetAwaiter().GetResult());
		}

		// ------------------------------------------------------------------ the null provider

		[Test]
		public void TheNullProvider_HasAnAdOnlyOnceLoaded_AndAShowUsesItUp()
		{
			var provider = new NullAdProvider(simulateAds: true, simulateLoadDelay: 0f, simulateShowDelay: 0f);
			Assert.IsTrue(Done(provider.InitializeAsync(new[] { new AdPlacementRegistration("reward", AdType.Rewarded, UnitA) })));
			Assert.IsFalse(provider.IsAdReady("reward", AdType.Rewarded), "nothing loaded yet");

			Assert.IsTrue(Done(provider.LoadAdAsync("reward", AdType.Rewarded, UnitA)).Success);
			Assert.IsTrue(provider.IsAdReady("reward", AdType.Rewarded));

			AdResult shown = Done(provider.ShowAdAsync("reward", AdType.Rewarded, UnitA));
			Assert.IsTrue(shown.Success);
			Assert.IsTrue(shown.RewardGranted);
			Assert.IsFalse(provider.IsAdReady("reward", AdType.Rewarded), "the show used the ad up");
			Assert.AreEqual(AdErrorType.NotReady, Done(provider.ShowAdAsync("reward", AdType.Rewarded, UnitA)).ErrorType);
		}

		[Test]
		public void TheNullProvider_WithSimulationOff_HasNoFill()
		{
			var provider = new NullAdProvider(simulateAds: false, simulateLoadDelay: 0f, simulateShowDelay: 0f);
			Done(provider.InitializeAsync(new[] { new AdPlacementRegistration("reward", AdType.Rewarded, UnitA) }));

			Assert.AreEqual(AdErrorType.NoFill, Done(provider.LoadAdAsync("reward", AdType.Rewarded, UnitA)).ErrorType);
			Assert.IsFalse(provider.IsAdReady("reward", AdType.Rewarded));
		}

		// ------------------------------------------------------------------ helpers

		/// <summary>Creates the service over the provider, and initializes it with the placements at level 0.</summary>
		private AdService Start(FakeAdProvider provider, params AdPlacementDefinition[] placements) =>
			Start(new IAdProvider[] { provider }, Meta(placements));

		private AdService Start(IAdProvider[] providers, params AdPlacementDefinition[] placements) =>
			Start(providers, Meta(placements));

		private AdService Start(FakeAdProvider provider, AdsMeta meta, int level = 0) =>
			Start(new IAdProvider[] { provider }, meta, level);

		/// <summary>Creates the service over the providers and the cap store, and initializes it at <paramref name="level"/>.</summary>
		private AdService Start(IAdProvider[] providers, AdsMeta meta, int level = 0)
		{
			_service = new AdService(Options, _clock, _store, providers);
			Assert.IsTrue(Done(_service.InitializeAsync(meta, level)), "a provider is up");
			return _service;
		}

		/// <summary>Shows the placement's ad, filling the load it waits for, and plays it to the end. A refusal comes back as it is.</summary>
		private static AdResult ShowToTheEnd(AdService service, FakeAdProvider network, AdPlacementDefinition placement)
		{
			UniTask<AdResult> show = service.ShowAdAsync(placement);

			if (show.Status == UniTaskStatus.Pending && network.IsLoading(placement.AdUnitID))
			{
				network.Fill(placement.AdUnitID);
			}

			if (network.IsShowing)
			{
				network.Complete();
			}

			return Done(show);
		}

		/// <summary>A remote switch with a fetched value.</summary>
		private RemoteBool RemoteSwitch(bool value)
		{
			var variable = Remote<RemoteBool>();
			variable.SetRemoteValue(value);
			return variable;
		}

		/// <summary>A remote number with a fetched value.</summary>
		private RemoteInt RemoteNumber(int value)
		{
			var variable = Remote<RemoteInt>();
			variable.SetRemoteValue(value);
			return variable;
		}

		/// <summary>A remote variable without a remote value: it reads as its default.</summary>
		private T Remote<T>() where T : RemoteVariableBase
		{
			var variable = ScriptableObject.CreateInstance<T>();
			_assets.Add(variable);
			return variable;
		}

		private AdsMeta Meta(params AdPlacementDefinition[] placements)
		{
			var meta = ScriptableObject.CreateInstance<AdsMeta>();
			meta.Placements = new List<AdPlacementDefinition>(placements);
			_assets.Add(meta);
			return meta;
		}

		private AdPlacementDefinition Placement(string id, string adUnitId, AdType adType = AdType.Rewarded, AdLoadingStrategy strategy = null, int minLevel = 0)
		{
			var placement = ScriptableObject.CreateInstance<AdPlacementDefinition>();
			placement.PlacementID     = id;
			placement.AdType          = adType;
			placement.AndroidAdUnitID = adUnitId;
			placement.IOSAdUnitID     = adUnitId;
			placement.MinPlayerLevel  = minLevel;
			placement.StrategyPreset  = AdLoadingStrategyPreset.Custom;
			placement.LoadingStrategy = strategy ?? Strategy();
			_assets.Add(placement);
			return placement;
		}

		/// <summary>By default: preloads, retries 3 times 10 s apart, reloads at once after a show, and reloads on resume.</summary>
		private static AdLoadingStrategy Strategy(bool retry = true, int maxRetries = 3, bool reloadOnResume = true) => new()
		{
			PreloadOnInitialize = true,
			AutoReloadOnFail    = retry,
			MaxRetryAttempts    = maxRetries,
			RetryDelaySeconds   = 10f,
			AutoReloadAfterShow = true,
			ReloadDelaySeconds  = 0f,
			LoadOnAppResume     = reloadOnResume,
		};

		private static T Done<T>(UniTask<T> task)
		{
			Assert.AreEqual(UniTaskStatus.Succeeded, task.Status, "the task should have completed");
			return task.GetAwaiter().GetResult();
		}
	}
}
