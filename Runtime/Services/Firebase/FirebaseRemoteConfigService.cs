#if UGFW_FIREBASE_REMOTE_CONFIG
using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain.RemoteConfig;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if !UNITY_WEBGL
using System.Threading.Tasks;
using Firebase.RemoteConfig;
#endif

namespace AK.Services
{
	/// <summary>
	/// Remote config from the Firebase Remote Config SDK. The
	/// <see cref="IFirebaseInitializationService"/> must have run first.
	///
	/// <para><b>Values.</b> Only values set in the Firebase console
	/// (<c>ValueSource.RemoteValue</c>) are applied. Firebase answers every other key with the
	/// in-app default this service gives it, and that never counts as a remote value. Until
	/// Firebase has fetched successfully once on the device, nothing is applied, and the cached
	/// values hold.</para>
	///
	/// <para><b>Fetching.</b> A fetch goes to the network only when the values Firebase holds
	/// are older than the cache expiration; otherwise Firebase answers from them. Firebase
	/// throttles clients that fetch too often.</para>
	///
	/// <para>Main thread only. Compiled only with com.google.firebase.remote-config installed.
	/// Firebase doesn't run on WebGL, where only the cached and default values are used.</para>
	/// </summary>
	public sealed class FirebaseRemoteConfigService : IRemoteConfigService
	{
		private const string TAG = "[FirebaseRemoteConfigService]";

		/// <summary>The SDK's own default: a fetch goes to the network once the values are 12 hours old.</summary>
		public static readonly TimeSpan DefaultCacheExpiration = TimeSpan.FromHours(12);

		private readonly RemoteConfigMeta               _meta;
		private readonly IFirebaseInitializationService _firebaseInit;
		private readonly TimeSpan                       _cacheExpiration;
		private readonly PrefsStore                     _cache;

		private bool _isInitialized;

		public bool IsInitialized => _isInitialized;

		/// <param name="remoteConfigMeta">The variables to apply values to.</param>
		/// <param name="firebaseInit">Firebase's initialization, which must have run first.</param>
		/// <param name="cacheExpiration">
		/// How old the values Firebase holds may get before a fetch goes to the network; null for
		/// <see cref="DefaultCacheExpiration"/>. It isn't a network timeout.
		/// </param>
		/// <param name="cache">Where the values are cached between sessions; null for <see cref="UniPrefs.Store"/>.</param>
		/// <exception cref="ArgumentNullException"><paramref name="firebaseInit"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="cacheExpiration"/> is negative.</exception>
		public FirebaseRemoteConfigService(
			RemoteConfigMeta remoteConfigMeta,
			IFirebaseInitializationService firebaseInit,
			TimeSpan? cacheExpiration = null,
			PrefsStore cache = null)
		{
			_meta            = remoteConfigMeta;
			_firebaseInit    = firebaseInit ?? throw new ArgumentNullException(nameof(firebaseInit));
			_cacheExpiration = cacheExpiration ?? DefaultCacheExpiration;
			_cache           = cache ?? UniPrefs.Store;

			if (_cacheExpiration < TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException(nameof(cacheExpiration), _cacheExpiration, "Must be zero or more.");
			}
		}

		public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (_isInitialized)
			{
				Debug.LogWarning($"{TAG} Already initialized");
				return;
			}

			if (_meta == null)
			{
				Debug.LogError($"{TAG} No RemoteConfigMeta, so no remote values can be applied.");
				_isInitialized = true;
				return;
			}

			// The last fetch's values come first, so they hold if Firebase never answers.
			int cached = _meta.LoadCachedValues(_cache);
			Debug.Log($"{TAG} Loaded {cached} cached values");

