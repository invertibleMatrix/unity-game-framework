using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AK.Core.ResourceManagement
{
    public static class UniResources
    {
       [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
       private static void OnReset() => _strategy.Reset();

       private static IResourceLoadingStrategy _strategy = IResourceLoadingStrategy.Default;

       public static void OverrideStrategy(IResourceLoadingStrategy strategy)
       {
          _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy), "Strategy cannot be null.");
       }

       public static UniTask InitAsync(CancellationToken cToken = default) => _strategy.InitAsync(cToken);

       public static UniTask<bool> HasResourceAsync<TResource>(string key, CancellationToken cToken = default)
       {
          return _strategy.HasResourceAsync(key, typeof(TResource), cToken);
       }

       public static UniTask<IList<IResourceLocation>> GetAllResourceLocationsAsync(Type type = null, CancellationToken cToken = default)
       {
          return _strategy.GetAllResourceLocationsAsync(type, cToken);
       }

       // --------------------------------------------------------------------------
       // CATALOG UPDATE API
       // --------------------------------------------------------------------------

       public static UniTask<List<string>> CheckForCatalogUpdatesAsync(CancellationToken cToken = default)
       {
          return _strategy.CheckForCatalogUpdatesAsync(cToken);
       }

       public static UniTask UpdateCatalogsAsync(IEnumerable<string> catalogs = null, bool autoCleanBundleCache = false, CancellationToken cToken = default)
       {
          return _strategy.UpdateCatalogsAsync(catalogs, autoCleanBundleCache, cToken);
       }

       /// <summary>
       /// Silently checks for and applies catalog updates in one seamless operation.
       /// Call this immediately after InitAsync to ensure the local client has the latest metadata.
       /// </summary>
       public static UniTask<bool> UpdateCatalogsIfNeededAsync(bool autoCleanBundleCache = false, CancellationToken cToken = default)
       {
          return _strategy.UpdateCatalogsIfNeededAsync(autoCleanBundleCache, cToken);
       }

       // --------------------------------------------------------------------------
       // CONTENT DOWNLOAD API
       // --------------------------------------------------------------------------

       public static UniTask<long> GetRemoteContentSizeAsync(string[] labels = null, CancellationToken cToken = default)
       {
          return _strategy.GetRemoteContentSizeAsync(labels, cToken);
       }

       public static UniTask<long> DownloadRemoteContentAsync(string[] labels = null, IProgress<float> progress = null, CancellationToken cToken = default)
       {
          return _strategy.DownloadRemoteContentAsync(labels, progress, cToken);
       }

       public static UniTask<IList<IResourceLocation>> GetResourceLocationsAsync(IEnumerable<string> keys, Type type = null, MergeMode mode = MergeMode.UseFirst, CancellationToken cToken = default)
       {
          return _strategy.GetResourceLocationsAsync(keys, type, mode, cToken);
       }

       public static UniTask<long> GetRemoteResourcesSizeAsync(IEnumerable<string> keys, CancellationToken cToken = default)
       {
          return _strategy.GetRemoteDependenciesSizeAsync(keys, cToken);
       }

       public static UniTask<long> GetRemoteResourcesSizeAsync(IList<IResourceLocation> locations, CancellationToken cToken = default)
       {
          return _strategy.GetRemoteDependenciesSizeAsync(locations, cToken);
       }

       public static UniTask GetRemoteDependenciesAsync(IEnumerable<string> keys, out IOperationStatusProvider provider, MergeMode mode = MergeMode.UseFirst, CancellationToken cToken = default)
       {
          return _strategy.GetRemoteDependenciesAsync(keys, out provider, mode, cToken);
       }

       public static UniTask GetRemoteDependenciesAsync(IList<IResourceLocation> locations, out IOperationStatusProvider provider, CancellationToken cToken = default)
       {
          return _strategy.GetRemoteDependenciesAsync(locations, out provider, cToken);
       }

       // --------------------------------------------------------------------------
       // ASYNC LOAD API
       // --------------------------------------------------------------------------

       public static UniTask<TObject> LoadAssetAsync<TObject>(string key, IProgress<float> progress = default, CancellationToken cToken = default)
       {
          return _strategy.LoadAssetAsync<TObject>(key, progress, cToken);
       }

       public static UniTask<TObject> LoadAssetAsync<TObject>(AssetReferenceT<TObject> reference, IProgress<float> progress = default, CancellationToken cToken = default) where TObject : Object
       {
          return _strategy.LoadAssetAsync<TObject>(reference, progress, cToken);
       }

       public static UniTask<TObject> LoadAssetAsync<TObject>(AssetReference reference, IProgress<float> progress = default, CancellationToken cToken = default)
       {
          return _strategy.LoadAssetAsync<TObject>(reference, progress, cToken);
       }

       public static UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IEnumerable<string> keys, MergeMode mode = MergeMode.UseFirst, IProgress<float> progress = default, CancellationToken cToken = default)
       {
          return _strategy.LoadAssetsAsync<TObject>(keys, mode, progress, cToken);
       }

       public static UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IEnumerable<AssetReference> references, MergeMode mode = MergeMode.UseFirst, IProgress<float> progress = default, CancellationToken cToken = default)
       {
          var validKeys = references.Where(r => r != null && r.RuntimeKeyIsValid()).Select(r => r.RuntimeKey.ToString());
          return _strategy.LoadAssetsAsync<TObject>(validKeys, mode, progress, cToken);
       }

       public static UniTask<GameObject> SpawnAsync(string key, Transform root, IProgress<float> progress = default, CancellationToken cToken = default)
       {
          return _strategy.SpawnAsync(key, root, progress, cToken);
       }

       public static UniTask<GameObject> SpawnAsync(AssetReference reference, Transform root = null, IProgress<float> progress = default, CancellationToken cToken = default)
       {
          return _strategy.SpawnAsync(reference, root, progress, cToken);
       }

       public static async UniTask<TComponent> SpawnAsync<TComponent>(AssetReference reference, Transform root = null, IProgress<float> progress = default, CancellationToken cToken = default) where TComponent : Component
       {
          var go = await _strategy.SpawnAsync(reference, root, progress, cToken);
          if (go.TryGetComponent<TComponent>(out var component)) return component;

          DisposeInstance(go);
          throw new InvalidOperationException($"Spawned object '{go.name}' does not have component '{typeof(TComponent).Name}'");
       }

       // --------------------------------------------------------------------------
       // SCENE API
       // --------------------------------------------------------------------------

       public static UniTask<SceneInstance> LoadSceneAsync(string key, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true, IProgress<float> progress = default, CancellationToken cToken = default)
       {
          return _strategy.LoadSceneAsync(key, mode, activateOnLoad, progress, cToken);
       }

       public static UniTask UnloadSceneAsync(SceneInstance scene, IProgress<float> progress = default, CancellationToken cToken = default)
       {
          return _strategy.UnloadSceneAsync(scene, progress, cToken);
       }

       // --------------------------------------------------------------------------
       // SYNCHRONOUS API
       // --------------------------------------------------------------------------

       public static TObject LoadAsset<TObject>(string key) => _strategy.LoadAsset<TObject>(key);
       public static TObject LoadAsset<TObject>(AssetReference reference) => _strategy.LoadAsset<TObject>(reference);
       public static TObject LoadAsset<TObject>(AssetReferenceT<TObject> reference) where TObject : Object => _strategy.LoadAsset<TObject>(reference);
       public static GameObject Spawn(string key, Transform root = null) => _strategy.Spawn(key, root);
       public static GameObject Spawn(AssetReference reference, Transform root = null) => _strategy.Spawn(reference, root);
       
       public static TComponent Spawn<TComponent>(AssetReference reference, Transform root = null) where TComponent : Component
       {
          var go = _strategy.Spawn(reference, root);
          if (go.TryGetComponent<TComponent>(out var component)) return component;

          DisposeInstance(go);
          throw new InvalidOperationException($"Spawned object '{go.name}' does not have component '{typeof(TComponent).Name}'");
       }

       // --------------------------------------------------------------------------
       // CLEANUP
       // --------------------------------------------------------------------------

       public static void DisposeAsset(Object uObject) => _strategy.DisposeAsset(uObject);
       public static void DisposeAssetsGroup<T>(AssetsGroup<T> group) => _strategy.DisposeAssetsGroup(group);
       public static bool DisposeInstance(GameObject gObject) => _strategy.DisposeInstance(gObject);

       public static UniTask RemoveUnusedResources(CancellationToken cToken = default)
       {
          return Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: cToken);
       }
    }
}