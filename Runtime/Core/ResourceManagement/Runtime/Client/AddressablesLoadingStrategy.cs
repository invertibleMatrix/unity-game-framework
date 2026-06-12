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
		public async UniTask<bool> CheckForCatalogUpdatesAsync(CancellationToken cToken = default)
		{
			var catalogUpdates = await Addressables.CheckForCatalogUpdates().ToUniTask(cancellationToken: cToken);
	
			if (catalogUpdates == null || catalogUpdates.Count == 0)
				return false;
	
			await Addressables.UpdateCatalogs(catalogUpdates).ToUniTask(cancellationToken: cToken);
			return true;
		}

		/// <inheritdoc />
		public async UniTask<long> DownloadRemoteContentAsync(string[] labels = null, CancellationToken cToken = default)
		{
			// Strategy: Use key-based GetDownloadSizeAsync which checks all bundles for the given keys.
			// If labels are provided, use them as keys. Otherwise, enumerate all keys from the catalog.
			// The wildcard "*" approach via LoadResourceLocationsAsync doesn't reliably enumerate
			// all locations in all catalog configurations, so we enumerate catalog keys directly.
			
			IEnumerable<string> keys;
			if (labels != null && labels.Length > 0)
			{
				keys = labels;
			}
			else
			{
				// Enumerate all keys from all resource locators (catalogs)
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
				keys = allKeys;
			}

			// Use key-based download size check. This may throw InvalidKeyException for
			// individual keys that have no matching locations, so we handle that gracefully.
			long downloadSize;
			try
			{
				downloadSize = await Addressables.GetDownloadSizeAsync(keys)
					.ToUniTask(cancellationToken: cToken);
			}
			catch (Exception)
			{
				// Fallback: check each key individually and sum up the download sizes
				downloadSize = 0;
				foreach (var key in keys)
				{
					try
					{
						var keySize = await Addressables.GetDownloadSizeAsync(key.ToString())
							.ToUniTask(cancellationToken: cToken);
						if (keySize > 0)
							downloadSize += keySize;
					}
					catch (Exception)
					{
						// Skip keys that throw — they have no matching locations
					}
				}
			}

			if (downloadSize > 0)
			{
				var downloadOp = Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union, true);
				await downloadOp.ToUniTask(cancellationToken: cToken);
				
				if (downloadOp.Status == AsyncOperationStatus.Failed)
					Debug.LogError($"[AddressablesStrategy] Download FAILED: {downloadOp.OperationException}");
				
				Addressables.Release(downloadOp);
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