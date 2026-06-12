using System;
using System.Collections.Generic;
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
	/// <inheritdoc />
	public sealed class AddressablesLoadingStrategy : IResourceLoadingStrategy
	{
		// Tracks handles for AssetsGroup disposal (keyed by group Guid).
		private readonly Dictionary<Guid, AsyncOperationHandle> _groupOperationsLookup = new();

		// Tracks handles for individual asset loads and spawns (keyed by the loaded object reference).
		// This is essential for proper ref-count management — every Load/Spawn must be paired with
		// a DisposeAsset/DisposeInstance to release the handle and decrement the Addressables ref-count.
		private readonly Dictionary<Object, AsyncOperationHandle> _objectHandleLookup = new();

		/// <inheritdoc />
		public UniTask InitAsync(CancellationToken cToken = default)
		{
			return Addressables.InitializeAsync().ToUniTask(cancellationToken: cToken);
		}

		/// <inheritdoc />
		public async UniTask<bool> HasResourceAsync(string key, Type type = null, CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			// LoadResourceLocationsAsync creates a handle that must be released to avoid leaking ref-counts.
			var handle = Addressables.LoadResourceLocationsAsync(key, type);
			var locations = await handle.WithCancellation(cToken);
			bool result = locations.Count > 0;
			Addressables.Release(handle);
			return result;
		}

		/// <inheritdoc />
		public async UniTask<IList<IResourceLocation>> GetResourceLocationsAsync(IEnumerable<string> keys, Type type,
		                                                                   MergeMode mode,
		                                                                   CancellationToken cToken = default)
		{
			var handle = Addressables.LoadResourceLocationsAsync(keys, mode.Convert(), type);
			var result = await handle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(handle);
			return result;
		}

		/// <inheritdoc />
		public async UniTask<IList<IResourceLocation>> GetAllResourceLocationsAsync(Type type = null,
		                                                                     CancellationToken cToken = default)
		{
			var handle = Addressables.LoadResourceLocationsAsync("*", type);
			var result = await handle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(handle);
			return result;
		}

		/// <inheritdoc />
		public async UniTask<List<string>> HasCatalogUpdatesAsync(CancellationToken cToken = default)
		{
			var handle = Addressables.CheckForCatalogUpdates(false);
			var catalogUpdates = await handle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(handle);
			return catalogUpdates ?? new List<string>();
		}

		/// <inheritdoc />
		public async UniTask ApplyCatalogUpdatesAsync(List<string> catalogIds, IProgress<float> progress = null, CancellationToken cToken = default)
		{
			if (catalogIds == null || catalogIds.Count == 0)
			{
				progress?.Report(1f);
				return;
			}

			progress?.Report(0f);
			var updateOp = Addressables.UpdateCatalogs(catalogIds);
			
			try
			{
				// Poll for progress while updating catalogs
				while (!updateOp.IsDone)
				{
					if (updateOp.IsValid())
					{
						var status = updateOp.GetDownloadStatus();
						progress?.Report(status.Percent);
					}
					await UniTask.Yield(cToken);
				}
				
				progress?.Report(1f);

				if (updateOp.Status == AsyncOperationStatus.Failed)
					Debug.LogError($"[AddressablesStrategy] Catalog update FAILED: {updateOp.OperationException}");
			}
			finally
			{
				if (updateOp.IsValid())
					Addressables.Release(updateOp);
			}
		}

		/// <inheritdoc />
		public async UniTask<bool> CheckForCatalogUpdatesAsync(IProgress<float> progress = null, CancellationToken cToken = default)
		{
			var catalogIds = await HasCatalogUpdatesAsync(cToken);
			if (catalogIds.Count == 0)
			{
				progress?.Report(1f);
				return false;
			}

			await ApplyCatalogUpdatesAsync(catalogIds, progress, cToken);
			return true;
		}

		/// <inheritdoc />
		public async UniTask<long> GetRemoteContentSizeAsync(string[] labels = null, CancellationToken cToken = default)
		{
			var keys = ResolveKeys(labels);
			return await GetDownloadSizeAsync(keys, cToken);
		}

		/// <inheritdoc />
		public async UniTask<long> DownloadRemoteContentAsync(string[] labels = null, IProgress<float> progress = null, CancellationToken cToken = default)
		{
			var keys = ResolveKeys(labels);
			var downloadSize = await GetDownloadSizeAsync(keys, cToken);

			if (downloadSize > 0)
			{
				// Use location-based download to avoid InvalidKeyException from key-based APIs.
				// First resolve all resource locations, then download by location.
				var locations = new List<IResourceLocation>();
				foreach (var key in keys)
				{
					try
					{
						var locsHandle = Addressables.LoadResourceLocationsAsync(key);
						var locs = await locsHandle.ToUniTask(cancellationToken: cToken);
						Addressables.Release(locsHandle);

						if (locs != null)
						{
							foreach (var loc in locs)
							{
								if (loc != null && !locations.Contains(loc))
									locations.Add(loc);
							}
						}
					}
					catch (OperationCanceledException) { throw; }
					catch (Exception)
					{
						// Skip keys that fail to resolve
					}
				}

				if (locations.Count > 0)
				{
					// Download using location-based API — never throws InvalidKeyException.
					// autoRelease=false: we control the release in the finally block.
					var downloadOp = Addressables.DownloadDependenciesAsync(locations, false);
					
					try
					{
						// Poll for progress and report via IProgress<float>
						while (!downloadOp.IsDone)
						{
							if (downloadOp.IsValid())
							{
								var status = downloadOp.GetDownloadStatus();
								progress?.Report(status.Percent);
							}
							await UniTask.Yield(cToken);
						}
						
						// Report 100% on completion
						progress?.Report(1f);
						
						if (downloadOp.Status == AsyncOperationStatus.Failed)
							Debug.LogError($"[AddressablesStrategy] Download FAILED: {downloadOp.OperationException}");
					}
					finally
					{
						if (downloadOp.IsValid())
							Addressables.Release(downloadOp);
					}
				}
				else
				{
					progress?.Report(1f);
				}
			}
			else
			{
				// Nothing to download — report complete immediately
				progress?.Report(1f);
			}

			return downloadSize;
		}

		/// <inheritdoc />
		public async UniTask<long> GetRemoteDependenciesSizeAsync(IEnumerable<string> keys,
		                                                    CancellationToken cToken = default)
		{
			var handle = Addressables.GetDownloadSizeAsync(keys);
			var result = await handle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(handle);
			return result;
		}

		/// <inheritdoc />
		public async UniTask<long> GetRemoteDependenciesSizeAsync(IList<IResourceLocation> locations,
		                                                    CancellationToken cToken = default)
		{
			var handle = Addressables.GetDownloadSizeAsync(locations);
			var result = await handle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(handle);
			return result;
		}

		/// <inheritdoc />
		public UniTask GetRemoteDependenciesAsync(IList<IResourceLocation> locations,
		                                          out IOperationStatusProvider provider,
		                                          CancellationToken cToken = default)
		{
			// autoReleaseHandle=true: handle auto-released on completion.
			// OperationStatusProvider.ToOperationStatus() gracefully handles invalid/released handles
			// by checking IsValid() first and returning OperationStatus(IsRunning=false) if invalid.
			var asyncOp = Addressables.DownloadDependenciesAsync(locations, true);
			provider = new OperationStatusProvider(asyncOp);
			return asyncOp.ToUniTask(cancellationToken: cToken);
		}

		/// <inheritdoc />
		public UniTask GetRemoteDependenciesAsync(IEnumerable<string> keys, out IOperationStatusProvider provider,
		                                          MergeMode mode = MergeMode.UseFirst,
		                                          CancellationToken cToken = default)
		{
			// autoReleaseHandle=true: handle auto-released on completion.
			// OperationStatusProvider.ToOperationStatus() gracefully handles invalid/released handles.
			var asyncOp = Addressables.DownloadDependenciesAsync(keys, mode.Convert(), true);
			provider = new OperationStatusProvider(asyncOp);
			return asyncOp.ToUniTask(cancellationToken: cToken);
		}

		// --------------------------------------------------------------------------
		// ASYNC IMPLEMENTATION
		// --------------------------------------------------------------------------

		/// <inheritdoc />
		public async UniTask<TObject> LoadAssetAsync<TObject>(string key, IProgress<float> progress = default,
		                                                CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			var asyncOp = Addressables.LoadAssetAsync<TObject>(key);
			var result = await asyncOp.ToUniTask(progress: progress, cancellationToken: cToken);

			if (asyncOp.Status == AsyncOperationStatus.Succeeded && result != null)
				TrackObjectHandle(result as Object, asyncOp);

			return result;
		}

		/// <inheritdoc />
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

		/// <inheritdoc />
		public async UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IEnumerable<string> keys,
		                                                                    MergeMode mode = MergeMode.UseFirst,
		                                                                    IProgress<float> progress = default,
		                                                                    CancellationToken cToken = default)
		{
			var asyncOp = Addressables.LoadAssetsAsync<TObject>(keys, default, mode.Convert());
			var task = asyncOp.ToUniTask(progress: progress, cancellationToken: cToken);
			var assetsGroup = new AssetsGroup<TObject>(await task);

			_groupOperationsLookup[assetsGroup.Guid] = asyncOp;
			return assetsGroup;
		}

		/// <inheritdoc />
		public async UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IList<IResourceLocation> keys,
		                                                                    IProgress<float> progress = default,
		                                                                    CancellationToken cToken = default)
		{
			var asyncOp = Addressables.LoadAssetsAsync<TObject>(keys, default);
			var task = asyncOp.ToUniTask(progress: progress, cancellationToken: cToken);
			var assetsGroup = new AssetsGroup<TObject>(await task);

			_groupOperationsLookup[assetsGroup.Guid] = asyncOp;
			return assetsGroup;
		}

		/// <inheritdoc />
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

		/// <inheritdoc />
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
		// SYNCHRONOUS IMPLEMENTATION (WaitForCompletion)
		// --------------------------------------------------------------------------

		/// <inheritdoc />
		public TObject LoadAsset<TObject>(string key)
		{
			CheckResourceKey(key);
			var op = Addressables.LoadAssetAsync<TObject>(key);
			var result = op.WaitForCompletion();

			if (op.Status == AsyncOperationStatus.Succeeded && result != null)
				TrackObjectHandle(result as Object, op);

			return result;
		}

		/// <inheritdoc />
		public TObject LoadAsset<TObject>(AssetReference reference)
		{
			ValidateReference(reference);
			var op = Addressables.LoadAssetAsync<TObject>(reference);
			var result = op.WaitForCompletion();

			if (op.Status == AsyncOperationStatus.Succeeded && result != null)
				TrackObjectHandle(result as Object, op);

			return result;
		}

		/// <inheritdoc />
		public GameObject Spawn(string key, Transform root)
		{
			CheckResourceKey(key);
			var op = Addressables.InstantiateAsync(key, root);
			var result = op.WaitForCompletion();

			if (op.Status == AsyncOperationStatus.Succeeded && result != null)
				TrackObjectHandle(result, op);

			return result;
		}

		/// <inheritdoc />
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

		/// <inheritdoc />
		public async UniTask<SceneInstance> LoadSceneAsync(string key, LoadSceneMode mode = LoadSceneMode.Single,
		                                                   bool activateOnLoad = true, IProgress<float> progress = default,
		                                                   CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			var asyncOp = Addressables.LoadSceneAsync(key, mode, activateOnLoad);
			var result = await asyncOp.ToUniTask(progress: progress, cancellationToken: cToken);

			// SceneInstance handles are released via UnloadSceneAsync, not DisposeAsset.
			// We don't track them in _objectHandleLookup since they have a dedicated unload path.
			return result;
		}

		/// <inheritdoc />
		public UniTask UnloadSceneAsync(SceneInstance scene, IProgress<float> progress = default,
		                                CancellationToken cToken = default)
		{
			if (scene.Scene.IsValid() == false)
				return UniTask.CompletedTask;

			return Addressables.UnloadSceneAsync(scene).ToUniTask(progress: progress, cancellationToken: cToken);
		}

		// --------------------------------------------------------------------------
		// CATALOG UPDATES (Low-level split API)
		// --------------------------------------------------------------------------

		/// <inheritdoc />
		public async UniTask<List<string>> CheckForCatalogUpdatesAsync(CancellationToken cToken = default)
		{
			var handle = Addressables.CheckForCatalogUpdates(false);
			var result = await handle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(handle);
			return result;
		}

		/// <inheritdoc />
		public async UniTask UpdateCatalogsAsync(IEnumerable<string> catalogs = null,
		                                        bool autoCleanBundleCache = false,
		                                        CancellationToken cToken = default)
		{
			var handle = autoCleanBundleCache
				? Addressables.UpdateCatalogs(true, catalogs, false)
				: Addressables.UpdateCatalogs(catalogs, false);

			await handle.ToUniTask(cancellationToken: cToken);
			Addressables.Release(handle);
		}

		// --------------------------------------------------------------------------
		// CLEANUP & HELPERS
		// --------------------------------------------------------------------------

		/// <inheritdoc />
		public void DisposeAsset(Object uObject)
		{
			if (uObject == null) return;

			// If we tracked this object's handle, release through the handle for proper ref-count management.
			if (_objectHandleLookup.TryGetValue(uObject, out var handle))
			{
				_objectHandleLookup.Remove(uObject);
				Addressables.Release(handle);
				return;
			}

			// Fallback: release by object reference (works for simple loads not tracked in the lookup).
			Addressables.Release(uObject);
		}

		/// <inheritdoc />
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

		/// <inheritdoc />
		public bool DisposeInstance(GameObject gObject)
		{
			if (gObject == null) return false;

			// Remove from handle tracking first.
			_objectHandleLookup.Remove(gObject);

			// Addressables.ReleaseInstance returns true if it successfully released the instance.
			// If it returns false, it means this object is not one that Addressables is tracking
			// (e.g., a nested prefab that was un-parented, or a non-addressable object).
			if (Addressables.ReleaseInstance(gObject))
			{
				return true;
			}

			// If Addressables didn't release it, we destroy it manually as a fallback.
			Object.Destroy(gObject);
			return true;
		}

		/// <inheritdoc />
		public void Reset()
		{
			// Release tracked individual asset handles.
			// For spawned instances (GameObject keys), use ReleaseInstance instead of Release
			// to avoid double-release: InstantiateAsync with trackHandle=true auto-releases on destroy,
			// so using ReleaseInstance correctly decrements Addressables' internal ref-count.
			foreach (var kvp in _objectHandleLookup)
			{
				if (kvp.Key is GameObject go)
					Addressables.ReleaseInstance(go);
				else
					Addressables.Release(kvp.Value);
			}
			_objectHandleLookup.Clear();

			// Release all tracked group operations.
			foreach (var kvp in _groupOperationsLookup)
				Addressables.Release(kvp.Value);
			_groupOperationsLookup.Clear();
		}

		/// <summary>
		/// Tracks individual asset/spawn handles so DisposeAsset/DisposeInstance can
		/// release the handle and decrement the Addressables ref-count.
		/// If the same object was loaded before, releases the old handle first to prevent leaks.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TrackObjectHandle(Object obj, AsyncOperationHandle handle)
		{
			if (obj == null) return;

			// If the same object was loaded before (e.g., same asset loaded twice),
			// release the previous handle before tracking the new one to avoid leaking the old reference.
			if (_objectHandleLookup.TryGetValue(obj, out var existingHandle))
			{
				Addressables.Release(existingHandle);
				_objectHandleLookup[obj] = handle;
				return;
			}

			_objectHandleLookup[obj] = handle;
		}

		/// <summary>
		/// Resolves the keys to use for download operations.
		/// If labels are provided, uses them directly. Otherwise, enumerates all catalog keys.
		/// Always returns a materialized <see cref="List{T}"/> to avoid multiple enumeration.
		/// </summary>
		private static List<string> ResolveKeys(string[] labels)
		{
			if (labels != null && labels.Length > 0)
				return new List<string>(labels);

			var allKeys = new List<string>();
			foreach (var locator in Addressables.ResourceLocators)
			{
				foreach (var key in locator.Keys)
				{
					var keyStr = key?.ToString();
					if (!string.IsNullOrEmpty(keyStr))
						allKeys.Add(keyStr);
				}
			}
			return allKeys;
		}

		/// <summary>
		/// Gets the total download size for the given keys without downloading.
		/// Handles <see cref="InvalidKeyException"/> by falling back to per-key checks.
		/// </summary>
		private static async UniTask<long> GetDownloadSizeAsync(IList<string> keys, CancellationToken cToken)
		{
			try
			{
				var handle = Addressables.GetDownloadSizeAsync(keys);
				var result = await handle.ToUniTask(cancellationToken: cToken);
				Addressables.Release(handle);
				return result;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception)
			{
				// Fallback: check each key individually and sum up the download sizes
				long downloadSize = 0;
				foreach (var key in keys)
				{
					try
					{
						var keyHandle = Addressables.GetDownloadSizeAsync(key);
						var keySize = await keyHandle.ToUniTask(cancellationToken: cToken);
						Addressables.Release(keyHandle);
						if (keySize > 0)
							downloadSize += keySize;
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (Exception)
					{
						// Skip keys that throw — they have no matching locations
					}
				}
				return downloadSize;
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
			throw new ArgumentException(
				"UniResources: AssetReference is null or has an invalid Runtime Key. Did you forget to assign it in Inspector?");
		}
	}
}
