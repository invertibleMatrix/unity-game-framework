using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace AK.Core.ResourceManagement
{
	/// <summary>
	/// <see cref="IResourceLoadingStrategy"/> Is A Contract Which Provide Basic Api To Manage Resources/Assets...
	/// </summary>
	public partial interface IResourceLoadingStrategy
	{
		/// <summary>
		/// The default strategy implementation.
		/// </summary>
		public static readonly IResourceLoadingStrategy Default = new AddressablesLoadingStrategy();

		/// <summary>
		/// <see cref="InitAsync"/> Is Going To Init The Current Strategy & Returns A Task Over It Completion...
		/// </summary>
		UniTask InitAsync(CancellationToken cToken = default);

		/// <summary>
		/// Check Whether The Given Key's Asset Exists In Current Resources Or Not...
		/// </summary>
		UniTask<bool> HasResourceAsync(string key, Type type = null, CancellationToken cToken = default);

		/// <summary>
		/// Loads the resource locations specified by a set of keys.
		/// </summary>
		public UniTask<IList<IResourceLocation>> GetResourceLocationsAsync(IEnumerable<string> keys, Type type,
			MergeMode mode = MergeMode.Union,
			CancellationToken cToken = default);

		/// <summary>
		/// Enumerates all resource locations from the catalog.
		/// Uses the wildcard key to match every location regardless of label assignment.
		/// </summary>
		UniTask<IList<IResourceLocation>> GetAllResourceLocationsAsync(Type type = null, CancellationToken cToken = default);

		/// <summary>
		/// Checks the remote CDN for catalog updates and applies them if available.
		/// Returns true if a catalog update was downloaded and applied.
		/// Must be called after <see cref="InitAsync"/> and before any asset loading.
		/// </summary>
		UniTask<bool> CheckForCatalogUpdatesAsync(CancellationToken cToken = default);

		/// <summary>
		/// Resolves resource locations and downloads all remote content.
		/// If <paramref name="labels"/> is null or empty, all catalog locations are enumerated.
		/// Otherwise, only locations matching the provided labels are resolved.
		/// Uses location-based APIs internally, which never throw <see cref="InvalidKeyException"/>.
		/// Returns the total download size in bytes (0 if nothing to download).
		/// </summary>
		UniTask<long> DownloadRemoteContentAsync(string[] labels = null, CancellationToken cToken = default);

		/// <summary>
		/// Determines the required download size for assets identified by keys.
		/// Throws <see cref="InvalidKeyException"/> if any key has no matching locations.
		/// </summary>
		UniTask<long> GetRemoteDependenciesSizeAsync(IEnumerable<string> keys, CancellationToken cToken = default);

		/// <summary>
		/// Determines the required download size for assets identified by locations.
		/// Location-based API never throws — returns 0 for empty lists.
		/// </summary>
		UniTask<long> GetRemoteDependenciesSizeAsync(IList<IResourceLocation> locations, CancellationToken cToken = default);

		/// <summary>
		/// Downloads dependencies of assets identified by a list of locations.
		/// </summary>
		UniTask GetRemoteDependenciesAsync(IList<IResourceLocation> locations, out IOperationStatusProvider provider,
			CancellationToken cToken = default);

		/// <summary>
		/// Downloads dependencies of assets identified by a list of keys.
		/// Throws <see cref="InvalidKeyException"/> if any key has no matching locations.
		/// </summary>
		UniTask GetRemoteDependenciesAsync(IEnumerable<string> keys, out IOperationStatusProvider provider,
			MergeMode mode = MergeMode.Union,
			CancellationToken cToken = default);

		// --------------------------------------------------------------------------
		// ASYNC API
		// --------------------------------------------------------------------------
		
		/// <summary>
		/// Loads a single asset identified by a key such as an address or label.
		/// </summary>
		UniTask<TObject> LoadAssetAsync<TObject>(string key, IProgress<float> progress = default,
			CancellationToken cToken = default);

		/// <summary>
		/// Loads a single asset identified by an <see cref="AssetReference"/>.
		/// </summary>
		UniTask<TObject> LoadAssetAsync<TObject>(AssetReference reference, IProgress<float> progress = default, 
			CancellationToken cToken = default);

		/// <summary>
		/// Loads multiple assets, based on the list of keys provided.
		/// </summary>
		UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IEnumerable<string> keys,
			MergeMode mode = MergeMode.Union,
			IProgress<float> progress = default,
			CancellationToken cToken = default);

		/// <summary>
		/// Loads multiple assets, based on the list of locations provided.
		/// </summary>
		UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IList<IResourceLocation> keys,
			IProgress<float> progress = default,
			CancellationToken cToken = default);

		/// <summary>
		/// Load & Spawn Single GameObject In The Game asynchronously.
		/// </summary>
		UniTask<GameObject> SpawnAsync(string key, Transform root, IProgress<float> progress = default,
			CancellationToken cToken = default);

		/// <summary>
		/// Load & Spawn Single GameObject In The Game from an <see cref="AssetReference"/>.
		/// </summary>
		UniTask<GameObject> SpawnAsync(AssetReference reference, Transform root, IProgress<float> progress = default, 
			CancellationToken cToken = default);

		// --------------------------------------------------------------------------
		// SYNCHRONOUS API (Blocking)
		// --------------------------------------------------------------------------

		/// <summary>
		/// Loads a single asset Synchronously (Blocks Main Thread).
		/// </summary>
		/// <remarks>
		/// WARNING: This forces the asset to load immediately. It will freeze the game frame until completion.
		/// Do not use on WebGL.
		/// </remarks>
		TObject LoadAsset<TObject>(string key);

		/// <summary>
		/// Loads a single asset Synchronously from an <see cref="AssetReference"/> (Blocks Main Thread).
		/// </summary>
		TObject LoadAsset<TObject>(AssetReference reference);

		/// <summary>
		/// Spawns a GameObject Synchronously (Blocks Main Thread).
		/// </summary>
		GameObject Spawn(string key, Transform root);

		/// <summary>
		/// Spawns a GameObject Synchronously from an <see cref="AssetReference"/> (Blocks Main Thread).
		/// </summary>
		GameObject Spawn(AssetReference reference, Transform root);

		// --------------------------------------------------------------------------
		// CLEANUP
		// --------------------------------------------------------------------------

		/// <summary>
		/// Release the operation and its associated resources.
		/// </summary>
		void DisposeAsset(UnityEngine.Object uObject);

		/// <summary>
		/// Release the assets group and its associated resources.
		/// </summary>
		void DisposeAssetsGroup<T>(AssetsGroup<T> group);

		/// <summary>
		/// Releases and destroys an object that was created via Spawn methods.
		/// </summary>
		bool DisposeInstance(GameObject gObject);

		/// <summary>
		/// Reset current strategy & Reset the private states associated with it...
		/// </summary>
		void Reset();
	}
}