			if (!_firebaseInit.CheckAvailable())
			{
				Debug.LogWarning($"{TAG} Firebase isn't available ({_firebaseInit.UnavailableReason}); using cached and default values.");
				_isInitialized = true;
				return;
			}

#if !UNITY_WEBGL
			FirebaseRemoteConfig config = FirebaseRemoteConfig.DefaultInstance;
			await RunAsync(config.SetDefaultsAsync(DefaultsOf(_meta)), "Setting the defaults", cancellationToken);
			await RunAsync(config.FetchAsync(_cacheExpiration), "The fetch", cancellationToken);
			await RunAsync(config.ActivateAsync(), "Activation", cancellationToken);
			Apply(config);
#else
			Debug.LogWarning($"{TAG} Firebase doesn't run on WebGL; using cached and default values.");
			await UniTask.CompletedTask;
#endif

			_isInitialized = true;
			Debug.Log($"{TAG} Initialization complete");
		}

		public async UniTask FetchAsync(CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

#if !UNITY_WEBGL
			if (CanUseFirebase())
			{
				await RunAsync(FirebaseRemoteConfig.DefaultInstance.FetchAsync(_cacheExpiration), "The fetch", cancellationToken);
			}
#else
			Debug.LogWarning($"{TAG} Firebase doesn't run on WebGL; nothing to fetch.");
			await UniTask.CompletedTask;
#endif
		}

		public async UniTask ActivateAsync(CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

#if !UNITY_WEBGL
			if (CanUseFirebase())
			{
				FirebaseRemoteConfig config = FirebaseRemoteConfig.DefaultInstance;
				await RunAsync(config.ActivateAsync(), "Activation", cancellationToken);
				Apply(config);
			}
#else
			await UniTask.CompletedTask;
#endif
		}

		public async UniTask FetchAndActivateAsync(CancellationToken cancellationToken = default)
		{
			await FetchAsync(cancellationToken);
			await ActivateAsync(cancellationToken);
		}

#if !UNITY_WEBGL
		private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		private bool CanUseFirebase()
		{
			if (_meta != null && _firebaseInit.CheckAvailable())
			{
				return true;
			}

			Debug.LogWarning($"{TAG} Skipped: {(_meta == null ? "there is no RemoteConfigMeta" : "Firebase isn't available")}.");
			return false;
		}

		/// <summary>Applies the values Firebase has activated, as one whole fetch.</summary>
		private void Apply(FirebaseRemoteConfig config)
		{
			// Before its first successful fetch, Firebase answers every key from its defaults.
			// Applying those would wipe the cached values the game is running on.
			if (config.Info.FetchTime <= UnixEpoch)
			{
				Debug.LogWarning($"{TAG} Firebase hasn't fetched values on this device yet; using cached and default values.");
				return;
			}

			var fetched = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (RemoteVariableBase variable in _meta.GetAllVariables())
			{
				if (variable == null || !variable.IsEnabled || string.IsNullOrEmpty(variable.VariableKey))
				{
					continue;
				}

				ConfigValue value = config.GetValue(variable.VariableKey);
				if (value.Source == ValueSource.RemoteValue)
				{
					fetched[variable.VariableKey] = value.StringValue;
				}
			}

			RemoteConfigUpdate update = _meta.ApplyFetchedValues(fetched, _cache);
			Debug.Log($"{TAG} Applied Firebase's values: {update}");
		}

		/// <summary>The in-app defaults, as text: Firebase stores any other object as its type's name.</summary>
		private static Dictionary<string, object> DefaultsOf(RemoteConfigMeta meta)
		{
			Dictionary<string, string> texts = meta.GetDefaultValueTexts();
			var defaults = new Dictionary<string, object>(texts.Count, StringComparer.Ordinal);
			foreach (KeyValuePair<string, string> pair in texts)
			{
				defaults.Add(pair.Key, pair.Value);
			}

			return defaults;
		}

		/// <summary>
		/// Waits for a Firebase task, back on the main thread. A failure is logged, not thrown.
		/// Cancelling stops the wait; the task itself runs on.
		/// </summary>
		private static async UniTask RunAsync(Task task, string step, CancellationToken cancellationToken)
		{
			try
			{
				await task.AsUniTask().AttachExternalCancellation(cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception e)
			{
				Debug.LogError($"{TAG} {step} failed: {e.GetBaseException().Message}");
			}
		}
#endif
	}
}
#endif
