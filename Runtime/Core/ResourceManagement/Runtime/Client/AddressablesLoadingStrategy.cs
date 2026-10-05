#if UGFW_ADDRESSABLES
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using AK.Kernel.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using AK.Core.Extensions;
using Object = UnityEngine.Object;

namespace AK.Core.ResourceManagement
{
	/// <summary>
	/// <see cref="IResourceLoadingStrategy"/> over Addressables.
	///
	/// Ownership: every object handed out by a load or spawn is one claim, backed by the
	/// Addressables handle that produced it. <see cref="DisposeAsset"/> and
	/// <see cref="DisposeInstance"/> give back exactly one claim and release exactly that
	/// handle, so loading a key N times takes N disposes, and Addressables' own reference
	/// count stays balanced. Claims on objects that were destroyed without a dispose (an
	/// instance that died with its scene, an asset destroyed by hand) are released when the
	/// next scene unloads.
	///
	/// Failures surface as exceptions (Addressables' <see cref="OperationException"/> or
	/// cancellation); nothing that failed reports success. Main thread only.
	///
	/// Synchronous loads wait for Addressables, except on WebGL, where Addressables runs on the
	/// main thread and nothing can wait. There they return only what is already loaded: a Try
	/// method answers false, and the others throw <see cref="NotSupportedException"/>.
	/// </summary>
	public sealed class AddressablesLoadingStrategy : IResourceLoadingStrategy
	{
		private readonly Dictionary<Guid, AsyncOperationHandle> _groupOperationsLookup = new();
		private readonly ClaimTable<Object, AsyncOperationHandle> _claims = new(64);

		// Scratch for releasing in bulk without allocating; only touched on the main thread.
		private readonly List<AsyncOperationHandle> _releaseBuffer = new();
		private readonly List<Object> _keyBuffer = new();
		private bool _watchingSceneUnloads;

		// Whether a synchronous load may wait for an operation that isn't done. Addressables' own
		// WaitForCompletion throws on WebGL for anything not done yet.
		private readonly bool _canBlock;

#if UNITY_WEBGL && !UNITY_EDITOR
		private const bool PlatformCanBlock = false;
#else
		private const bool PlatformCanBlock = true;
#endif

		public AddressablesLoadingStrategy() : this(PlatformCanBlock)
		{
		}

		/// <param name="canBlock">False acts as WebGL, for tests.</param>
		internal AddressablesLoadingStrategy(bool canBlock)
		{
			_canBlock = canBlock;
		}

		/// <summary>Outstanding claims: loads and spawns not yet disposed.</summary>
		public int ClaimCount => _claims.ClaimCount;

		/// <summary>Distinct objects holding at least one claim.</summary>
		public int ClaimedObjectCount => _claims.Count;

		/// <summary>Claims currently held on <paramref name="uObject"/>.</summary>
		public int ClaimsOf(Object uObject) => ReferenceEquals(uObject, null) ? 0 : _claims.ClaimsOf(uObject);

		public UniTask InitAsync(CancellationToken cToken = default)
		{
			return Addressables.InitializeAsync().ToUniTask(cancellationToken: cToken);
		}

		public async UniTask<bool> HasResourceAsync(string key, Type type = null, CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			var handle = Addressables.LoadResourceLocationsAsync(key, type);
			try
			{
				var locations = await handle.WithCancellation(cToken);
				return locations.Count > 0;
			}
			finally
			{
				ReleaseIfValid(handle);
			}
		}

		public async UniTask<IList<IResourceLocation>> GetResourceLocationsAsync(IEnumerable<string> keys, Type type, MergeMode mode,
		                                                                         CancellationToken cToken = default)
		{
			var handle = Addressables.LoadResourceLocationsAsync(keys, mode.Convert(), type);
			try
			{
				return await handle.ToUniTask(cancellationToken: cToken);
			}
			finally
			{
				ReleaseIfValid(handle);
			}
		}

		public async UniTask<IList<IResourceLocation>> GetAllResourceLocationsAsync(Type type = null, CancellationToken cToken = default)
		{
			var handle = Addressables.LoadResourceLocationsAsync("*", type);
			try
			{
				return await handle.ToUniTask(cancellationToken: cToken);
			}
			finally
			{
				ReleaseIfValid(handle);
			}
		}

