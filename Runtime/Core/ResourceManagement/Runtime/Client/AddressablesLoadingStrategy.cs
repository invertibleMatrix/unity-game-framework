using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using AK.Core.Extensions;
using Object = UnityEngine.Object;

namespace AK.Core.ResourceManagement
{
	/// <inheritdoc />
	public sealed class AddressablesLoadingStrategy : IResourceLoadingStrategy
	{
		private readonly Dictionary<Guid, AsyncOperationHandle> _operationsLookup = new();

		/// <inheritdoc />
		public UniTask InitAsync(CancellationToken cToken = default)
		{
			return Addressables.InitializeAsync().ToUniTask(cancellationToken: cToken);
		}

		/// <inheritdoc />
		public async UniTask<bool> HasResourceAsync(string key, Type type = null, CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			var locations = await Addressables.LoadResourceLocationsAsync(key, type).WithCancellation(cToken);
			return locations.Count > 0;
		}

		/// <inheritdoc />
		public UniTask<IList<IResourceLocation>> GetResourceLocationsAsync(IEnumerable<string> keys, Type type,
		                                                                   MergeMode mode,
		                                                                   CancellationToken cToken = default)
		{
			return Addressables.LoadResourceLocationsAsync(keys, mode.Convert(), type)
			                   .ToUniTask(cancellationToken: cToken);
		}

		/// <inheritdoc />
		public UniTask<IList<IResourceLocation>> GetAllResourceLocationsAsync(Type type = null,
		                                                                     CancellationToken cToken = default)
		{
			return Addressables.LoadResourceLocationsAsync("*", type)
			                   .ToUniTask(cancellationToken: cToken);
		}

		/// <inheritdoc />
		public async UniTask<List<string>> HasCatalogUpdatesAsync(CancellationToken cToken = default)
		{
			var catalogUpdates = await Addressables.CheckForCatalogUpdates().ToUniTask(cancellationToken: cToken);
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
			Addressables.Release(updateOp);
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
						var locs = await Addressables.LoadResourceLocationsAsync(key).ToUniTask(cancellationToken: cToken);
						if (locs != null)
						{
							foreach (var loc in locs)
							{
								if (loc != null && !locations.Contains(loc))
									locations.Add(loc);
							}
						}
					}
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
		public UniTask<long> GetRemoteDependenciesSizeAsync(IEnumerable<string> keys,
		                                                    CancellationToken cToken = default)
		{
			return Addressables.GetDownloadSizeAsync(keys).ToUniTask(cancellationToken: cToken);
		}

		/// <inheritdoc />
		public UniTask<long> GetRemoteDependenciesSizeAsync(IList<IResourceLocation> locations,
		                                                    CancellationToken cToken = default)
		{
			return Addressables.GetDownloadSizeAsync(locations).ToUniTask(cancellationToken: cToken);
		}

		/// <inheritdoc />
		public UniTask GetRemoteDependenciesAsync(IList<IResourceLocation> locations,
		                                          out IOperationStatusProvider provider,
		                                          CancellationToken cToken = default)
		{
			var asyncOp = Addressables.DownloadDependenciesAsync(locations, true);
			provider = new OperationStatusProvider(asyncOp);
			return asyncOp.ToUniTask(cancellationToken: cToken);
		}

		/// <inheritdoc />
		public UniTask GetRemoteDependenciesAsync(IEnumerable<string> keys, out IOperationStatusProvider provider,
		                                          MergeMode mode = MergeMode.Union,
		                                          CancellationToken cToken = default)
		{
			var asyncOp = Addressables.DownloadDependenciesAsync(keys, mode.Convert(), true);
			provider = new OperationStatusProvider(asyncOp);
			return asyncOp.ToUniTask(cancellationToken: cToken);
		}

		// --------------------------------------------------------------------------
		// ASYNC IMPLEMENTATION
		// --------------------------------------------------------------------------

		/// <inheritdoc />
		public UniTask<TObject> LoadAssetAsync<TObject>(string key, IProgress<float> progress = default,
		                                                CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			var asyncOp = Addressables.LoadAssetAsync<TObject>(key);
			return TrackOperation(asyncOp).ToUniTask(progress: progress, cancellationToken: cToken);
		}

		/// <inheritdoc />
		public UniTask<TObject> LoadAssetAsync<TObject>(AssetReference reference, IProgress<float> progress = default,
		                                                CancellationToken cToken = default)
		{
			ValidateReference(reference);
			var asyncOp = Addressables.LoadAssetAsync<TObject>(reference);
			return TrackOperation(asyncOp).ToUniTask(progress: progress, cancellationToken: cToken);
		}

