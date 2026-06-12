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
		private readonly Dictionary<Guid, AsyncOperationHandle>   _groupOperationsLookup = new();
		private readonly Dictionary<Object, AsyncOperationHandle> _objectHandleLookup    = new();

		public UniTask InitAsync(CancellationToken cToken = default)
		{
			return Addressables.InitializeAsync().ToUniTask(cancellationToken: cToken);
		}

		public async UniTask<bool> HasResourceAsync(string key, Type type = null, CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			var handle = Addressables.LoadResourceLocationsAsync(key, type);
			var locations = await handle.WithCancellation(cToken);
			bool result = locations.Count > 0;
			Addressables.Release(handle);
			return result;
		}

		public async UniTask<IList<IResourceLocation>> GetResourceLocationsAsync(IEnumerable<string> keys, Type type, MergeMode mode,
		                                                                         CancellationToken cToken = default)
		{
			var handle = Addressables.LoadResourceLocationsAsync(keys, mode.Convert(), type);
			var result = await handle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(handle);
			return result;
		}

		public async UniTask<IList<IResourceLocation>> GetAllResourceLocationsAsync(Type type = null, CancellationToken cToken = default)
		{
			var handle = Addressables.LoadResourceLocationsAsync("*", type);
			var result = await handle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(handle);
			return result;
		}

		// --------------------------------------------------------------------------
		// CATALOG UPDATES (The Clever Refactor)
		// --------------------------------------------------------------------------

		public async UniTask<List<string>> CheckForCatalogUpdatesAsync(CancellationToken cToken = default)
		{
			var handle = Addressables.CheckForCatalogUpdates(false);
			var result = await handle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(handle);
			return result ?? new List<string>();
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
				if (handle.IsValid())
					Addressables.Release(handle);
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
			// THE OPTIMIZATION: If no labels are provided, grab all known keys.
			// Addressables natively strips duplicates and local files, returning the exact remote size instantly.
			if (labels == null || labels.Length == 0)
			{
				var allKeys = Addressables.ResourceLocators.SelectMany(x => x.Keys);
				var handle = Addressables.GetDownloadSizeAsync(allKeys);
				var result = await handle.ToUniTask(cancellationToken: cToken);
				Addressables.Release(handle);
				return result;
			}

			// Existing label-based logic...
			var locations = await ResolveRemoteLocationsAsync(labels, cToken);
			if (locations.Count == 0) return 0;

			var locHandle = Addressables.GetDownloadSizeAsync(locations);
			var locResult = await locHandle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(locHandle);
			return locResult;
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

				// THE FIX: Call the native Addressables API directly (which accepts objects)
				// instead of routing through the string-only wrapper method.
				var sizeHandle = Addressables.GetDownloadSizeAsync(allKeys);
				downloadSize = await sizeHandle.ToUniTask(cancellationToken: cToken);
				Addressables.Release(sizeHandle);

				if (downloadSize == 0)
				{
					progress?.Report(1f);
					return 0;
				}

				// Pass all keys natively using Union merge mode
				downloadOp = Addressables.DownloadDependenciesAsync(allKeys, Addressables.MergeMode.Union, false);
			}
			else
			{
				// Existing label-based logic...
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
				if (downloadOp.IsValid())
					Addressables.Release(downloadOp);
			}

			return downloadSize;
		}

		public async UniTask<long> GetRemoteDependenciesSizeAsync(IEnumerable<string> keys, CancellationToken cToken = default)
		{
			var handle = Addressables.GetDownloadSizeAsync(keys);
			var result = await handle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(handle);
			return result;
		}

		public async UniTask<long> GetRemoteDependenciesSizeAsync(IList<IResourceLocation> locations, CancellationToken cToken = default)
		{
			var handle = Addressables.GetDownloadSizeAsync(locations);
			var result = await handle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(handle);
			return result;
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
			var result = await asyncOp.ToUniTask(progress: progress, cancellationToken: cToken);

			if (asyncOp.Status == AsyncOperationStatus.Succeeded && result != null)
				TrackObjectHandle(result as Object, asyncOp);

			return result;
		}

		public async UniTask<TObject> LoadAssetAsync<TObject>(AssetReference reference, IProgress<float> progress = default,
		                                                      CancellationToken cToken = default)
		{
			ValidateReference(reference);
			var asyncOp = Addressables.LoadAssetAsync<TObject>(reference);
			var result = await asyncOp.ToUniTask(progress: progress, cancellationToken: cToken);

			if (asyncOp.Status == AsyncOperationStatus.Succeeded && result != null)
				TrackObjectHandle(result as Object, asyncOp);

			return result;
		}

		public async UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IEnumerable<string> keys, MergeMode mode = MergeMode.UseFirst,
		                                                                    IProgress<float> progress = default, CancellationToken cToken = default)
		{
			var asyncOp = Addressables.LoadAssetsAsync<TObject>(keys, default, mode.Convert());
			var task = asyncOp.ToUniTask(progress: progress, cancellationToken: cToken);
			var assetsGroup = new AssetsGroup<TObject>(await task);

			_groupOperationsLookup[assetsGroup.Guid] = asyncOp;
			return assetsGroup;
		}

		public async UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IList<IResourceLocation> keys, IProgress<float> progress = default,
		                                                                    CancellationToken cToken = default)
		{
			var asyncOp = Addressables.LoadAssetsAsync<TObject>(keys, default);
			var task = asyncOp.ToUniTask(progress: progress, cancellationToken: cToken);
			var assetsGroup = new AssetsGroup<TObject>(await task);

			_groupOperationsLookup[assetsGroup.Guid] = asyncOp;
			return assetsGroup;
		}

		public async UniTask<GameObject> SpawnAsync(string key, Transform root, IProgress<float> progress = default,
		                                            CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			var asyncOp = Addressables.InstantiateAsync(key, root);
			var result = await asyncOp.ToUniTask(progress: progress, cancellationToken: cToken);

			if (asyncOp.Status == AsyncOperationStatus.Succeeded && result != null)
				TrackObjectHandle(result, asyncOp);

			return result;
		}

		public async UniTask<GameObject> SpawnAsync(AssetReference reference, Transform root, IProgress<float> progress = default,
		                                            CancellationToken cToken = default)
		{
			ValidateReference(reference);
			var asyncOp = Addressables.InstantiateAsync(reference, root);
			var result = await asyncOp.ToUniTask(progress: progress, cancellationToken: cToken);

			if (asyncOp.Status == AsyncOperationStatus.Succeeded && result != null)
				TrackObjectHandle(result, asyncOp);

			return result;
		}

		// --------------------------------------------------------------------------
		// SYNCHRONOUS IMPLEMENTATION
		// --------------------------------------------------------------------------

		public TObject LoadAsset<TObject>(string key)
		{
			CheckResourceKey(key);
			var op = Addressables.LoadAssetAsync<TObject>(key);
			var result = op.WaitForCompletion();

			if (op.Status == AsyncOperationStatus.Succeeded && result != null)
				TrackObjectHandle(result as Object, op);

			return result;
		}

		public TObject LoadAsset<TObject>(AssetReference reference)
		{
			ValidateReference(reference);
			var op = Addressables.LoadAssetAsync<TObject>(reference);
			var result = op.WaitForCompletion();

			if (op.Status == AsyncOperationStatus.Succeeded && result != null)
				TrackObjectHandle(result as Object, op);

			return result;
		}

		public GameObject Spawn(string key, Transform root)
		{
			CheckResourceKey(key);
			var op = Addressables.InstantiateAsync(key, root);
			var result = op.WaitForCompletion();

			if (op.Status == AsyncOperationStatus.Succeeded && result != null)
				TrackObjectHandle(result, op);

			return result;
		}

		public GameObject Spawn(AssetReference reference, Transform root)
		{
			ValidateReference(reference);
			var op = Addressables.InstantiateAsync(reference, root);
			var result = op.WaitForCompletion();

			if (op.Status == AsyncOperationStatus.Succeeded && result != null)
				TrackObjectHandle(result, op);

			return result;
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

			if (_objectHandleLookup.TryGetValue(uObject, out var handle))
			{
				_objectHandleLookup.Remove(uObject);
				Addressables.Release(handle);
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
				Addressables.Release(operation);
				return;
			}

			Debug.LogError("--> Trying To Dispose An Assets Group Which Is Not Getting Track!");
		}

		public bool DisposeInstance(GameObject gObject)
		{
			if (gObject == null) return false;

			_objectHandleLookup.Remove(gObject);

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
					Addressables.Release(kvp.Value);
			}

			_objectHandleLookup.Clear();

			foreach (var kvp in _groupOperationsLookup)
				Addressables.Release(kvp.Value);
			_groupOperationsLookup.Clear();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TrackObjectHandle(Object obj, AsyncOperationHandle handle)
		{
			if (obj == null) return;

			if (_objectHandleLookup.TryGetValue(obj, out var existingHandle))
			{
				Addressables.Release(existingHandle);
				_objectHandleLookup[obj] = handle;
				return;
			}

			_objectHandleLookup[obj] = handle;
		}

		private static async UniTask<List<IResourceLocation>> ResolveRemoteLocationsAsync(string[] labels, CancellationToken cToken)
		{
			var locations = new List<IResourceLocation>();

			if (labels != null && labels.Length > 0)
			{
				foreach (var label in labels)
				{
					var handle = Addressables.LoadResourceLocationsAsync(label, typeof(Object));
					var locs = await handle.ToUniTask(cancellationToken: cToken);
					Addressables.Release(handle);
					if (locs != null)
						locations.AddRange(locs);
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