		// --------------------------------------------------------------------------
		// CATALOG UPDATES
		// --------------------------------------------------------------------------

		public async UniTask<List<string>> CheckForCatalogUpdatesAsync(CancellationToken cToken = default)
		{
			var handle = Addressables.CheckForCatalogUpdates(false);
			try
			{
				return await handle.ToUniTask(cancellationToken: cToken) ?? new List<string>();
			}
			finally
			{
				ReleaseIfValid(handle);
			}
		}

		public async UniTask UpdateCatalogsAsync(IEnumerable<string> catalogs = null, bool autoCleanBundleCache = false,
		                                         CancellationToken cToken = default)
		{
			var handle = autoCleanBundleCache
				? Addressables.UpdateCatalogs(true, catalogs, false)
				: Addressables.UpdateCatalogs(catalogs, false);

			try
			{
				// UniTask surfaces a failed operation as its OperationException.
				await handle.ToUniTask(cancellationToken: cToken);
			}
			finally
			{
				ReleaseIfValid(handle);
			}
		}

		public async UniTask<bool> UpdateCatalogsIfNeededAsync(bool autoCleanBundleCache = false, CancellationToken cToken = default)
		{
			var catalogIds = await CheckForCatalogUpdatesAsync(cToken);

			if (catalogIds.Count == 0)
				return false;

			await UpdateCatalogsAsync(catalogIds, autoCleanBundleCache, cToken);
			return true;
		}

		// --------------------------------------------------------------------------
		// CONTENT DOWNLOADS
		// --------------------------------------------------------------------------

		/// <inheritdoc />
		public async UniTask<long> GetRemoteContentSizeAsync(string[] labels = null, CancellationToken cToken = default)
		{
			// If no labels are provided, grab all known keys.
			// Addressables natively strips duplicates and local files, returning the exact remote size instantly.
			if (labels == null || labels.Length == 0)
			{
				var allKeys = Addressables.ResourceLocators.SelectMany(x => x.Keys);
				var handle = Addressables.GetDownloadSizeAsync(allKeys);
				try
				{
					return await handle.ToUniTask(cancellationToken: cToken);
				}
				finally
				{
					ReleaseIfValid(handle);
				}
			}

			var locations = await ResolveRemoteLocationsAsync(labels, cToken);
			if (locations.Count == 0) return 0;

			var locHandle = Addressables.GetDownloadSizeAsync(locations);
			try
			{
				return await locHandle.ToUniTask(cancellationToken: cToken);
			}
			finally
			{
				ReleaseIfValid(locHandle);
			}
		}

		/// <inheritdoc />
		public async UniTask<long> DownloadRemoteContentAsync(string[] labels = null, IProgress<float> progress = null,
		                                                      CancellationToken cToken = default)
		{
			AsyncOperationHandle downloadOp;
			long downloadSize = 0;

			if (labels == null || labels.Length == 0)
			{
				// This returns IEnumerable<object> because keys can be strings, GUIDs, or Types.
				var allKeys = Addressables.ResourceLocators.SelectMany(x => x.Keys);

				var sizeHandle = Addressables.GetDownloadSizeAsync(allKeys);
				try
				{
					downloadSize = await sizeHandle.ToUniTask(cancellationToken: cToken);
				}
				finally
				{
					ReleaseIfValid(sizeHandle);
				}

				if (downloadSize == 0)
				{
					progress?.Report(1f);
					return 0;
				}

				downloadOp = Addressables.DownloadDependenciesAsync(allKeys, Addressables.MergeMode.Union, false);
			}
			else
			{
				var locations = await ResolveRemoteLocationsAsync(labels, cToken);
				if (locations.Count == 0)
				{
					progress?.Report(1f);
					return 0;
				}

				downloadSize = await GetRemoteDependenciesSizeAsync(locations, cToken);
				if (downloadSize == 0)
				{
					progress?.Report(1f);
					return 0;
				}

				downloadOp = Addressables.DownloadDependenciesAsync(locations, false);
			}

			// Shared UI Progress Tracker
			try
			{
				while (!downloadOp.IsDone)
				{
					if (downloadOp.IsValid())
					{
						var status = downloadOp.GetDownloadStatus();
						progress?.Report(status.Percent);
					}

					await UniTask.Yield(cToken);
				}

				if (downloadOp.Status == AsyncOperationStatus.Failed)
				{
					// Bundles that finished before the failure stay cached, so a retry resumes.
					throw new OperationException("Remote content download failed.", downloadOp.OperationException);
				}

				progress?.Report(1f);
			}
			finally
			{
				ReleaseIfValid(downloadOp);
			}

			return downloadSize;
		}