		/// <inheritdoc />
		public async UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IEnumerable<string> keys,
		                                                                    MergeMode mode = MergeMode.Union,
		                                                                    IProgress<float> progress = default,
		                                                                    CancellationToken cToken = default)
		{
			var asyncOp = Addressables.LoadAssetsAsync<TObject>(keys, default, mode.Convert());
			var task = TrackOperation(asyncOp).ToUniTask(progress: progress, cancellationToken: cToken);
			var assetsGroup = new AssetsGroup<TObject>(await task);

			_operationsLookup[assetsGroup.Guid] = asyncOp;
			return assetsGroup;
		}

		/// <inheritdoc />
		public async UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IList<IResourceLocation> keys,
		                                                                    IProgress<float> progress = default,
		                                                                    CancellationToken cToken = default)
		{
			var asyncOp = Addressables.LoadAssetsAsync<TObject>(keys, default);
			var task = TrackOperation(asyncOp).ToUniTask(progress: progress, cancellationToken: cToken);
			var assetsGroup = new AssetsGroup<TObject>(await task);

			_operationsLookup[assetsGroup.Guid] = asyncOp;
			return assetsGroup;
		}

		/// <inheritdoc />
		public UniTask<GameObject> SpawnAsync(string key, Transform root, IProgress<float> progress = default,
		                                      CancellationToken cToken = default)
		{
			CheckResourceKey(key);
			var asyncOp = Addressables.InstantiateAsync(key, root);
			return TrackOperation(asyncOp).ToUniTask(progress: progress, cancellationToken: cToken);
		}

		/// <inheritdoc />
		public UniTask<GameObject> SpawnAsync(AssetReference reference, Transform root, IProgress<float> progress = default,
		                                      CancellationToken cToken = default)
		{
			ValidateReference(reference);
			var asyncOp = Addressables.InstantiateAsync(reference, root);
			return TrackOperation(asyncOp).ToUniTask(progress: progress, cancellationToken: cToken);
		}

		// --------------------------------------------------------------------------
		// SYNCHRONOUS IMPLEMENTATION (WaitForCompletion)
		// --------------------------------------------------------------------------

		/// <inheritdoc />
		public TObject LoadAsset<TObject>(string key)
		{
			CheckResourceKey(key);
			var op = Addressables.LoadAssetAsync<TObject>(key);
			return op.WaitForCompletion();
		}

		/// <inheritdoc />
		public TObject LoadAsset<TObject>(AssetReference reference)
		{
			ValidateReference(reference);
			var op = Addressables.LoadAssetAsync<TObject>(reference);
			return op.WaitForCompletion();
		}

		/// <inheritdoc />
		public GameObject Spawn(string key, Transform root)
		{
			CheckResourceKey(key);
			var op = Addressables.InstantiateAsync(key, root);
			return op.WaitForCompletion();
		}

		/// <inheritdoc />
		public GameObject Spawn(AssetReference reference, Transform root)
		{
			ValidateReference(reference);
			var op = Addressables.InstantiateAsync(reference, root);
			return op.WaitForCompletion();
		}

		// --------------------------------------------------------------------------
		// CLEANUP & HELPERS
		// --------------------------------------------------------------------------

		/// <inheritdoc />
		public void DisposeAsset(UnityEngine.Object uObject) => Addressables.Release(uObject);

		/// <inheritdoc />
		public void DisposeAssetsGroup<T>(AssetsGroup<T> group)
		{
			if (group == null) return;
			if (group == AssetsGroup<T>.Default) return;

			if (_operationsLookup.TryGetValue(group.Guid, out var operation))
			{
				group.DisposeAssets();
				_operationsLookup.Remove(group.Guid);

				Addressables.Release(operation);
				return;
			}

			Debug.LogError("--> Trying To Dispose An Assets Group Which Is Not Getting Track!");
		}

		/// <inheritdoc />
		public bool DisposeInstance(GameObject gObject)
		{
			if (gObject == null) return false;

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
			_operationsLookup.Clear();
		}

		private static AsyncOperationHandle<T> TrackOperation<T>(AsyncOperationHandle<T> asyncOp)
		{
			// TODO: Track Operations To Log & Keep States
			return asyncOp;
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
				return await Addressables.GetDownloadSizeAsync(keys)
					.ToUniTask(cancellationToken: cToken);
			}
			catch (Exception)
			{
				// Fallback: check each key individually and sum up the download sizes
				long downloadSize = 0;
				foreach (var key in keys)
				{
					try
					{
						var keySize = await Addressables.GetDownloadSizeAsync(key)
							.ToUniTask(cancellationToken: cToken);
						if (keySize > 0)
							downloadSize += keySize;
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