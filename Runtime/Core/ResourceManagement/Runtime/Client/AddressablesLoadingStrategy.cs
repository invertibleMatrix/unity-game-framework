using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using AK.Core.Extensions;
using Object = UnityEngine.Object;

namespace AK.Core.ResourceManagement
{
	public sealed class AddressablesLoadingStrategy : IResourceLoadingStrategy
	{
		/// <summary>
		/// A tracked load claim. Addressables caches completed ops per key, so loading the
		/// same key twice yields the same object AND the same underlying handle - what differs
		/// is how many callers hold a claim. We refcount claims and release the handle only
		/// when the last claim is disposed, so one caller can never unload an asset from
		/// under another caller.
		/// </summary>
		private sealed class TrackedAsset
		{
			public AsyncOperationHandle Handle;
			public int RefCount;
		}

		private readonly Dictionary<Guid, AsyncOperationHandle> _groupOperationsLookup = new();
		private readonly Dictionary<Object, TrackedAsset>       _objectHandleLookup    = new();

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
				await handle.ToUniTask(cancellationToken: cToken);
				if (handle.IsValid() && handle.Status == AsyncOperationStatus.Failed)
					Debug.LogError($"[AddressablesStrategy] Catalog update FAILED: {handle.OperationException}");
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

				progress?.Report(1f);

				if (downloadOp.Status == AsyncOperationStatus.Failed)
					Debug.LogError($"[AddressablesStrategy] Download FAILED: {downloadOp.OperationException}");
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
			return WaitAndTrack(op);
		}

		public TObject LoadAsset<TObject>(AssetReference reference)
		{
			ValidateReference(reference);
			var op = Addressables.LoadAssetAsync<TObject>(reference);
			return WaitAndTrack(op);
		}

		public GameObject Spawn(string key, Transform root)
		{
			CheckResourceKey(key);
			var op = Addressables.InstantiateAsync(key, root);
			return WaitAndTrack(op);
		}

		public GameObject Spawn(AssetReference reference, Transform root)
		{
			ValidateReference(reference);
			var op = Addressables.InstantiateAsync(reference, root);
			return WaitAndTrack(op);
		}

		// --------------------------------------------------------------------------
		// SCENE LOADING
		// --------------------------------------------------------------------------

		public async UniTask<SceneInstance> LoadSceneAsync(string key, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true,
		                                                   IProgress<float> progress = default, CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			var asyncOp = Addressables.LoadSceneAsync(key, mode, activateOnLoad);
			var result = await asyncOp.ToUniTask(progress: progress, cancellationToken: cToken);
			return result;
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

		public void DisposeAsset(Object uObject)
		{
			if (uObject == null) return;

			if (_objectHandleLookup.TryGetValue(uObject, out var tracked))
			{
				tracked.RefCount--;

				if (tracked.RefCount <= 0)
				{
					_objectHandleLookup.Remove(uObject);
					ReleaseIfValid(tracked.Handle);
				}

				return;
			}

			Addressables.Release(uObject);
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

		public bool DisposeInstance(GameObject gObject)
		{
			if (gObject == null) return false;

			if (_objectHandleLookup.TryGetValue(gObject, out var tracked))
			{
				tracked.RefCount--;

				if (tracked.RefCount > 0)
					return true; // Other callers still hold claims on this instance.

				_objectHandleLookup.Remove(gObject);
			}

			if (Addressables.ReleaseInstance(gObject))
				return true;

			Object.Destroy(gObject);
			return true;
		}

		public void Reset()
		{
			foreach (var kvp in _objectHandleLookup)
			{
				if (kvp.Key is GameObject go)
					Addressables.ReleaseInstance(go);
				else
					ReleaseIfValid(kvp.Value.Handle);
			}

			_objectHandleLookup.Clear();

			foreach (var kvp in _groupOperationsLookup)
				ReleaseIfValid(kvp.Value);
			_groupOperationsLookup.Clear();
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
				TrackObjectHandle(tracked, asyncOp);
			}
			else
			{
				ReleaseIfValid(asyncOp); // Succeeded but produced nothing - don't leak the handle.
			}

			return result;
		}

		private TObject WaitAndTrack<TObject>(AsyncOperationHandle<TObject> op)
		{
			TObject result;
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
				TrackObjectHandle(tracked, op);
			}
			else
			{
				ReleaseIfValid(op);
			}

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TrackObjectHandle(Object obj, AsyncOperationHandle handle)
		{
			if (obj == null) return;

			if (_objectHandleLookup.TryGetValue(obj, out var existing))
			{
				// Same key loaded again: same object, same underlying op - just add a claim.
				// Releasing or overwriting here would let one caller unload the asset from
				// under another.
				existing.RefCount++;
				return;
			}

			_objectHandleLookup[obj] = new TrackedAsset { Handle = handle, RefCount = 1 };
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