using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

#if !UNITY_WEBGL
using AK.Core.Extensions;
using AK.Kernel.Timing;
using Firebase.Extensions;
#endif

namespace AK.Services
{
	/// <summary>
	/// <para>Centralized Firebase initialization service.
	/// Ensures Firebase dependencies are checked before any Firebase API is used.
	/// This service must be initialized before any other Firebase-dependent services.</para>
	///
	/// <para>Firebase is initialized once: every call to <see cref="InitializeAsync"/> shares the
	/// first one's initialization, and a call after it has ended returns its result. A dependency
	/// check that hasn't answered within <see cref="DependencyCheckTimeoutSeconds"/> of foreground
	/// time counts as unavailable; if it answers later that Firebase is available, Firebase is
	/// available from then on. Main thread only.</para>
	///
	/// <para>AK.Services.Firebase compiles only with com.google.firebase.app installed. Firebase
	/// doesn't run on WebGL, where it is always unavailable.</para>
	/// </summary>
	public class FirebaseInitializationService : IFirebaseInitializationService
	{
		/// <summary>How long the dependency check may take, in seconds of foreground time.</summary>
		public const double DependencyCheckTimeoutSeconds = 30d;

		private bool _isInitialized;
		private bool _isAvailable;
		private string _unavailableReason;

		// Runs the initialization: Firebase's, or a test's.
		private readonly Func<UniTask<bool>> _run;

		// The one initialization, shared by every call once started.
		private UniTask<bool> _initialization;
		private bool          _started;

		public FirebaseInitializationService()
		{
			_run = RunAsync;
		}

		/// <summary>For tests: <paramref name="run"/> is the initialization, in place of Firebase's.</summary>
		internal FirebaseInitializationService(Func<UniTask<bool>> run)
		{
			_run = run;
		}

		public bool IsInitialized => _isInitialized;
		public bool IsAvailable => _isAvailable;
		public string UnavailableReason => _unavailableReason;

#if !UNITY_WEBGL
		// Held for the service's lifetime, as Firebase advises.
		private Firebase.FirebaseApp _app;

		/// <summary>The default Firebase app once Firebase is available; null until then.</summary>
		public Firebase.FirebaseApp App => _app;
#endif

		/// <summary>
		/// Event fired when Firebase initialization completes successfully. Never on WebGL.
		/// </summary>
#pragma warning disable CS0067 // Unused on WebGL, where Firebase never initializes.
		public event Action OnInitialized;
#pragma warning restore CS0067

		/// <summary>
		/// Event fired when Firebase initialization fails.
		/// </summary>
		public event Action<string> OnInitializationFailed;

		/// <summary>
		/// Initializes Firebase by checking and fixing dependencies, once. Must be called and
		/// awaited before using any Firebase API. Cancelling <paramref name="cancellationToken"/>
		/// ends this call's wait with an <see cref="OperationCanceledException"/>; the
		/// initialization goes on, and a later call waits for it.
		/// </summary>
		/// <returns>True if Firebase is available, false otherwise.</returns>
		public UniTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
		{
			if (!_started)
			{
				_started = true;
				_initialization = _run().Preserve();
			}

			return _initialization.AttachExternalCancellation(cancellationToken);
		}

#if !UNITY_WEBGL
		private async UniTask<bool> RunAsync()
		{
			try
			{
				Debug.Log("[FirebaseInitializationService] Checking Firebase dependencies...");

				var answer = new UniTaskCompletionSource<bool>();

				// The continuation answers through the completion source; its own task isn't awaited.
				_ = Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
				{
					// Fault/cancel handling comes first: task.Result on a faulted task throws,
					// which would escape this callback and leave the answer (and the awaiter) hanging forever.
					if (task.IsFaulted || task.IsCanceled)
					{
						_isAvailable = false;
						_unavailableReason = task.Exception?.GetBaseException().Message ?? "Dependency check was canceled";
						Debug.LogError($"[FirebaseInitializationService] Dependency check failed: {_unavailableReason}");
						OnInitializationFailed?.Invoke(_unavailableReason);
						answer.TrySetResult(false);
						return;
					}

					var dependencyStatus = task.Result;

					if (dependencyStatus == Firebase.DependencyStatus.Available)
					{
						_app = Firebase.FirebaseApp.DefaultInstance;
						_isAvailable = true;
						Debug.Log("[FirebaseInitializationService] Firebase is available and ready to use");
						OnInitialized?.Invoke();
						answer.TrySetResult(true);
					}
					else
					{
						_isAvailable = false;
						_unavailableReason = $"Dependency status: {dependencyStatus}";
						Debug.LogError($"[FirebaseInitializationService] Firebase unavailable: {_unavailableReason}");
						OnInitializationFailed?.Invoke(_unavailableReason);
						answer.TrySetResult(false);
					}
				});

				// Timeout guard: a lost native callback must never hang the caller forever.
				using var timeout = new CancellationTokenSource();
				(bool answered, bool available) = await UniTask.WhenAny(answer.Task, TimeDomain.Unscaled.Delay(DependencyCheckTimeoutSeconds, timeout.Token));
				timeout.Cancel();

				if (!answered)
				{
					throw new TimeoutException($"The dependency check didn't answer within {DependencyCheckTimeoutSeconds:0} s.");
				}

				_isInitialized = true;
				return available;
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
		}
#else
		private UniTask<bool> RunAsync()
		{
			_isInitialized = true;
			_isAvailable = false;
			_unavailableReason = "Firebase doesn't run on WebGL";
			Debug.LogWarning($"[FirebaseInitializationService] {_unavailableReason}.");
			OnInitializationFailed?.Invoke(_unavailableReason);
			return UniTask.FromResult(false);
		}
#endif

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
		/// Whether the initialization has ended (regardless of success).
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
		/// Initializes Firebase by checking dependencies, once: every call shares the first
		/// one's initialization. Cancelling <paramref name="cancellationToken"/> ends this call's
		/// wait with an <see cref="OperationCanceledException"/>, not the initialization.
		/// </summary>
		/// <returns>True if Firebase is available.</returns>
		UniTask<bool> InitializeAsync(CancellationToken cancellationToken = default);

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
