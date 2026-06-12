using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

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
			MergeMode mode = MergeMode.UseFirst,
			CancellationToken cToken = default);

		/// <summary>
		/// Enumerates all resource locations from the catalog.
		/// Uses the wildcard key to match every location regardless of label assignment.
		/// </summary>
		UniTask<IList<IResourceLocation>> GetAllResourceLocationsAsync(Type type = null, CancellationToken cToken = default);

		/// <summary>
		/// Checks the remote CDN for catalog updates WITHOUT applying them.
		/// Returns a list of catalog IDs that need updating, or an empty list if none.
		/// Use this for a two-step flow: check first, prompt user, then call <see cref="ApplyCatalogUpdatesAsync"/>.
		/// Must be called after <see cref="InitAsync"/> and before any asset loading.
		/// </summary>
		/// <remarks>Does NOT apply updates. For check+apply in one call, use <see cref="CheckForCatalogUpdatesAsync(IProgress{float}, CancellationToken)"/>.</remarks>
		UniTask<List<string>> HasCatalogUpdatesAsync(CancellationToken cToken = default);

		/// <summary>
		/// Applies catalog updates that were discovered by <see cref="HasCatalogUpdatesAsync"/>.
		/// <paramref name="catalogIds"/> is the list returned by <see cref="HasCatalogUpdatesAsync"/>.
		/// <paramref name="progress"/> reports update progress as a float between 0 and 1.
		/// </summary>
		UniTask ApplyCatalogUpdatesAsync(List<string> catalogIds, IProgress<float> progress = null, CancellationToken cToken = default);

		/// <summary>
		/// Convenience method that checks for catalog updates AND applies them in one call.
		/// Internally calls <see cref="HasCatalogUpdatesAsync"/> then <see cref="ApplyCatalogUpdatesAsync"/>.
		/// Returns true if a catalog update was downloaded and applied, false if none were available.
		/// Must be called after <see cref="InitAsync"/> and before any asset loading.
		/// <paramref name="progress"/> reports update progress as a float between 0 and 1.
		/// </summary>
		UniTask<bool> CheckForCatalogUpdatesAsync(IProgress<float> progress = null, CancellationToken cToken = default);

		/// <summary>
		/// Checks the total download size for remote content without downloading.
		/// If <paramref name="labels"/> is null or empty, all catalog locations are enumerated.
		/// Otherwise, only locations matching the provided labels are checked.
		/// Returns the total download size in bytes (0 if nothing to download).
		/// </summary>
		UniTask<long> GetRemoteContentSizeAsync(string[] labels = null, CancellationToken cToken = default);

		/// <summary>
		/// Resolves resource locations and downloads all remote content.
		/// If <paramref name="labels"/> is null or empty, all catalog locations are enumerated.
		/// Otherwise, only locations matching the provided labels are resolved.
		/// Uses location-based APIs internally, which never throw <see cref="InvalidKeyException"/>.
		/// Returns the total download size in bytes (0 if nothing to download).
		/// <paramref name="progress"/> reports download progress as a float between 0 and 1.
		/// </summary>
		UniTask<long> DownloadRemoteContentAsync(string[] labels = null, IProgress<float> progress = null, CancellationToken cToken = default);

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
			MergeMode mode = MergeMode.UseFirst,
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
			MergeMode mode = MergeMode.UseFirst,
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
		// SCENE API
		// --------------------------------------------------------------------------

		/// <summary>
		/// Loads an Addressable scene by key. Internally uses SceneManager.LoadSceneAsync.
		/// <para>⚠ If <paramref name="activateOnLoad"/> is false, it blocks the entire async operation queue
		/// until you call <see cref="SceneInstance.ActivateAsync"/> on the result.</para>
		/// </summary>
		UniTask<SceneInstance> LoadSceneAsync(string key, LoadSceneMode mode = LoadSceneMode.Single,
			bool activateOnLoad = true, IProgress<float> progress = default,
			CancellationToken cToken = default);

		/// <summary>
		/// Unloads a previously loaded Addressable scene.
		/// </summary>
		UniTask UnloadSceneAsync(SceneInstance scene, IProgress<float> progress = default,
			CancellationToken cToken = default);

		// --------------------------------------------------------------------------
		// CATALOG UPDATE API (Low-level split)
		// --------------------------------------------------------------------------

		/// <summary>
		/// Low-level: checks if any loaded catalogs have remote updates available.
		/// Returns a list of modified catalog IDs, or an empty list if none.
		/// Does NOT apply updates — call <see cref="UpdateCatalogsAsync"/> after this.
		/// For the higher-level check+apply flow, see <see cref="HasCatalogUpdatesAsync"/>
		/// + <see cref="ApplyCatalogUpdatesAsync"/> or the convenience
		/// <see cref="CheckForCatalogUpdatesAsync(IProgress{float}, CancellationToken)"/>.
		/// </summary>
		UniTask<List<string>> CheckForCatalogUpdatesAsync(CancellationToken cToken = default);

		/// <summary>
		/// Downloads and applies updated content catalogs.
		/// When <paramref name="autoCleanBundleCache"/> is true, removes bundles no longer referenced by any catalog.
		/// <para>⚠ This blocks all Addressable requests until complete. Call at startup or during loading screens.</para>
		/// </summary>
		UniTask UpdateCatalogsAsync(IEnumerable<string> catalogs = null,
			bool autoCleanBundleCache = false,
			CancellationToken cToken = default);

		// --------------------------------------------------------------------------
		// SYNCHRONOUS API (Blocking)
		// --------------------------------------------------------------------------

		/// <summary>
		/// Loads a single asset Synchronously (Blocks Main Thread).
		/// </summary>
		/// <remarks>
		/// WARNING: This forces the asset to load immediately. It will freeze the game frame until completion.
		/// Do not use on WebGL. Completes ALL active asset load operations, not just this one.
		/// Do not call in Awake — use Start instead to avoid deadlocks.
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
