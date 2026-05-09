using System;
using Cysharp.Threading.Tasks;
using GameplayCore.MetaData;
using GameplayCore.MetaData.RemoteConfig;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// Firebase Remote Config service implementation.
	/// Initializes remote variables from RemoteConfigMeta and applies fetched values.
	/// Requires IFirebaseInitializationService to be initialized first.
	/// </summary>
	public class FirebaseRemoteConfigService : IRemoteConfigService
	{
		private readonly RemoteConfigMeta _remoteConfigMeta;
		private readonly IFirebaseInitializationService _firebaseInit;
		private readonly TimeSpan _fetchTimeout;

		private bool _isInitialized;

		public bool IsInitialized => _isInitialized;

		/// <summary>
		/// Creates a new FirebaseRemoteConfigService.
		/// </summary>
		/// <param name="remoteConfigMeta">The meta data repository containing RemoteConfigMeta.</param>
		/// <param name="firebaseInit">Firebase initialization service (must be initialized first).</param>
		/// <param name="fetchTimeoutSeconds">Timeout for fetch operations. Default is 10 seconds.</param>
		public FirebaseRemoteConfigService(
			RemoteConfigMeta remoteConfigMeta,
			IFirebaseInitializationService firebaseInit,
			int fetchTimeoutSeconds = 10)
		{
			_remoteConfigMeta = remoteConfigMeta;
			_firebaseInit = firebaseInit;
			_fetchTimeout = TimeSpan.FromSeconds(fetchTimeoutSeconds);
		}

		public async UniTask InitializeAsync(RemoteConfigMeta remoteConfigMeta)
		{
			if (_isInitialized)
			{
				Debug.LogWarning("[FirebaseRemoteConfigService] Already initialized");
				return;
			}

			// Check if Firebase is available
			if (!_firebaseInit.CheckAvailable())
			{
				Debug.LogWarning($"[FirebaseRemoteConfigService] Firebase not available: {_firebaseInit.UnavailableReason}. Using cached/default values.");
				_isInitialized = true;
				return;
			}

			if (remoteConfigMeta == null)
			{
				Debug.LogError("[FirebaseRemoteConfigService] RemoteConfigMeta is null in MetaDataRepository!");
				_isInitialized = true;
				return;
			}

			try
			{
				// Step 1: Load any cached values first (for offline support)
				remoteConfigMeta.LoadAllCachedValues();
				Debug.Log("[FirebaseRemoteConfigService] Loaded cached values");

				// Step 2: Get defaults from RemoteConfigMeta
				var defaults = remoteConfigMeta.GetDefaultsForFirebase();
				Debug.Log($"[FirebaseRemoteConfigService] Got {defaults.Count} default values");

				// Step 3: Set defaults in Firebase
				await SetDefaultsAsync(defaults);
				Debug.Log("[FirebaseRemoteConfigService] Set defaults in Firebase");

				// Step 4: Fetch from server
				await FetchAsync();
				Debug.Log("[FirebaseRemoteConfigService] Fetched values from server");

				// Step 5: Activate fetched values
				await ActivateAsync();
				Debug.Log("[FirebaseRemoteConfigService] Activated fetched values");

				// Step 6: Apply values to RemoteVariables
				ApplyFetchedValuesToVariables(remoteConfigMeta);
				Debug.Log("[FirebaseRemoteConfigService] Applied values to variables");

				_isInitialized = true;
				Debug.Log("[FirebaseRemoteConfigService] Initialization complete");
			}
			catch (Exception e)
			{
				Debug.LogError($"[FirebaseRemoteConfigService] Initialization failed: {e.Message}");
				// Still mark as initialized so we can use cached/default values
				_isInitialized = true;
			}
		}

		public async UniTask FetchAsync()
		{
			try
			{
#if FIREBASE_REMOTE_CONFIG
				var remoteConfig = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance;
				await remoteConfig.FetchAsync(_fetchTimeout).AsUniTask();
				Debug.Log("[FirebaseRemoteConfigService] Fetch complete");
#else
				Debug.LogWarning("[FirebaseRemoteConfigService] Firebase Remote Config not available. Using defaults.");
				await UniTask.CompletedTask;
#endif
			}
			catch (Exception e)
			{
				Debug.LogError($"[FirebaseRemoteConfigService] Fetch failed: {e.Message}");
			}
		}

		public async UniTask ActivateAsync()
		{
			try
			{
#if FIREBASE_REMOTE_CONFIG
				var remoteConfig = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance;
				await remoteConfig.ActivateAsync().AsUniTask();
				Debug.Log("[FirebaseRemoteConfigService] Activate complete");
#else
				await UniTask.CompletedTask;
#endif
			}
			catch (Exception e)
			{
				Debug.LogError($"[FirebaseRemoteConfigService] Activate failed: {e.Message}");
			}
		}

		public async UniTask FetchAndActivateAsync()
		{
			await FetchAsync();
			await ActivateAsync();
			ApplyFetchedValuesToVariables(_remoteConfigMeta);
		}

		private async UniTask SetDefaultsAsync(System.Collections.Generic.Dictionary<string, object> defaults)
		{
			try
			{
#if FIREBASE_REMOTE_CONFIG
				var remoteConfig = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance;
				await remoteConfig.SetDefaultsAsync(defaults).AsUniTask();
#else
				await UniTask.CompletedTask;
#endif
			}
			catch (Exception e)
			{
				Debug.LogError($"[FirebaseRemoteConfigService] SetDefaults failed: {e.Message}");
			}
		}

		private void ApplyFetchedValuesToVariables(RemoteConfigMeta remoteConfigMeta)
		{
			if (remoteConfigMeta == null)
				return;

			var enabledVariables = remoteConfigMeta.GetEnabledVariables();
			Debug.Log($"[FirebaseRemoteConfigService] Applying values to {enabledVariables.Count} enabled variables");

			foreach (var variable in enabledVariables)
			{
				try
				{
					string value = GetRemoteValue(variable.VariableKey);
					if (!string.IsNullOrEmpty(value))
					{
						variable.SetRemoteValueFromString(value);
					}
				}
				catch (Exception e)
				{
					Debug.LogError($"[FirebaseRemoteConfigService] Failed to apply value for '{variable.VariableKey}': {e.Message}");
				}
			}
		}

		private string GetRemoteValue(string key)
		{
#if FIREBASE_REMOTE_CONFIG
			var remoteConfig = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance;
			return remoteConfig.GetValue(key).StringValue;
#else
			return null;
#endif
		}
	}
}