		public async UniTask<long> GetRemoteDependenciesSizeAsync(IEnumerable<string> keys, CancellationToken cToken = default)
		{
			var handle = Addressables.GetDownloadSizeAsync(keys);
			try
			{
				return await handle.ToUniTask(cancellationToken: cToken);
			}
			finally
			{
				ReleaseIfValid(handle);
			}
		}

		public async UniTask<long> GetRemoteDependenciesSizeAsync(IList<IResourceLocation> locations, CancellationToken cToken = default)
		{
			var handle = Addressables.GetDownloadSizeAsync(locations);
			try
			{
				return await handle.ToUniTask(cancellationToken: cToken);
			}
			finally
			{
				ReleaseIfValid(handle);
			}
		}

		public UniTask GetRemoteDependenciesAsync(IList<IResourceLocation> locations, out IOperationStatusProvider provider,
		                                          CancellationToken cToken = default)
		{
			var asyncOp = Addressables.DownloadDependenciesAsync(locations, true);
			provider = new OperationStatusProvider(asyncOp);
			return asyncOp.ToUniTask(cancellationToken: cToken);
		}

		public UniTask GetRemoteDependenciesAsync(IEnumerable<string> keys, out IOperationStatusProvider provider,
		                                          MergeMode mode = MergeMode.UseFirst, CancellationToken cToken = default)
		{
			var asyncOp = Addressables.DownloadDependenciesAsync(keys, mode.Convert(), true);
			provider = new OperationStatusProvider(asyncOp);
			return asyncOp.ToUniTask(cancellationToken: cToken);
		}

		// --------------------------------------------------------------------------
		// ASYNC IMPLEMENTATION
		// --------------------------------------------------------------------------

		public async UniTask<TObject> LoadAssetAsync<TObject>(string key, IProgress<float> progress = default, CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			var asyncOp = Addressables.LoadAssetAsync<TObject>(key);
			return await AwaitAndTrack(asyncOp, progress, cToken);
		}

		public async UniTask<TObject> LoadAssetAsync<TObject>(AssetReference reference, IProgress<float> progress = default,
		                                                      CancellationToken cToken = default)
		{
			ValidateReference(reference);
			var asyncOp = Addressables.LoadAssetAsync<TObject>(reference);
			return await AwaitAndTrack(asyncOp, progress, cToken);
		}

		/// <summary>
		/// Async <see cref="TryLoadAssetList{TObject}"/>: every sub-asset of the key
		/// matching TObject (all frames of a sliced sprite sheet). Null when the key
		/// is unknown — the location probe keeps misses silent.
		/// </summary>
		public async UniTask<IList<TObject>> TryLoadAssetListAsync<TObject>(string key, CancellationToken cToken = default)
		{
			if (!await HasLocationsAsync(key, typeof(TObject), cToken))
				return null;

			var op = Addressables.LoadAssetAsync<IList<TObject>>(key);
			return await AwaitAndTrackList(op, cToken);
		}

		public async UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IEnumerable<string> keys, MergeMode mode = MergeMode.UseFirst,
		                                                                    IProgress<float> progress = default, CancellationToken cToken = default)
		{
			var asyncOp = Addressables.LoadAssetsAsync<TObject>(keys, default, mode.Convert());
			try
			{
				var assetsGroup = new AssetsGroup<TObject>(await asyncOp.ToUniTask(progress: progress, cancellationToken: cToken,
					autoReleaseWhenCanceled: true));
				_groupOperationsLookup[assetsGroup.Guid] = asyncOp;
				return assetsGroup;
			}
			catch
			{
				ReleaseIfValid(asyncOp);
				throw;
			}
		}

