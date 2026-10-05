using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Ads;
using AK.Kernel.Ads;
using AK.Kernel.Persistence;
using AK.Kernel.Retry;
using AK.Services.Ads;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// The ads service. Keeps ad units loaded, shows ads through the providers in priority
	/// order (highest first), and applies frequency caps and cooldowns.
	///
	/// <para><b>Ad units.</b> An ad unit (the network's ID for one ad format) holds the loaded
	/// ad, so loading is per unit. Placements that share a unit share its ad, its load and its
	/// <see cref="AdUnitStateMachine"/>. The first placement in <see cref="AdsMeta"/> that uses a
	/// unit sets how it loads.</para>
	///
	/// <para><b>Loading.</b> A unit runs one load at a time, and everyone who wants its ad joins
	/// that load. A load tries each started provider in priority order until one fills. A failed
	/// load retries after a backoff while its budget lasts, then the unit idles. Asking for the
	/// ad, and coming back to the foreground when the unit's strategy loads on resume, start a
	/// fresh budget at once, cutting short a wait to retry; a level refresh restarts a unit that
	/// went idle. Initialization, refreshes and resumes start loads without waiting for them.</para>
	///
	/// <para><b>Showing.</b> One ad shows at a time. A show whose ad isn't loaded yet waits up to
	/// <see cref="AdServiceOptions.ShowLoadSeconds"/> for it. If a provider's ad never reaches the
	/// screen, the next provider with an ad gets the show; once an ad has been on screen, the
	/// show is over, completed or not. Frequency caps count every ad that reached the screen.
	/// However a show ends, the unit then reloads as its loading strategy says.</para>
	///
	/// <para><b>Rules.</b> A show is refused, in this order, when the placement is switched off,
	/// its ad type or all ads are switched off, the player's level is outside the placement's or
	/// the ad type's range, or a frequency cap or cooldown holds, the placement's own or its ad
	/// type's across placements (<see cref="AdsMeta"/>). Remote config overrides each local
	/// setting once it has a remote value. Ads load only for placements the switches and levels
	/// allow; caps don't stop loading, since they lift.</para>
	///
	/// <para><b>Caps.</b> Counted per device, in UTC days, on the clock's
	/// <see cref="IAdsClock.UtcNow"/>. Day counts and cooldowns are kept in the cap store across
	/// launches; session counts last as long as the service. An impression stamped later than
	/// now, after the device clock went back, starts no cooldown.</para>
	///
	/// <para>Main thread only. The service owns its providers: disposing it disposes those that
	/// are <see cref="IDisposable"/>.</para>
	/// </summary>
	public sealed class AdService : IAdsService, IDisposable
	{
		private const string TAG = "[AdService]";

		private readonly List<IAdProvider> _providers = new();
		private readonly List<AdUnit> _units = new();
		private readonly Dictionary<UnitKey, AdUnit> _unitByKey = new();
		private readonly Dictionary<string, AdUnit> _unitByPlacement = new();
		private readonly Dictionary<string, AdPlacementDefinition> _placementById = new();
		private readonly List<AdPlacementRegistration> _registrations = new();
		private readonly AdServiceOptions _options;
		private readonly IAdsClock _clock;

		// Impressions per placement, and per ad type across placements (TypeCapKey).
		private readonly FrequencyCapPolicy       _placementCaps = new();
		private readonly FrequencyCapPolicy       _typeCaps      = new();
		private readonly List<FrequencyCapRecord> _capRecords    = new();
		private readonly PrefsStore               _capStore;
		private readonly CapStoreListener         _capStoreListener;
		private readonly System.Random _random = new();
		private readonly CancellationTokenSource _lifetime = new();

		private AdsMeta _adsMeta;
		private int     _playerLevel;
		private bool    _isInitialized;
		private bool    _adsDisabled;
		private bool    _userCanTrack = true;
		private bool    _userUnderAge;
		private bool    _disposed;

		// Providers started by InitializeAsync, which waits a while for them. Ads start loading
		// when that wait ends, so they go to the best provider; one that starts later loads then.
		private UniTaskCompletionSource _providersStarted;
		private int                     _providersStarting;
		private bool                    _initWaiting;

		// One show at a time, from the request to its result.
		private bool _showing;

		// The app went to the background while an ad was showing, so the next resume is that ad
		// closing, not the app opening.
		private bool _pausedDuringShow;

		public bool IsInitialized => _isInitialized && !_disposed;

		public int CurrentPlayerLevel => _playerLevel;

		public bool AdsDisabled
		{
			get => _adsDisabled;
			set
			{
				if (_adsDisabled == value)
				{
					return;
				}

				_adsDisabled = value;

				if (value)
				{
					StopBackgroundWork();
					DestroyBanner();
					Raise(OnAdsDisabled);
				}
				else
				{
					PrefetchAvailable();
				}
			}
		}

		public event Action OnAdsDisabled;
		public event Action<AdPlacementDefinition> OnAdShown;
		public event Action<AdPlacementDefinition, AdErrorType, string> OnAdFailed;
		public event Action<AdPlacementDefinition, AdResult> OnAdShowFinished;
		public event Action<AdPlacementDefinition> OnAdRewardGranted;

		/// <summary>
		/// Creates a service with the default options and the <see cref="ForegroundAdsClock"/>,
		/// keeping its caps in <see cref="UniPrefs.Store"/>. Providers are tried in priority
		/// order, highest first.
		/// </summary>
		public AdService(params IAdProvider[] providers)
			: this(AdServiceOptions.Default, new ForegroundAdsClock(), UniPrefs.Store, providers)
		{
		}

		/// <param name="options">How long the service waits, and how it spreads out retries.</param>
		/// <param name="clock">The time the service runs on.</param>
		/// <param name="capStore">Where day counts and cooldowns are kept across launches; null keeps them for the service's lifetime only.</param>
		/// <param name="providers">The ad networks, tried in priority order, highest first.</param>
		/// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
		public AdService(AdServiceOptions options, IAdsClock clock, PrefsStore capStore, params IAdProvider[] providers)
		{
			_options  = options;
			_clock    = clock ?? throw new ArgumentNullException(nameof(clock));
			_capStore = capStore;

			if (capStore != null)
			{
				_capStoreListener = new CapStoreListener(this);
				capStore.AddResetListener(_capStoreListener);
			}

			if (providers != null)
			{
				foreach (IAdProvider provider in providers)
				{
					AddProvider(provider);
				}
			}
		}

		/// <summary>
		/// Adds a provider, after those of the same or higher priority. One added after
		/// initialization starts at once, and loads ads once it's up.
		/// </summary>
		public void AddProvider(IAdProvider provider)
		{
			if (provider == null || _providers.Contains(provider))
			{
				return;
			}

			int index = 0;
			while (index < _providers.Count && _providers[index].Priority >= provider.Priority)
			{
				index++;
			}

			_providers.Insert(index, provider);

			if (IsInitialized)
			{
				StartProviderAsync(provider, duringInit: false).Forget();
			}
		}

		/// <summary>Removes a provider. The service no longer owns it, so it isn't disposed.</summary>
		public void RemoveProvider(IAdProvider provider)
		{
			_providers.Remove(provider);
		}

		public async UniTask<bool> InitializeAsync(AdsMeta adsMeta, int playerLevel, CancellationToken cancellationToken = default)
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(AdService));
			}

			if (_isInitialized)
			{
				Debug.LogWarning($"{TAG} Already initialized");
				return AnyProviderStarted();
			}

			if (adsMeta == null)
			{
				Debug.LogError($"{TAG} AdsMeta is null");
				return false;
			}

			_adsMeta       = adsMeta;
			_playerLevel   = playerLevel;
			_isInitialized = true;

			LoadCaps();
			RegisterPlacements();

			// Every provider starts at once. Copied, because one may be added while they start.
			IAdProvider[] providers = _providers.ToArray();
			_providersStarted  = new UniTaskCompletionSource();
			_providersStarting = providers.Length;
			_initWaiting       = true;

			foreach (IAdProvider provider in providers)
			{
				StartProviderAsync(provider, duringInit: true).Forget();
			}

			if (_providersStarting == 0)
			{
				_providersStarted.TrySetResult();
			}

			bool allStarted;
			try
			{
				allStarted = await WaitAsync(_providersStarted.Task, _options.ProviderInitSeconds, cancellationToken);
			}
			finally
			{
				_initWaiting = false;
				PrefetchAvailable();
			}

			if (!allStarted)
			{
				Debug.LogWarning($"{TAG} Ad providers still starting after {_options.ProviderInitSeconds:0.#}s; their ads load once they're up.");
			}

			bool ready = AnyProviderStarted();
			Debug.Log(ready ? $"{TAG} Initialization complete" : $"{TAG} Initialization complete, but no ad provider is up yet");
			return ready;
		}

		public bool IsAdReady(AdPlacementDefinition placement) =>
			CanWork() && placement != null && CanShowPlacement(placement) && HasAd(placement.PlacementID, placement.AdType);

		public bool IsAdReady(string placementId) => IsAdReady(FindPlacement(placementId));

		public bool IsAdReady(AdType adType)
		{
			List<AdPlacementDefinition> placements = _adsMeta?.Placements;
			if (!CanWork() || placements == null)
			{
				return false;
			}

			foreach (AdPlacementDefinition placement in placements)
			{
				if (placement != null && placement.AdType == adType && IsAdReady(placement))
				{
					return true;
				}
			}

			return false;
		}

		public async UniTask<AdLoadResult> LoadAdAsync(AdPlacementDefinition placement, CancellationToken cancellationToken = default)
		{
			string id     = placement?.PlacementID;
			AdType adType = placement?.AdType ?? AdType.Rewarded;

			if (!IsInitialized)
			{
				return AdLoadResult.Failed(id, adType, AdErrorType.NotInitialized, "Service not initialized");
			}

			if (_adsDisabled)
			{
				return AdLoadResult.Failed(id, adType, AdErrorType.AdsDisabled, "Ads are disabled for this user");
			}

			if (placement == null)
			{
				return AdLoadResult.Failed(null, adType, AdErrorType.InvalidPlacement, "Placement is null");
			}

			if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(placement.AdUnitID))
			{
				return AdLoadResult.Failed(id, adType, AdErrorType.InvalidPlacement, "Placement ID or ad unit ID is empty");
			}

			cancellationToken.ThrowIfCancellationRequested();

			if (adType == AdType.Banner)
			{
				return await LoadThroughProvidersAsync(id, adType, placement.AdUnitID);
			}

			AdUnit unit      = UnitOf(placement);
			bool   available = IsAvailable(unit);
			UniTaskCompletionSource<AdLoadResult> load = Execute(unit, unit.Machine.Demand(available)) ?? unit.Attempt;

			if (available)
			{
				return AdLoadResult.Succeeded(id, adType);
			}

			if (load == null)
			{
				return AdLoadResult.Failed(id, adType, AdErrorType.NotReady, "The ad unit is showing; it reloads when the show ends");
			}

			AdLoadResult result = await load.Task.AttachExternalCancellation(cancellationToken);
			result.PlacementId = id;
			return result;
		}

		public UniTask<AdLoadResult> LoadAdAsync(string placementId, CancellationToken cancellationToken = default)
		{
			AdPlacementDefinition placement = FindPlacement(placementId);
			if (placement != null)
			{
				return LoadAdAsync(placement, cancellationToken);
			}

			return UniTask.FromResult(IsInitialized
				? AdLoadResult.Failed(placementId, AdType.Rewarded, AdErrorType.InvalidPlacement, $"Placement '{placementId}' not found")
				: AdLoadResult.Failed(placementId, AdType.Rewarded, AdErrorType.NotInitialized, "Service not initialized"));
		}

		public async UniTask PreloadAdsAsync(AdType adType, CancellationToken cancellationToken = default)
		{
			List<AdPlacementDefinition> placements = _adsMeta?.Placements;
			if (!CanWork() || placements == null)
			{
				return;
			}

			List<UniTask<AdLoadResult>> loads = null;

			foreach (AdPlacementDefinition placement in placements)
			{
				if (placement == null || placement.AdType != adType || !IsLive(placement))
				{
					continue;
				}

				AdUnit unit = UnitOf(placement);
				if (unit == null)
				{
					continue;
				}

				UniTaskCompletionSource<AdLoadResult> load = Execute(unit, unit.Machine.Prefetch(IsAvailable(unit))) ?? unit.Attempt;
				if (load != null)
				{
					(loads ??= new List<UniTask<AdLoadResult>>()).Add(load.Task);
				}
			}

			if (loads != null)
			{
				await UniTask.WhenAll(loads).AttachExternalCancellation(cancellationToken);
			}
		}

		public async UniTask<AdResult> ShowAdAsync(AdPlacementDefinition placement, CancellationToken cancellationToken = default)
		{
			if (!CanStartShow(placement, out AdResult refusal))
			{
				Report(placement, refusal);
				return refusal;
			}

			cancellationToken.ThrowIfCancellationRequested();

			AdResult result;
			if (placement.AdType == AdType.Banner)
			{
				result = await ShowBannerThroughProvidersAsync(placement, BannerPosition.Bottom);
			}
			else
			{
				_showing = true;
				try
				{
					result = await ShowFullscreenAsync(placement, cancellationToken);
				}
				finally
				{
					_showing = false;
				}
			}

			// After the show slot is free, so a handler can show the next ad.
			Report(placement, result);
			return result;
		}

		public UniTask<AdResult> ShowAdAsync(string placementId, CancellationToken cancellationToken = default)
		{
			AdPlacementDefinition placement = FindPlacement(placementId);
			if (placement != null)
			{
				return ShowAdAsync(placement, cancellationToken);
			}

			AdResult result = IsInitialized
				? AdResult.Failed(placementId, AdType.Rewarded, AdErrorType.InvalidPlacement, $"Placement '{placementId}' not found")
				: AdResult.Failed(placementId, AdType.Rewarded, AdErrorType.NotInitialized, "Service not initialized");
			Report(null, result);
			return UniTask.FromResult(result);
		}

		public async UniTask<bool> ShowRewardedAdAsync(AdPlacementDefinition placement, CancellationToken cancellationToken = default)
		{
			if (placement == null || !IsRewarded(placement.AdType))
			{
				Debug.LogWarning($"{TAG} ShowRewardedAdAsync called with non-rewarded placement");
				return false;
			}

			AdResult result = await ShowAdAsync(placement, cancellationToken);
			return result.Success && result.RewardGranted;
		}

		public UniTask<bool> ShowRewardedAdAsync(string placementId, CancellationToken cancellationToken = default) =>
			ShowRewardedAdAsync(FindPlacement(placementId), cancellationToken);

		public UniTask<AdResult> ShowInterstitialAsync(AdPlacementDefinition placement, CancellationToken cancellationToken = default)
		{
			if (placement == null || placement.AdType != AdType.Interstitial)
			{
				return UniTask.FromResult(AdResult.Failed(placement?.PlacementID, AdType.Interstitial, AdErrorType.InvalidPlacement, "Invalid interstitial placement"));
			}

			return ShowAdAsync(placement, cancellationToken);
		}

		public UniTask<AdResult> ShowInterstitialAsync(string placementId, CancellationToken cancellationToken = default) =>
			ShowInterstitialAsync(FindPlacement(placementId), cancellationToken);

		public UniTask<AdResult> ShowBannerAsync(AdPlacementDefinition placement, BannerPosition position = BannerPosition.Bottom, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (!IsInitialized)
			{
				return UniTask.FromResult(AdResult.Failed(placement?.PlacementID, AdType.Banner, AdErrorType.NotInitialized, "Service not initialized"));
			}

			if (_adsDisabled)
			{
				return UniTask.FromResult(AdResult.AdsDisabled(placement?.PlacementID, AdType.Banner));
			}

			if (placement == null || placement.AdType != AdType.Banner)
			{
				return UniTask.FromResult(AdResult.Failed(placement?.PlacementID, AdType.Banner, AdErrorType.InvalidPlacement, "Invalid banner placement"));
			}

			return ShowBannerThroughProvidersAsync(placement, position);
		}

		public UniTask<AdResult> ShowBannerAsync(string placementId, BannerPosition position = BannerPosition.Bottom, CancellationToken cancellationToken = default) =>
			ShowBannerAsync(FindPlacement(placementId), position, cancellationToken);

		public void HideBanner()
		{
			foreach (IAdProvider provider in _providers)
			{
				if (provider.IsInitialized)
				{
					provider.HideBanner();
				}
			}
		}

		public void DestroyBanner()
		{
			foreach (IAdProvider provider in _providers)
			{
				if (provider.IsInitialized)
				{
					provider.DestroyBanner();
				}
			}
		}

		public async UniTask<bool> TryShowAppOpenAdAsync(CancellationToken cancellationToken = default)
		{
			List<AdPlacementDefinition> placements = _adsMeta?.Placements;

			// Coming back from our own ad isn't opening the app.
			if (!CanWork() || _showing || _pausedDuringShow || placements == null)
			{
				return false;
			}

			// Copied: showing an ad hands control to the caller's code, which may change the list.
			foreach (AdPlacementDefinition placement in placements.ToArray())
			{
				// Only an ad that is loaded now: one that turns up seconds after the app opened
				// is worse than none.
				if (placement == null || placement.AdType != AdType.AppOpen || !IsAdReady(placement))
				{
					continue;
				}

				AdResult result = await ShowAdAsync(placement, cancellationToken);
				if (result.Success)
				{
					return true;
				}
			}

			return false;
		}

		public bool CanShowPlacement(AdPlacementDefinition placement) =>
			IsInitialized && placement != null && BlockedBy(placement, out _) == AdErrorType.None;

		public int GetSessionShowCount(string placementId) =>
			placementId != null ? _placementCaps.SessionCount(placementId) : 0;

		public int GetDailyShowCount(string placementId) =>
			placementId != null ? _placementCaps.DayCount(placementId, _clock.UtcNow) : 0;

		public void SetUserConsent(bool canTrack)
		{
			_userCanTrack = canTrack;
			foreach (IAdProvider provider in _providers)
			{
				provider.SetUserConsent(canTrack);
			}
		}

		public void SetUserUnderAge(bool isUnderAge)
		{
			_userUnderAge = isUnderAge;
			foreach (IAdProvider provider in _providers)
			{
				provider.SetUserUnderAge(isUnderAge);
			}
		}

		public void OnApplicationPause(bool isPaused)
		{
			foreach (IAdProvider provider in _providers)
			{
				try
				{
					provider.OnApplicationPause(isPaused);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}

			if (isPaused)
			{
				_pausedDuringShow = _showing;
				return;
			}

			if (!CanWork() || !AnyProviderStarted())
			{
				return;
			}

			// Per their loading strategy, units that ran out of retries or are waiting to retry
			// load now, with a fresh budget.
			for (int i = 0; i < _units.Count; i++)
			{
				AdUnit unit = _units[i];
				if (InUse(unit))
				{
					Execute(unit, unit.Machine.Resume(IsAvailable(unit)));
				}
			}
		}

		public bool RefreshPlacementsForLevel(int newLevel = -1)
		{
			if (!IsInitialized)
			{
				return false;
			}

			if (newLevel >= 0 && newLevel != _playerLevel)
			{
				Debug.Log($"{TAG} Player level updated from {_playerLevel} to {newLevel}");
				_playerLevel = newLevel;
			}

			return PrefetchAvailable();
		}

		public void CancelAllTasks()
		{
			StopBackgroundWork();
			Debug.Log($"{TAG} Background loading stopped");
		}

		/// <summary>
		/// Stops everything: pending loads end as failed, waits are dropped, providers are
		/// disposed, and event handlers are let go. A show that is waiting for its ad returns
		/// a failure. Safe to call more than once.
		/// </summary>
		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			// Waiters are released first, with results, so none of them sees a cancellation.
			_providersStarted?.TrySetResult();

			// By index: releasing a waiter runs the caller's code.
			for (int i = 0; i < _units.Count; i++)
			{
				AdUnit unit = _units[i];
				CancelTimer(unit);
				unit.Machine.Reset();

				UniTaskCompletionSource<AdLoadResult> attempt = unit.Attempt;
				unit.Attempt = null;
				attempt?.TrySetResult(AdLoadResult.Failed(unit.Owner.PlacementID, unit.Type, AdErrorType.NotInitialized, "The ads service was disposed"));
			}

			_lifetime.Cancel();
			_lifetime.Dispose();

			if (_capStoreListener != null)
			{
				_capStore.RemoveResetListener(_capStoreListener);
			}

			foreach (IAdProvider provider in _providers)
			{
				if (provider is IDisposable disposable)
				{
					try
					{
						disposable.Dispose();
					}
					catch (Exception e)
					{
						Debug.LogException(e);
					}
				}
			}

			OnAdsDisabled     = null;
			OnAdShown         = null;
			OnAdFailed        = null;
			OnAdShowFinished  = null;
			OnAdRewardGranted = null;
		}

		#region Initialization

		/// <summary>Indexes the meta's placements, binds each to its ad unit, and lists them for the providers.</summary>
		private void RegisterPlacements()
		{
			List<AdPlacementDefinition> placements = _adsMeta.Placements;
			if (placements == null)
			{
				return;
			}

			foreach (AdPlacementDefinition placement in placements)
			{
				if (placement == null || string.IsNullOrEmpty(placement.PlacementID))
				{
					continue;
				}

				// The first placement with an ID wins, as in AdsMeta.GetPlacementByID.
				if (!_placementById.ContainsKey(placement.PlacementID))
				{
					_placementById.Add(placement.PlacementID, placement);
				}

				if (string.IsNullOrEmpty(placement.AdUnitID))
				{
					Debug.LogWarning($"{TAG} Placement '{placement.PlacementID}' has no ad unit ID, skipping");
					continue;
				}

				_registrations.Add(new AdPlacementRegistration(placement.PlacementID, placement.AdType, placement.AdUnitID));
				Bind(placement);
			}
		}

		private async UniTaskVoid StartProviderAsync(IAdProvider provider, bool duringInit)
		{
			bool started = false;
			try
			{
				provider.SetUserConsent(_userCanTrack);
				provider.SetUserUnderAge(_userUnderAge);
				started = await provider.InitializeAsync(_registrations);
			}
			catch (Exception e)
			{
				Debug.LogError($"{TAG} Provider '{provider.ProviderName}' threw while initializing: {e}");
			}

			if (_disposed)
			{
				return;
			}

			if (started)
			{
				Debug.Log($"{TAG} Provider '{provider.ProviderName}' initialized");
			}
			else
			{
				Debug.LogWarning($"{TAG} Provider '{provider.ProviderName}' failed to initialize");
			}

			if (duringInit && --_providersStarting == 0)
			{
				_providersStarted.TrySetResult();
			}

			if (started && !_initWaiting)
			{
				PrefetchAvailable();
			}
		}

		#endregion

		#region Units

		/// <summary>The placement's ad unit, bound on first use. Null for banners and placements without an ad unit ID.</summary>
		private AdUnit UnitOf(AdPlacementDefinition placement) =>
			!string.IsNullOrEmpty(placement.PlacementID) && _unitByPlacement.TryGetValue(placement.PlacementID, out AdUnit unit)
				? unit
				: Bind(placement);

		/// <summary>
		/// Binds a placement to its ad unit, creating the unit for the first placement that uses
		/// it. Null for banners, which the provider keeps loaded itself.
		/// </summary>
		private AdUnit Bind(AdPlacementDefinition placement)
		{
			if (placement.AdType == AdType.Banner || string.IsNullOrEmpty(placement.PlacementID) || string.IsNullOrEmpty(placement.AdUnitID))
			{
				return null;
			}

			AdLoadingStrategy strategy = placement.GetEffectiveLoadingStrategy();
			AdUnitPolicy      policy   = PolicyOf(strategy);
			var               key      = new UnitKey(placement.AdType, placement.AdUnitID);

			if (_unitByKey.TryGetValue(key, out AdUnit unit))
			{
				if (!unit.Machine.Policy.Equals(policy) || unit.Preload != strategy.PreloadOnInitialize)
				{
					Debug.LogWarning($"{TAG} Placements '{unit.Owner.PlacementID}' and '{placement.PlacementID}' share ad unit {placement.AdUnitID} " +
					                 $"but load it differently; '{unit.Owner.PlacementID}' decides.");
				}
			}
			else
			{
				unit = new AdUnit(placement, policy, strategy.PreloadOnInitialize);
				_unitByKey.Add(key, unit);
				_units.Add(unit);
			}

			if (!unit.Placements.Contains(placement))
			{
				unit.Placements.Add(placement);
			}

			_unitByPlacement[placement.PlacementID] = unit;
			return unit;
		}

		private AdUnitPolicy PolicyOf(AdLoadingStrategy strategy)
		{
			// MaxRetryAttempts of 0 means unlimited retries.
			int    retries = strategy.MaxRetryAttempts > 0 ? strategy.MaxRetryAttempts : BackoffPolicy.Unlimited;
			double delay   = NonNegative(strategy.RetryDelaySeconds);

			BackoffPolicy backoff = strategy.UseExponentialBackoff
				? BackoffPolicy.Exponential(delay, NonNegative(strategy.MaxBackoffDelaySeconds), retries, _options.RetryJitter)
				: BackoffPolicy.Constant(delay, retries, _options.RetryJitter);

			return new AdUnitPolicy(
				backoff,
				retryFailedLoads: strategy.AutoReloadOnFail,
				reloadAfterShow: strategy.AutoReloadAfterShow,
				reloadDelay: NonNegative(strategy.ReloadDelaySeconds),
				reloadOnResume: strategy.LoadOnAppResume);
		}

		private static double NonNegative(float seconds) => seconds > 0f ? seconds : 0d;

		/// <summary>Whether a placement on the unit is live: its ad is worth keeping loaded.</summary>
		private bool InUse(AdUnit unit)
		{
			foreach (AdPlacementDefinition placement in unit.Placements)
			{
				if (IsLive(placement))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Starts loading every unit that preloads and has a placement available at the current
		/// level. True when one of them has an ad already. Does nothing while no provider is up:
		/// the first one to come up calls this again.
		/// </summary>
		private bool PrefetchAvailable()
		{
			if (!CanWork() || !AnyProviderStarted())
			{
				return false;
			}

			bool anyReady = false;

			for (int i = 0; i < _units.Count; i++)
			{
				AdUnit unit = _units[i];
				if (!unit.Preload || !InUse(unit))
				{
					continue;
				}

				bool available = IsAvailable(unit);
				anyReady |= available;
				Execute(unit, unit.Machine.Prefetch(available));
			}

			return anyReady;
		}

		/// <summary>Drops pending waits and sends every unit idle. Loads already running finish, unheeded.</summary>
		private void StopBackgroundWork()
		{
			for (int i = 0; i < _units.Count; i++)
			{
				CancelTimer(_units[i]);
				_units[i].Machine.Reset();
			}
		}

		/// <summary>Carries out a unit machine's command. For a load, returns the attempt, for the caller to wait on.</summary>
		private UniTaskCompletionSource<AdLoadResult> Execute(AdUnit unit, AdUnitCommand command)
		{
			switch (command.Action)
			{
				case AdUnitAction.Load:
					CancelTimer(unit);
					return StartAttempt(unit, command.Ticket);

				case AdUnitAction.Wait:
					CancelTimer(unit);
					StartWait(unit, command.Ticket, command.Delay);
					return null;

				default:
					return null;
			}
		}

		private UniTaskCompletionSource<AdLoadResult> StartAttempt(AdUnit unit, int ticket)
		{
			var attempt = new UniTaskCompletionSource<AdLoadResult>();
			unit.Attempt = attempt;
			RunAttemptAsync(unit, ticket, attempt).Forget();
			return attempt;
		}

		private async UniTaskVoid RunAttemptAsync(AdUnit unit, int ticket, UniTaskCompletionSource<AdLoadResult> attempt)
		{
			AdLoadResult result = await LoadThroughProvidersAsync(unit.Owner.PlacementID, unit.Type, unit.Id);

			if (unit.Attempt == attempt)
			{
				unit.Attempt = null;
			}

			if (!_disposed)
			{
				// The machine moves first, so whoever joined the attempt finds the unit settled.
				AdUnitCommand next = result.Success
					? unit.Machine.LoadSucceeded(ticket)
					: unit.Machine.LoadFailed(ticket, IsRetryable(result.ErrorType), _random.NextDouble());

				if (result.Success)
				{
					Debug.Log($"{TAG} Loaded '{unit.Owner.PlacementID}' ({unit.Type})");
				}
				else
				{
					string retry = next.Action == AdUnitAction.Wait ? $"; retrying in {next.Delay:0.#}s" : "";
					Debug.LogWarning($"{TAG} Failed to load '{unit.Owner.PlacementID}' ({unit.Type}): {result.ErrorType} ({result.FailureReason}){retry}");
				}

				Execute(unit, next);
			}

			attempt.TrySetResult(result);
		}

		private void StartWait(AdUnit unit, int ticket, double delay)
		{
			var timer = new CancellationTokenSource();
			unit.Timer = timer;
			RunWaitAsync(unit, ticket, delay, timer).Forget();
		}

		private async UniTaskVoid RunWaitAsync(AdUnit unit, int ticket, double delay, CancellationTokenSource timer)
		{
			if (await _clock.Delay(delay, timer.Token).SuppressCancellationThrow() || unit.Timer != timer)
			{
				return;
			}

			unit.Timer = null;
			timer.Dispose();

			if (!_disposed)
			{
				Execute(unit, unit.Machine.Due(ticket));
			}
		}

		private static void CancelTimer(AdUnit unit)
		{
			CancellationTokenSource timer = unit.Timer;
			if (timer == null)
			{
				return;
			}

			unit.Timer = null;
			timer.Cancel();
			timer.Dispose();
		}

		/// <summary>Tries each started provider in priority order until one loads. Otherwise, the first provider's failure.</summary>
		private async UniTask<AdLoadResult> LoadThroughProvidersAsync(string placementId, AdType adType, string adUnitId)
		{
			AdLoadResult first = default;
			bool         tried = false;

			for (int i = 0; i < _providers.Count && !_disposed; i++)
			{
				IAdProvider provider = _providers[i];
				if (!Serves(provider, adType))
				{
					continue;
				}

				AdLoadResult result;
				try
				{
					result = await provider.LoadAdAsync(placementId, adType, adUnitId);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					result = AdLoadResult.Failed(placementId, adType, AdErrorType.InternalError, e.Message);
				}

				if (result.Success)
				{
					return result;
				}

				if (!tried)
				{
					first = result;
					tried = true;
				}
			}

			return tried ? first : AdLoadResult.Failed(placementId, adType, AdErrorType.NotInitialized, $"No ad provider is up for {adType} ads");
		}

		/// <summary>
		/// Failures worth retrying. The rest won't change by waiting: a bad placement stays bad,
		/// and a provider that comes up later starts the loads itself.
		/// </summary>
		private static bool IsRetryable(AdErrorType errorType) => errorType switch
		{
			AdErrorType.InvalidPlacement  => false,
			AdErrorType.UnsupportedAdType => false,
			AdErrorType.NotInitialized    => false,
			AdErrorType.AdsDisabled       => false,
			_                             => true,
		};

		#endregion

		#region Showing

		private bool CanStartShow(AdPlacementDefinition placement, out AdResult refusal)
		{
			string id     = placement?.PlacementID;
			AdType adType = placement?.AdType ?? AdType.Rewarded;

			if (!IsInitialized)
			{
				return Refuse(out refusal, id, adType, AdErrorType.NotInitialized, "Service not initialized");
			}

			if (_adsDisabled)
			{
				refusal = AdResult.AdsDisabled(id, adType);
				return false;
			}

			if (placement == null)
			{
				return Refuse(out refusal, null, adType, AdErrorType.InvalidPlacement, "Placement is null");
			}

			if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(placement.AdUnitID))
			{
				return Refuse(out refusal, id, adType, AdErrorType.InvalidPlacement, "Placement ID or ad unit ID is empty");
			}

			AdErrorType blocked = BlockedBy(placement, out string reason);
			if (blocked != AdErrorType.None)
			{
				return Refuse(out refusal, id, adType, blocked, reason);
			}

			if (adType != AdType.Banner && _showing)
			{
				return Refuse(out refusal, id, adType, AdErrorType.AlreadyShowing, "Another ad is showing");
			}

			refusal = default;
			return true;
		}

		private static bool Refuse(out AdResult refusal, string placementId, AdType adType, AdErrorType errorType, string reason)
		{
			refusal = AdResult.Failed(placementId, adType, errorType, reason);
			return false;
		}

		/// <summary>The show slot is claimed: wait for the ad if needed, show it, then reload the unit.</summary>
		private async UniTask<AdResult> ShowFullscreenAsync(AdPlacementDefinition placement, CancellationToken cancellationToken)
		{
			string id     = placement.PlacementID;
			AdType adType = placement.AdType;
			AdUnit unit   = UnitOf(placement);

			if (!IsAvailable(unit))
			{
				// Joins the load in flight, or starts one at once, cutting short a wait to retry.
				UniTaskCompletionSource<AdLoadResult> load = Execute(unit, unit.Machine.Demand(adAvailable: false)) ?? unit.Attempt;
				if (load != null)
				{
					(bool finished, AdLoadResult loaded) = await WaitForLoadAsync(load.Task, _options.ShowLoadSeconds, cancellationToken);

					if (!finished)
					{
						return AdResult.Failed(id, adType, AdErrorType.Timeout, $"No ad loaded within {_options.ShowLoadSeconds:0.#}s");
					}

					if (!loaded.Success)
					{
						return AdResult.Failed(id, adType, loaded.ErrorType, loaded.FailureReason);
					}
				}

				if (_disposed)
				{
					return AdResult.Failed(id, adType, AdErrorType.NotInitialized, "The ads service was disposed");
				}

				if (_adsDisabled)
				{
					return AdResult.AdsDisabled(id, adType);
				}
			}

			if (!unit.Machine.BeginShow(IsAvailable(unit)))
			{
				return AdResult.Failed(id, adType, AdErrorType.NotReady, "No ad is loaded");
			}

			CancelTimer(unit);

			AdResult result = await ShowThroughProvidersAsync(placement);

			if (!_disposed)
			{
				Execute(unit, unit.Machine.EndShow());
			}

			return result;
		}

		/// <summary>
		/// Shows through the first provider with the ad loaded. If that ad never reaches the
		/// screen, the next provider with one gets the show.
		/// </summary>
		private async UniTask<AdResult> ShowThroughProvidersAsync(AdPlacementDefinition placement)
		{
			string   id     = placement.PlacementID;
			AdType   adType = placement.AdType;
			AdResult first  = default;
			bool     tried  = false;

			for (int i = 0; i < _providers.Count && !_disposed; i++)
			{
				IAdProvider provider = _providers[i];
				if (!Serves(provider, adType) || !provider.IsAdReady(id, adType))
				{
					continue;
				}

				AdResult result;
				try
				{
					result = await provider.ShowAdAsync(id, adType, placement.AdUnitID);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					result = AdResult.Failed(id, adType, AdErrorType.InternalError, e.Message);
				}

				// Only an ad on screen completes or gets closed early. Older providers report a
				// closed-early ad as a plain failure.
				if (result.Success || result.ErrorType == AdErrorType.UserCancelled)
				{
					result.Displayed = true;
				}

				if (result.Displayed)
				{
					return result;
				}

				Debug.LogWarning($"{TAG} '{provider.ProviderName}' could not show '{id}': {result.ErrorType} ({result.FailureReason})");

				if (!tried)
				{
					first = result;
					tried = true;
				}
			}

			return tried ? first : AdResult.Failed(id, adType, AdErrorType.NotReady, "No ad provider has the ad loaded");
		}

		private async UniTask<AdResult> ShowBannerThroughProvidersAsync(AdPlacementDefinition placement, BannerPosition position)
		{
			string   id    = placement.PlacementID;
			AdResult first = default;
			bool     tried = false;

			for (int i = 0; i < _providers.Count && !_disposed; i++)
			{
				IAdProvider provider = _providers[i];
				if (!Serves(provider, AdType.Banner))
				{
					continue;
				}

				AdResult result;
				try
				{
					result = await provider.ShowBannerAsync(id, placement.AdUnitID, position);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					result = AdResult.Failed(id, AdType.Banner, AdErrorType.InternalError, e.Message);
				}

				if (result.Success)
				{
					Debug.Log($"{TAG} Banner shown: {id} via {provider.ProviderName}");
					return result;
				}

				if (!tried)
				{
					first = result;
					tried = true;
				}
			}

			return tried ? first : AdResult.Failed(id, AdType.Banner, AdErrorType.NotInitialized, "No ad provider is up for banners");
		}

		/// <summary>Records an impression against the caps, then raises the events, each handler on its own.</summary>
		private void Report(AdPlacementDefinition placement, AdResult result)
		{
			string id = placement?.PlacementID ?? result.PlacementId;

			if (result.Success)
			{
				Debug.Log($"{TAG} Ad shown: {id} via {result.NetworkName}{(result.RewardGranted ? ", reward granted" : "")}");
			}
			else if (result.ErrorType == AdErrorType.UserCancelled)
			{
				Debug.Log($"{TAG} Ad closed before its reward: {id}");
			}
			else
			{
				Debug.LogWarning($"{TAG} Ad not shown{(result.Displayed ? " to the end" : "")}: {id}: {result.ErrorType} ({result.FailureReason})");
			}

			if (result.Displayed && placement != null)
			{
				RecordAdShown(placement);
				Raise(OnAdShown, placement);
			}

			if (!result.Success)
			{
				Raise(OnAdFailed, placement, result.ErrorType, result.FailureReason);
			}

			Raise(OnAdShowFinished, placement, result);

			if (result.Success && result.RewardGranted)
			{
				Raise(OnAdRewardGranted, placement);
			}
		}

		/// <summary>Counts an impression against the placement's caps and its ad type's, and saves them.</summary>
		private void RecordAdShown(AdPlacementDefinition placement)
		{
			DateTime now = _clock.UtcNow;

			if (!string.IsNullOrEmpty(placement.PlacementID))
			{
				_placementCaps.Record(placement.PlacementID, now);
			}

			string typeKey = TypeCapKey(placement.AdType);
			if (typeKey != null)
			{
				_typeCaps.Record(typeKey, now);
			}

			SaveCaps();
		}

		/// <summary>Why the placement can't show now, or <see cref="AdErrorType.None"/> when it can. The order is the class's.</summary>
		private AdErrorType BlockedBy(AdPlacementDefinition placement, out string reason)
		{
			AdType adType = placement.AdType;

			if (!placement.IsSwitchedOn)
			{
				reason = "The placement is switched off";
				return AdErrorType.PlacementDisabled;
			}

			if (!_adsMeta.IsTypeEnabled(adType))
			{
				reason = _adsMeta.AreAdsEnabled ? $"{adType} ads are switched off" : "Ads are switched off";
				return AdErrorType.PlacementDisabled;
			}

			if (!placement.AllowsLevel(_playerLevel) || _playerLevel < _adsMeta.GetMinLevel(adType))
			{
				reason = $"Not shown at level {_playerLevel}";
				return AdErrorType.LevelRestricted;
			}

			DateTime now = _clock.UtcNow;

			string id = placement.PlacementID;
			FrequencyCapBlock block = string.IsNullOrEmpty(id)
				? FrequencyCapBlock.None
				: _placementCaps.Check(id, PlacementCaps(placement), now);
			string scope = "";

			if (block == FrequencyCapBlock.None)
			{
				string typeKey = TypeCapKey(adType);
				if (typeKey != null)
				{
					block = _typeCaps.Check(typeKey, TypeCaps(adType), now);
					scope = $" for {adType} ads";
				}
			}

			switch (block)
			{
				case FrequencyCapBlock.SessionCap:
					reason = "Session cap reached" + scope;
					return AdErrorType.FrequencyCapReached;
				case FrequencyCapBlock.DailyCap:
					reason = "Daily cap reached" + scope;
					return AdErrorType.FrequencyCapReached;
				case FrequencyCapBlock.Cooldown:
					reason = "Cooldown active" + scope;
					return AdErrorType.CooldownActive;
				default:
					reason = null;
					return AdErrorType.None;
			}
		}

		#endregion

		#region Caps

		private const string CapsKey = "UGFW_AD_CAPS";

		/// <summary>
		/// Whether the placement may show as the switches and levels stand, caps aside: whether
		/// its ad is worth loading.
		/// </summary>
		private bool IsLive(AdPlacementDefinition placement) =>
			placement.IsAvailable(_playerLevel) && _adsMeta.IsTypeEnabled(placement.AdType) && _playerLevel >= _adsMeta.GetMinLevel(placement.AdType);

		private static FrequencyCaps PlacementCaps(AdPlacementDefinition placement) =>
			new(placement.GetMaxPerSession(), placement.GetMaxPerDay(), placement.GetCooldownSeconds());

		private FrequencyCaps TypeCaps(AdType adType) =>
			new(_adsMeta.GetMaxPerSession(adType), 0, _adsMeta.GetCooldownSeconds(adType));

		/// <summary>The key an ad type's impressions count under across placements; null for types without limits of their own.</summary>
		private static string TypeCapKey(AdType adType) => adType switch
		{
			AdType.Interstitial                            => "interstitial",
			AdType.Rewarded or AdType.RewardedInterstitial => "rewarded",
			_                                              => null,
		};

		/// <summary>Brings back the day counts and cooldowns saved by an earlier launch. An unreadable save is set aside by the store.</summary>
		private void LoadCaps()
		{
			if (_capStore == null || !_capStore.TryGet(CapsKey, out SavedCaps saved))
			{
				return;
			}

			Restore(_placementCaps, saved.Placements);
			Restore(_typeCaps, saved.Types);
		}

		private static void Restore(FrequencyCapPolicy policy, List<SavedCap> caps)
		{
			if (caps == null)
			{
				return;
			}

			foreach (SavedCap cap in caps)
			{
				if (!string.IsNullOrEmpty(cap.Key) && PersistedTime.TryParse(cap.LastShownUtc, out DateTime lastShown))
				{
					policy.Restore(new FrequencyCapRecord(cap.Key, cap.DayCount, lastShown));
				}
			}
		}

		private void SaveCaps()
		{
			if (_capStore == null)
			{
				return;
			}

			var saved = new SavedCaps();
			Collect(_placementCaps, saved.Placements);
			Collect(_typeCaps, saved.Types);
			_capStore.Set(CapsKey, saved);
		}

		private void Collect(FrequencyCapPolicy policy, List<SavedCap> into)
		{
			_capRecords.Clear();
			policy.CopyRecordsTo(_capRecords);

			foreach (FrequencyCapRecord record in _capRecords)
			{
				into.Add(new SavedCap { Key = record.Key, DayCount = record.DayCount, LastShownUtc = PersistedTime.Format(record.LastShownUtc) });
			}

			_capRecords.Clear();
		}

		/// <summary>The caps as saved: <c>{"Placements":[…],"Types":[…]}</c>, one entry per key with an impression.</summary>
		[Serializable]
		private sealed class SavedCaps
		{
			public List<SavedCap> Placements = new();
			public List<SavedCap> Types      = new();
		}

		[Serializable]
		private struct SavedCap
		{
			public string Key;

			/// <summary>Impressions on the UTC day of <see cref="LastShownUtc"/>.</summary>
			public int DayCount;

			/// <summary>The last impression, as <see cref="PersistedTime"/> text.</summary>
			public string LastShownUtc;
		}

		/// <summary>Forgets every impression when the cap store deletes its data, so the next save can't write them back.</summary>
		private sealed class CapStoreListener : IStoreResetListener
		{
			private readonly AdService _service;

			public CapStoreListener(AdService service) => _service = service;

			public void OnStoreReset()
			{
				_service._placementCaps.Clear();
				_service._typeCaps.Clear();
			}
		}

		#endregion

		#region Helpers

		private bool CanWork() => _isInitialized && !_disposed && !_adsDisabled;

		private bool AnyProviderStarted()
		{
			foreach (IAdProvider provider in _providers)
			{
				if (provider.IsInitialized)
				{
					return true;
				}
			}

			return false;
		}

		private AdPlacementDefinition FindPlacement(string placementId)
		{
			if (string.IsNullOrEmpty(placementId))
			{
				return null;
			}

			return _placementById.TryGetValue(placementId, out AdPlacementDefinition placement)
				? placement
				: _adsMeta?.GetPlacementByID(placementId);
		}

		/// <summary>Whether a started provider has the unit's ad: the networks' truth, which the unit machines follow.</summary>
		private bool IsAvailable(AdUnit unit) => HasAd(unit.Owner.PlacementID, unit.Type);

		private bool HasAd(string placementId, AdType adType)
		{
			if (string.IsNullOrEmpty(placementId))
			{
				return false;
			}

			foreach (IAdProvider provider in _providers)
			{
				if (Serves(provider, adType) && provider.IsAdReady(placementId, adType))
				{
					return true;
				}
			}

			return false;
		}

		private static bool Serves(IAdProvider provider, AdType adType)
		{
			if (!provider.IsInitialized)
			{
				return false;
			}

			IReadOnlyList<AdType> supported = provider.SupportedAdTypes;
			for (int i = 0; i < supported.Count; i++)
			{
				if (supported[i] == adType)
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsRewarded(AdType adType) => adType is AdType.Rewarded or AdType.RewardedInterstitial;

		/// <summary>
		/// Waits for <paramref name="task"/> for at most <paramref name="seconds"/> on the clock.
		/// False when the time ran out first.
		/// </summary>
		private async UniTask<bool> WaitAsync(UniTask task, double seconds, CancellationToken cancellationToken)
		{
			using CancellationTokenSource timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
			try
			{
				return await UniTask.WhenAny(task, _clock.Delay(seconds, timer.Token)) == 0;
			}
			finally
			{
				timer.Cancel();
			}
		}

		/// <summary>Waits for a load for at most <paramref name="seconds"/> on the clock.</summary>
		private async UniTask<(bool Finished, AdLoadResult Result)> WaitForLoadAsync(UniTask<AdLoadResult> load, double seconds, CancellationToken cancellationToken)
		{
			using CancellationTokenSource timer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
			try
			{
				(bool finished, AdLoadResult result) = await UniTask.WhenAny(load, _clock.Delay(seconds, timer.Token));
				return (finished, result);
			}
			finally
			{
				timer.Cancel();
			}
		}

		/// <summary>Raises an event one handler at a time, so a handler that throws can't stop the others, or the show.</summary>
		private static void Raise(Action handlers)
		{
			if (handlers == null)
			{
				return;
			}

			foreach (Delegate handler in handlers.GetInvocationList())
			{
				try
				{
					((Action)handler).Invoke();
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}
		}

		private static void Raise<T>(Action<T> handlers, T arg)
		{
			if (handlers == null)
			{
				return;
			}

			foreach (Delegate handler in handlers.GetInvocationList())
			{
				try
				{
					((Action<T>)handler).Invoke(arg);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}
		}

		private static void Raise<T1, T2>(Action<T1, T2> handlers, T1 arg1, T2 arg2)
		{
			if (handlers == null)
			{
				return;
			}

			foreach (Delegate handler in handlers.GetInvocationList())
			{
				try
				{
					((Action<T1, T2>)handler).Invoke(arg1, arg2);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}
		}

		private static void Raise<T1, T2, T3>(Action<T1, T2, T3> handlers, T1 arg1, T2 arg2, T3 arg3)
		{
			if (handlers == null)
			{
				return;
			}

			foreach (Delegate handler in handlers.GetInvocationList())
			{
				try
				{
					((Action<T1, T2, T3>)handler).Invoke(arg1, arg2, arg3);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}
		}

		#endregion

		/// <summary>An ad unit: its machine, the load in flight and the pending wait.</summary>
		private sealed class AdUnit
		{
			/// <summary>The first placement on the unit. Its strategy sets how the unit loads, and providers load under its ID.</summary>
			public readonly AdPlacementDefinition Owner;

			public readonly string Id;
			public readonly AdType Type;

			/// <summary>The unit is kept loaded ahead of time.</summary>
			public readonly bool Preload;

			public readonly AdUnitStateMachine Machine;
			public readonly List<AdPlacementDefinition> Placements = new(1);

			/// <summary>The newest load. Everyone who wants the ad meanwhile waits for it.</summary>
			public UniTaskCompletionSource<AdLoadResult> Attempt;

			/// <summary>Runs out the pending retry or reload wait.</summary>
			public CancellationTokenSource Timer;

			public AdUnit(AdPlacementDefinition owner, AdUnitPolicy policy, bool preload)
			{
				Owner   = owner;
				Id      = owner.AdUnitID;
				Type    = owner.AdType;
				Preload = preload;
				Machine = new AdUnitStateMachine(policy);
			}
		}

		private readonly struct UnitKey : IEquatable<UnitKey>
		{
			private readonly AdType _type;
			private readonly string _id;

			public UnitKey(AdType type, string id)
			{
				_type = type;
				_id   = id;
			}

			public bool Equals(UnitKey other) => _type == other._type && string.Equals(_id, other._id, StringComparison.Ordinal);

			public override bool Equals(object obj) => obj is UnitKey other && Equals(other);

			public override int GetHashCode() => unchecked((int)_type * 397 ^ (_id != null ? _id.GetHashCode() : 0));
		}
	}
}
