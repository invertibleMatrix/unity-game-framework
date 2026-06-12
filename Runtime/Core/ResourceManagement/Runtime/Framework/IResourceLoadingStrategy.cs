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
       public static readonly IResourceLoadingStrategy Default = new AddressablesLoadingStrategy();

       UniTask InitAsync(CancellationToken cToken = default);

       UniTask<bool> HasResourceAsync(string key, Type type = null, CancellationToken cToken = default);

       UniTask<IList<IResourceLocation>> GetResourceLocationsAsync(IEnumerable<string> keys, Type type,
          MergeMode mode = MergeMode.UseFirst,
          CancellationToken cToken = default);

       UniTask<IList<IResourceLocation>> GetAllResourceLocationsAsync(Type type = null, CancellationToken cToken = default);

       // --------------------------------------------------------------------------
       // CATALOG UPDATE API (The Clever Refactor)
       // --------------------------------------------------------------------------

       /// <summary>
       /// Low-level: Checks the remote CDN for catalog updates. Returns a list of catalog IDs.
       /// Does NOT apply them.
       /// </summary>
       UniTask<List<string>> CheckForCatalogUpdatesAsync(CancellationToken cToken = default);

       /// <summary>
       /// Low-level: Downloads and applies updated content catalogs using specific IDs.
       /// </summary>
       UniTask UpdateCatalogsAsync(IEnumerable<string> catalogs = null, bool autoCleanBundleCache = false, CancellationToken cToken = default);

       /// <summary>
       /// High-level: Silently checks for and applies catalog updates in one seamless operation.
       /// Returns true if catalogs were updated and mounted into memory.
       /// Use this to silently sync metadata before checking GetRemoteContentSizeAsync.
       /// </summary>
       UniTask<bool> UpdateCatalogsIfNeededAsync(bool autoCleanBundleCache = false, CancellationToken cToken = default);

       // --------------------------------------------------------------------------
       // CONTENT DOWNLOAD API
       // --------------------------------------------------------------------------

       UniTask<long> GetRemoteContentSizeAsync(string[] labels = null, CancellationToken cToken = default);

       UniTask<long> DownloadRemoteContentAsync(string[] labels = null, IProgress<float> progress = null, CancellationToken cToken = default);

       UniTask<long> GetRemoteDependenciesSizeAsync(IEnumerable<string> keys, CancellationToken cToken = default);

       UniTask<long> GetRemoteDependenciesSizeAsync(IList<IResourceLocation> locations, CancellationToken cToken = default);

       UniTask GetRemoteDependenciesAsync(IList<IResourceLocation> locations, out IOperationStatusProvider provider, CancellationToken cToken = default);

       UniTask GetRemoteDependenciesAsync(IEnumerable<string> keys, out IOperationStatusProvider provider, MergeMode mode = MergeMode.UseFirst, CancellationToken cToken = default);

       // --------------------------------------------------------------------------
       // ASYNC LOAD API
       // --------------------------------------------------------------------------
       
       UniTask<TObject> LoadAssetAsync<TObject>(string key, IProgress<float> progress = default, CancellationToken cToken = default);

       UniTask<TObject> LoadAssetAsync<TObject>(AssetReference reference, IProgress<float> progress = default, CancellationToken cToken = default);

       UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IEnumerable<string> keys, MergeMode mode = MergeMode.UseFirst, IProgress<float> progress = default, CancellationToken cToken = default);

       UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IList<IResourceLocation> keys, IProgress<float> progress = default, CancellationToken cToken = default);

       UniTask<GameObject> SpawnAsync(string key, Transform root, IProgress<float> progress = default, CancellationToken cToken = default);

       UniTask<GameObject> SpawnAsync(AssetReference reference, Transform root, IProgress<float> progress = default, CancellationToken cToken = default);

       // --------------------------------------------------------------------------
       // SCENE API
       // --------------------------------------------------------------------------

       UniTask<SceneInstance> LoadSceneAsync(string key, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true, IProgress<float> progress = default, CancellationToken cToken = default);

       UniTask UnloadSceneAsync(SceneInstance scene, IProgress<float> progress = default, CancellationToken cToken = default);

       // --------------------------------------------------------------------------
       // SYNCHRONOUS API (Blocking)
       // --------------------------------------------------------------------------

       TObject LoadAsset<TObject>(string key);
       TObject LoadAsset<TObject>(AssetReference reference);
       GameObject Spawn(string key, Transform root);
       GameObject Spawn(AssetReference reference, Transform root);

       // --------------------------------------------------------------------------
       // CLEANUP
       // --------------------------------------------------------------------------

       void DisposeAsset(UnityEngine.Object uObject);
       void DisposeAssetsGroup<T>(AssetsGroup<T> group);
       bool DisposeInstance(GameObject gObject);
       void Reset();
    }
}