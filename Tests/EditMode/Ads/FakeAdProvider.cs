using System;
using System.Collections.Generic;
using AK.CoreDomain.Ads;
using AK.Services;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace AK.Tests.Ads
{
	/// <summary>
	/// An ad network the test answers by hand. Loads and shows wait until the test calls
	/// <see cref="Fill"/>, <see cref="FailLoad"/>, <see cref="Complete"/>, <see cref="Close"/> or
	/// <see cref="FailDisplay"/>. Starting is immediate unless <c>deferInit</c> is set. Like a
	/// real network, a show uses its ad up, and a second load for a unit joins the first.
	/// </summary>
	internal sealed class FakeAdProvider : IAdProvider, IDisposable
	{
		private static readonly AdType[] Fullscreen = { AdType.Rewarded, AdType.Interstitial, AdType.AppOpen, AdType.RewardedInterstitial };

		private readonly Dictionary<string, string> _unitByPlacement = new();
		private readonly HashSet<string> _loaded = new();
		private readonly Dictionary<string, PendingLoad> _loads = new();
		private readonly bool _deferInit;
		private UniTaskCompletionSource<bool> _init;
		private UniTaskCompletionSource<AdResult> _show;
		private string _showPlacementId;
		private AdType _showAdType;

		public FakeAdProvider(string name = "Fake", int priority = 0, bool deferInit = false, params AdType[] supportedAdTypes)
		{
			ProviderName     = name;
			Priority         = priority;
			_deferInit       = deferInit;
			SupportedAdTypes = supportedAdTypes is { Length: > 0 } ? supportedAdTypes : Fullscreen;
		}

		public string                ProviderName     { get; }
		public int                   Priority         { get; }
		public bool                  IsInitialized    { get; private set; }
		public IReadOnlyList<AdType> SupportedAdTypes { get; }

		public int  InitCalls { get; private set; }
		public int  LoadCalls { get; private set; }
		public int  ShowCalls { get; private set; }
		public bool Disposed  { get; private set; }

		/// <summary>A show is waiting for the test to end it.</summary>
		public bool IsShowing => _show != null;

		public bool IsLoading(string adUnitId) => _loads.ContainsKey(adUnitId);

		public bool HasAd(string adUnitId) => _loaded.Contains(adUnitId);

		public UniTask<bool> InitializeAsync(IEnumerable<AdPlacementRegistration> placements)
		{
			InitCalls++;
			foreach (AdPlacementRegistration placement in placements)
			{
				_unitByPlacement[placement.PlacementId] = placement.AdUnitId;
			}

			if (!_deferInit)
			{
				IsInitialized = true;
				return UniTask.FromResult(true);
			}

			_init ??= new UniTaskCompletionSource<bool>();
			return _init.Task;
		}

		/// <summary>Finishes a deferred start.</summary>
		public void CompleteInit(bool started = true)
		{
			UniTaskCompletionSource<bool> init = _init ?? throw new InvalidOperationException($"{ProviderName} isn't starting.");
			_init         = null;
			IsInitialized = started;
			init.TrySetResult(started);
		}

		public bool IsAdReady(string placementId, AdType adType) =>
			IsInitialized && _unitByPlacement.TryGetValue(placementId, out string adUnitId) && _loaded.Contains(adUnitId);

		public UniTask<AdLoadResult> LoadAdAsync(string placementId, AdType adType, string adUnitId)
		{
			LoadCalls++;
			_unitByPlacement[placementId] = adUnitId;

			if (!_loads.TryGetValue(adUnitId, out PendingLoad load))
			{
				load = new PendingLoad(placementId, adType);
				_loads.Add(adUnitId, load);
			}

			return load.Done.Task;
		}

		/// <summary>The unit's load in flight fills.</summary>
		public void Fill(string adUnitId)
		{
			PendingLoad load = Take(adUnitId);
			_loaded.Add(adUnitId);
			load.Done.TrySetResult(AdLoadResult.Succeeded(load.PlacementId, load.AdType));
		}

		/// <summary>The unit's load in flight fails.</summary>
		public void FailLoad(string adUnitId, AdErrorType errorType = AdErrorType.NoFill)
		{
			PendingLoad load = Take(adUnitId);
			load.Done.TrySetResult(AdLoadResult.Failed(load.PlacementId, load.AdType, errorType, $"{errorType} from {ProviderName}"));
		}

		/// <summary>The network has an ad for the unit without being asked, as networks sometimes do.</summary>
		public void Stock(string adUnitId) => _loaded.Add(adUnitId);

		/// <summary>The unit's ad expires.</summary>
		public void Expire(string adUnitId) => _loaded.Remove(adUnitId);

		public UniTask<AdResult> ShowAdAsync(string placementId, AdType adType, string adUnitId)
		{
			ShowCalls++;
			_unitByPlacement[placementId] = adUnitId;

			if (_show != null)
			{
				return UniTask.FromResult(AdResult.Failed(placementId, adType, AdErrorType.AlreadyShowing, $"{ProviderName} is showing an ad"));
			}

			if (!_loaded.Remove(adUnitId))
			{
				return UniTask.FromResult(AdResult.Failed(placementId, adType, AdErrorType.NotReady, $"{ProviderName} has no ad for {adUnitId}"));
			}

			_show            = new UniTaskCompletionSource<AdResult>();
			_showPlacementId = placementId;
			_showAdType      = adType;
			return _show.Task;
		}

		/// <summary>The ad plays to the end. A rewarded one earns its reward.</summary>
		public void Complete(double revenue = 0d) =>
			FinishShow(AdResult.Succeeded(_showPlacementId, _showAdType, ProviderName, revenue,
				rewardGranted: _showAdType is AdType.Rewarded or AdType.RewardedInterstitial));

		/// <summary>The player closes the ad before its reward.</summary>
		public void Close() =>
			FinishShow(AdResult.Incomplete(_showPlacementId, _showAdType, AdErrorType.UserCancelled, "Closed early", ProviderName));

		/// <summary>The ad never reaches the screen.</summary>
		public void FailDisplay(AdErrorType errorType = AdErrorType.InternalError) =>
			FinishShow(AdResult.Failed(_showPlacementId, _showAdType, errorType, $"{ProviderName} couldn't display the ad"));

		public void FinishShow(AdResult result)
		{
			UniTaskCompletionSource<AdResult> show = _show;
			if (show == null)
			{
				Assert.Fail($"{ProviderName} has no show to finish.");
			}

			_show = null;
			show.TrySetResult(result);
		}

		public UniTask<AdResult> ShowBannerAsync(string placementId, string adUnitId, BannerPosition position) =>
			UniTask.FromResult(AdResult.Succeeded(placementId, AdType.Banner, ProviderName));

		public void HideBanner()
		{
		}

		public void DestroyBanner()
		{
		}

		public void SetUserConsent(bool canTrack)
		{
		}

		public void SetUserUnderAge(bool isUnderAge)
		{
		}

		public void OnApplicationPause(bool isPaused)
		{
		}

		public void Dispose() => Disposed = true;

		private PendingLoad Take(string adUnitId)
		{
			if (!_loads.Remove(adUnitId, out PendingLoad load))
			{
				Assert.Fail($"{ProviderName} has no load in flight for {adUnitId}.");
			}

			return load;
		}

		private sealed class PendingLoad
		{
			public readonly string PlacementId;
			public readonly AdType AdType;
			public readonly UniTaskCompletionSource<AdLoadResult> Done = new();

			public PendingLoad(string placementId, AdType adType)
			{
				PlacementId = placementId;
				AdType      = adType;
			}
		}
	}
}