		public async UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IList<IResourceLocation> keys, IProgress<float> progress = default,
		                                                                    CancellationToken cToken = default)
		{
			var asyncOp = Addressables.LoadAssetsAsync<TObject>(keys, default);
			try
			{
				var assetsGroup = new AssetsGroup<TObject>(await asyncOp.ToUniTask(progress: progress, cancellationToken: cToken,
					autoReleaseWhenCanceled: true));
				_groupOperationsLookup[assetsGroup.Guid] = asyncOp;
				return assetsGroup;
			}
			catch
			{
				ReleaseIfValid(asyncOp);
				throw;
			}
		}

		public async UniTask<GameObject> SpawnAsync(string key, Transform root, IProgress<float> progress = default,
		                                            CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			var asyncOp = Addressables.InstantiateAsync(key, root);
			return await AwaitAndTrack(asyncOp, progress, cToken);
		}

		public async UniTask<GameObject> SpawnAsync(AssetReference reference, Transform root, IProgress<float> progress = default,
		                                            CancellationToken cToken = default)
		{
			ValidateReference(reference);
			var asyncOp = Addressables.InstantiateAsync(reference, root);
			return await AwaitAndTrack(asyncOp, progress, cToken);
		}

		// --------------------------------------------------------------------------
		// SYNCHRONOUS IMPLEMENTATION
		// --------------------------------------------------------------------------

		public TObject LoadAsset<TObject>(string key)
		{
			CheckResourceKey(key);
			var op = Addressables.LoadAssetAsync<TObject>(key);
			return WaitAndTrack(op, key);
		}

		public TObject LoadAsset<TObject>(AssetReference reference)
		{
			ValidateReference(reference);
			var op = Addressables.LoadAssetAsync<TObject>(reference);
			return WaitAndTrack(op, reference);
		}

		/// <summary>
		/// Same as <see cref="LoadAsset{TObject}(string)"/> but missing/empty keys return
		/// false instead of throwing. Probes locations first so Addressables never raises
		/// InvalidKeyException (or logs it) for a key that is simply not in the catalog.
		/// On WebGL, false too for an asset that isn't loaded yet.
		/// </summary>
		public bool TryLoadAsset<TObject>(string key, out TObject asset)
		{
			asset = default;
			if (!HasLocations(key, typeof(TObject)))
				return false;

			var op = Addressables.LoadAssetAsync<TObject>(key);
			return TryWaitAndTrack(op, out asset) && asset != null;
		}

		public bool TryLoadAsset<TObject>(AssetReference reference, out TObject asset)
		{
			asset = default;
			if (reference == null || !reference.RuntimeKeyIsValid())
				return false;
			if (!HasLocations(reference.RuntimeKey, typeof(TObject)))
				return false;

			var op = Addressables.LoadAssetAsync<TObject>(reference);
			return TryWaitAndTrack(op, out asset) && asset != null;
		}

		/// <summary>
		/// All sub-assets of the key matching TObject (every frame of a sliced sprite
		/// sheet). Probes for TObject locations first so missing keys stay silent.
		/// On WebGL, false for a sheet that isn't loaded yet.
		/// </summary>
		public bool TryLoadAssetList<TObject>(string key, out IList<TObject> assets)
		{
			assets = null;
			if (!HasLocations(key, typeof(TObject)))
				return false;

			var op = Addressables.LoadAssetAsync<IList<TObject>>(key);
			return TryWaitAndTrackList(op, out assets) && assets != null && assets.Count > 0;
		}

		public GameObject Spawn(string key, Transform root)
		{
			CheckResourceKey(key);
			var op = Addressables.InstantiateAsync(key, root);
			return WaitAndTrack(op, key);
		}

		public GameObject Spawn(AssetReference reference, Transform root)
		{
			ValidateReference(reference);
			var op = Addressables.InstantiateAsync(reference, root);
			return WaitAndTrack(op, reference);
		}

		// --------------------------------------------------------------------------
		// SCENE LOADING
		// --------------------------------------------------------------------------

		public async UniTask<SceneInstance> LoadSceneAsync(string key, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true,
		                                                   IProgress<float> progress = default, CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			var asyncOp = Addressables.LoadSceneAsync(key, mode, activateOnLoad);
			try
			{
				return await asyncOp.ToUniTask(progress: progress, cancellationToken: cToken);
			}
			catch (OperationCanceledException) when (cToken.IsCancellationRequested)
			{
				// Unity can't abort a scene load midway. An additive scene is unloaded once it lands
				// so neither the scene nor its handle outlives the request; a single-mode load has
				// already replaced everything else, and Addressables releases it with that scene.
				if (mode == LoadSceneMode.Additive)
				{
					UnloadWhenLoadedAsync(asyncOp).Forget();
				}

				throw;
			}
			catch
			{
				ReleaseIfValid(asyncOp);
				throw;
			}
		}

		private static async UniTaskVoid UnloadWhenLoadedAsync(AsyncOperationHandle<SceneInstance> asyncOp)
		{
			try
			{
				await asyncOp.ToUniTask();
			}
			catch
			{
				ReleaseIfValid(asyncOp);
				return;
			}

			if (asyncOp.IsValid())
			{
				// The unload releases the load handle, and auto-releases its own.
				_ = Addressables.UnloadSceneAsync(asyncOp);
			}
		}

		public UniTask UnloadSceneAsync(SceneInstance scene, IProgress<float> progress = default, CancellationToken cToken = default)
		{
			if (scene.Scene.IsValid() == false)
				return UniTask.CompletedTask;

			return Addressables.UnloadSceneAsync(scene).ToUniTask(progress: progress, cancellationToken: cToken);
		}

		// --------------------------------------------------------------------------
		// CLEANUP & HELPERS
		// --------------------------------------------------------------------------

		/// <summary>
		/// Gives back one claim on <paramref name="uObject"/>. A destroyed object still gives its
		/// claim back. Objects this strategy didn't load go to <c>Addressables.Release</c>.
		/// </summary>
		public void DisposeAsset(Object uObject)
		{
			if (ReferenceEquals(uObject, null)) return;

			if (_claims.TryRelease(uObject, out AsyncOperationHandle handle))
			{
				ReleaseIfValid(handle);
				return;
			}

			if (uObject != null)
			{
				Addressables.Release(uObject);
			}
		}

		public void DisposeAssetsGroup<T>(AssetsGroup<T> group)
		{
			if (group == null) return;
			if (group == AssetsGroup<T>.Default) return;

			if (_groupOperationsLookup.TryGetValue(group.Guid, out var operation))
			{
				group.DisposeAssets();
				_groupOperationsLookup.Remove(group.Guid);
				ReleaseIfValid(operation);
				return;
			}

			Debug.LogError("--> Trying To Dispose An Assets Group Which Is Not Getting Track!");
		}

		/// <summary>
		/// Gives back the claim on a spawned instance; releasing its handle destroys it. A
		/// destroyed instance still gives its claim back. An instance this strategy didn't spawn
		/// goes to <c>Addressables.ReleaseInstance</c>, and is destroyed if Addressables doesn't
		/// know it either. False only for null or an untracked, already destroyed object.
		/// </summary>
		public bool DisposeInstance(GameObject gObject)
		{
			if (ReferenceEquals(gObject, null)) return false;

			if (_claims.TryRelease(gObject, out AsyncOperationHandle handle))
			{
				ReleaseIfValid(handle);
				return true;
			}

			if (gObject == null) return false;

			if (Addressables.ReleaseInstance(gObject))
				return true;

			gObject.DestroyInAnyMode();
			return true;
		}

		/// <summary>Releases every claim and group this strategy holds; spawned instances are destroyed.</summary>
		public void Reset()
		{
			_releaseBuffer.Clear();
			_claims.DrainAll(_releaseBuffer);
			ReleaseBuffered();

			foreach (var kvp in _groupOperationsLookup)
				ReleaseIfValid(kvp.Value);
			_groupOperationsLookup.Clear();
		}

		/// <summary>
		/// Releases the claims of objects destroyed without a dispose: instances that died with
		/// their scene, or assets destroyed by hand. Runs on every scene unload; returns how
		/// many claims it released.
		/// </summary>
		public int ReleaseDestroyed()
		{
			if (_claims.Count == 0) return 0;

			_keyBuffer.Clear();
			_claims.CopyKeysTo(_keyBuffer);
			_releaseBuffer.Clear();

			foreach (Object key in _keyBuffer)
			{
				if (key == null) // Unity null: the native object is gone.
					_claims.Drain(key, _releaseBuffer);
			}

			_keyBuffer.Clear();
			return ReleaseBuffered();
		}

		// Addressables also releases tracked instances whose scene unloaded. Either side may go
		// first: whichever releases an instance's operation destroys it, which invalidates the
		// other side's handle and removes it from Addressables' tracking, so it is released once.
		private void OnSceneUnloaded(Scene scene) => ReleaseDestroyed();

		private int ReleaseBuffered()
		{
			int count = _releaseBuffer.Count;
			for (int i = 0; i < count; i++)
				ReleaseIfValid(_releaseBuffer[i]);

			_releaseBuffer.Clear();
			return count;
		}

		/// <summary>
		/// Awaits a load/spawn op, tracks the result on success, and guarantees the handle is
		/// released on failure or cancellation so failed loads never pin bundles in memory.
		/// </summary>
		private async UniTask<TObject> AwaitAndTrack<TObject>(AsyncOperationHandle<TObject> asyncOp, IProgress<float> progress,
		                                                      CancellationToken cToken)
		{
			TObject result;
			try
			{
				result = await asyncOp.ToUniTask(progress: progress, cancellationToken: cToken, autoReleaseWhenCanceled: true);
			}
			catch (OperationCanceledException)
			{
				throw; // autoReleaseWhenCanceled already released the handle.
			}
			catch
			{
				ReleaseIfValid(asyncOp); // Failed ops throw before we could track them.
				throw;
			}

			// All Addressables assets are UnityEngine.Objects; the TObject parameter itself is
			// unconstrained (matches the IResourceLoadingStrategy interface).
			if (result is Object tracked)
			{
				Claim(tracked, asyncOp);
			}
			else
			{
				ReleaseIfValid(asyncOp); // Succeeded but produced nothing - don't leak the handle.
			}

			return result;
		}

		// A synchronous load can wait for any op, except on WebGL, where only an op that is
		// already done (a cached asset) has its result.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool CanWaitFor(AsyncOperationHandle op) => _canBlock || op.IsDone;

		/// <summary>
		/// The blocking <see cref="AwaitAndTrack{TObject}"/>. False when the op isn't done and
		/// this platform can't wait for it (WebGL): the handle is released, and the op still
		/// runs to its end, where Addressables destroys it. Internal for tests, which hand it
		/// operations they finish themselves.
		/// </summary>
		internal bool TryWaitAndTrack<TObject>(AsyncOperationHandle<TObject> op, out TObject result)
		{
			result = default;
			if (!CanWaitFor(op))
			{
				ReleaseIfValid(op);
				return false;
			}

			try
			{
				result = op.WaitForCompletion();
			}
			catch
			{
				ReleaseIfValid(op);
				throw;
			}

			if (op.Status == AsyncOperationStatus.Succeeded && result is Object tracked)
			{
				Claim(tracked, op);
			}
			else
			{
				ReleaseIfValid(op);
			}

			return true;
		}

		internal TObject WaitAndTrack<TObject>(AsyncOperationHandle<TObject> op, object key)
		{
			return TryWaitAndTrack(op, out TObject result) ? result : throw CannotWait(key);
		}

		// List loads (every frame of a sprite sheet) produce no Unity Object result to
		// key on, so the claim is keyed on the first element; DisposeAsset on that
		// element releases the whole list's handle.
		internal bool TryWaitAndTrackList<TObject>(AsyncOperationHandle<IList<TObject>> op, out IList<TObject> result)
		{
			result = null;
			if (!CanWaitFor(op))
			{
				ReleaseIfValid(op);
				return false;
			}

			try
			{
				result = op.WaitForCompletion();
			}
			catch
			{
				ReleaseIfValid(op);
				throw;
			}

			if (op.Status == AsyncOperationStatus.Succeeded && result is { Count: > 0 } && result[0] is Object first)
				Claim(first, op);
			else
				ReleaseIfValid(op);

			return true;
		}

		private static NotSupportedException CannotWait(object key)
		{
			return new NotSupportedException(
				$"UniResources: '{key}' isn't loaded, and a synchronous load can't wait for it on WebGL. Load it with the async API.");
		}

		private async UniTask<IList<TObject>> AwaitAndTrackList<TObject>(AsyncOperationHandle<IList<TObject>> op, CancellationToken cToken)
		{
			IList<TObject> result;
			try
			{
				result = await op.ToUniTask(cancellationToken: cToken, autoReleaseWhenCanceled: true);
			}
			catch (OperationCanceledException)
			{
				throw; // autoReleaseWhenCanceled already released the handle.
			}
			catch
			{
				ReleaseIfValid(op);
				throw;
			}

			if (result is { Count: > 0 } && result[0] is Object first)
				Claim(first, op);
			else
				ReleaseIfValid(op);

			return result;
		}

		/// <summary>
		/// Records one claim on <paramref name="obj"/>, backed by the handle that produced it. A
		/// repeat load of a cached key yields the same object but a new handle (Addressables
		/// raises the operation's reference count each time), so every handle is kept and each
		/// dispose releases exactly one of them.
		/// </summary>
		private void Claim(Object obj, AsyncOperationHandle handle)
		{
			if (obj == null)
			{
				ReleaseIfValid(handle);
				return;
			}

			_claims.Acquire(obj, handle);

			if (!_watchingSceneUnloads)
			{
				_watchingSceneUnloads = true;
				SceneManager.sceneUnloaded += OnSceneUnloaded;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void ReleaseIfValid(AsyncOperationHandle handle)
		{
			if (handle.IsValid())
				Addressables.Release(handle);
		}

		private static async UniTask<List<IResourceLocation>> ResolveRemoteLocationsAsync(string[] labels, CancellationToken cToken)
		{
			var locations = new List<IResourceLocation>();

			if (labels != null && labels.Length > 0)
			{
				foreach (var label in labels)
				{
					var handle = Addressables.LoadResourceLocationsAsync(label, typeof(Object));
					try
					{
						var locs = await handle.ToUniTask(cancellationToken: cToken);
						if (locs != null)
							locations.AddRange(locs);
					}
					finally
					{
						ReleaseIfValid(handle);
					}
				}
			}

			return locations;
		}

		// Whether the catalog has the key for the type. On WebGL the probe can't wait for
		// Addressables to initialize, so it answers false until then: nothing is loaded yet anyway.
		private bool HasLocations(object key, Type type)
		{
			if (key == null)
				return false;
			if (key is string s && string.IsNullOrEmpty(s))
				return false;

			var handle = Addressables.LoadResourceLocationsAsync(key, type);
			try
			{
				if (!CanWaitFor(handle))
					return false;

				var locations = handle.WaitForCompletion();
				return locations != null && locations.Count > 0;
			}
			catch
			{
				return false;
			}
			finally
			{
				ReleaseIfValid(handle);
			}
		}

		// The probe for the async loads. It waits for Addressables to initialize, and after that
		// it completes without yielding, as the location lookup is synchronous.
		private static async UniTask<bool> HasLocationsAsync(object key, Type type, CancellationToken cToken)
		{
			if (key == null)
				return false;
			if (key is string s && string.IsNullOrEmpty(s))
				return false;

			var handle = Addressables.LoadResourceLocationsAsync(key, type);
			try
			{
				var locations = await handle.ToUniTask(cancellationToken: cToken);
				return locations != null && locations.Count > 0;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch
			{
				return false;
			}
			finally
			{
				ReleaseIfValid(handle);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void CheckResourceKey(string key)
		{
			if (string.IsNullOrEmpty(key) == false) return;
			throw new ArgumentException("UniResources: Key Cannot Be Empty Or Void!");
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void ValidateReference(AssetReference reference)
		{
			if (reference != null && reference.RuntimeKeyIsValid()) return;
			throw new ArgumentException("UniResources: AssetReference is null or has an invalid Runtime Key.");
		}
	}
}
#endif
