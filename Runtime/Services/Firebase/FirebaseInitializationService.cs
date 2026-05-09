using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

#if FIREBASE_INITIALIZATION
using Firebase.Extensions;
#endif

namespace AK.Services
{
	/// <summary>
	/// Centralized Firebase initialization service.
	/// Ensures Firebase dependencies are checked before any Firebase API is used.
	/// This service must be initialized before any other Firebase-dependent services.
	/// </summary>
	public class FirebaseInitializationService : IFirebaseInitializationService
	{
		private bool _isInitialized;
		private bool _isAvailable;
		private string _unavailableReason;

		public bool IsInitialized => _isInitialized;
		public bool IsAvailable => _isAvailable;
		public string UnavailableReason => _unavailableReason;

		/// <summary>
		/// Event fired when Firebase initialization completes successfully.
		/// </summary>
		public event Action OnInitialized;

		/// <summary>
		/// Event fired when Firebase initialization fails.
		/// </summary>
		public event Action<string> OnInitializationFailed;

		/// <summary>
		/// Initializes Firebase by checking and fixing dependencies.
		/// Must be called and awaited before using any Firebase API.
		/// </summary>
		/// <returns>True if Firebase is available, false otherwise.</returns>
		public async UniTask<bool> InitializeAsync()
		{
			if (_isInitialized)
			{
				Debug.Log("[FirebaseInitializationService] Already initialized");
				return _isAvailable;
			}

#if FIREBASE_INITIALIZATION
			try
			{
				Debug.Log("[FirebaseInitializationService] Checking Firebase dependencies...");

				var tcs = new UniTaskCompletionSource<bool>();

				Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
				{
					var dependencyStatus = task.Result;

					if (dependencyStatus == Firebase.DependencyStatus.Available)
					{
						// Firebase is available - create and hold reference to default instance
						var app = Firebase.FirebaseApp.DefaultInstance;
						_isAvailable = true;
						Debug.Log("[FirebaseInitializationService] Firebase is available and ready to use");
						OnInitialized?.Invoke();
						tcs.TrySetResult(true);
					}
					else
					{
						_isAvailable = false;
						_unavailableReason = $"Dependency status: {dependencyStatus}";
						Debug.LogError($"[FirebaseInitializationService] Firebase unavailable: {_unavailableReason}");
						OnInitializationFailed?.Invoke(_unavailableReason);
						tcs.TrySetResult(false);
					}
				});

				var result = await tcs.Task;
				_isInitialized = true;
				return result;
			}
			catch (Exception e)
			{
				_isAvailable = false;
				_unavailableReason = e.Message;
				_isInitialized = true;
				Debug.LogError($"[FirebaseInitializationService] Initialization failed: {e.Message}");
				OnInitializationFailed?.Invoke(e.Message);
				return false;
			}
#else
			Debug.LogWarning("[FirebaseInitializationService] Firebase SDK not integrated. Define FIREBASE_INITIALIZATION to enable.");
			_isInitialized = true;
			_isAvailable = false;
			_unavailableReason = "Firebase SDK not integrated";
			return false;
#endif
		}

		/// <summary>
		/// Throws an exception if Firebase is not available.
		/// Use this to guard Firebase API calls.
		/// </summary>
		public void EnsureAvailable()
		{
			if (!_isInitialized)
			{
				throw new InvalidOperationException("Firebase not initialized. Call InitializeAsync() first.");
			}

			if (!_isAvailable)
			{
				throw new InvalidOperationException($"Firebase not available: {_unavailableReason}");
			}
		}

		/// <summary>
		/// Checks if Firebase is available without throwing.
		/// Returns true if Firebase is ready to use.
		/// </summary>
		public bool CheckAvailable()
		{
			return _isInitialized && _isAvailable;
		}
	}

	/// <summary>
	/// Interface for Firebase initialization service.
	/// </summary>
	public interface IFirebaseInitializationService
	{
		/// <summary>
		/// Whether InitializeAsync has been called (regardless of success).
		/// </summary>
		bool IsInitialized { get; }

		/// <summary>
		/// Whether Firebase is available and ready to use.
		/// </summary>
		bool IsAvailable { get; }

		/// <summary>
		/// Reason why Firebase is unavailable, if applicable.
		/// </summary>
		string UnavailableReason { get; }

		/// <summary>
		/// Initializes Firebase by checking dependencies.
		/// </summary>
		/// <returns>True if Firebase is available.</returns>
		UniTask<bool> InitializeAsync();

		/// <summary>
		/// Checks if Firebase is available without throwing.
		/// </summary>
		bool CheckAvailable();

		/// <summary>
		/// Throws if Firebase is not available.
		/// </summary>
		void EnsureAvailable();
	}
}