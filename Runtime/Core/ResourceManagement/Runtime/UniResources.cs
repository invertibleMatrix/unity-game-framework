using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace AK.Core.ResourceManagement
{
	/// <summary>
	/// <para>The <see cref="UniResources"/> allows you to find and access Objects including assets.
	/// It uses <see cref="IResourceLoadingStrategy"/> To work & Strategy can be overridden with <see cref="OverrideStrategy"/>.
	/// </para>
	/// </summary>
	public static class UniResources
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void OnReset() => _strategy.Reset();

		private static IResourceLoadingStrategy _strategy = IResourceLoadingStrategy.Default;

		/// <summary>
		/// <see cref="OverrideStrategy"/> Is Going To Override The Previous Concrete Of
		/// <see cref="IResourceLoadingStrategy"/>' Instance..
		/// </summary>
		/// <param name="strategy">Concrete Of <see cref="IResourceLoadingStrategy"/> TO Load/Override...</param>
		public static void OverrideStrategy(IResourceLoadingStrategy strategy)
		{
			_strategy = strategy ?? throw new ArgumentNullException(nameof(strategy), "Strategy cannot be null.");
		}

		/// <summary>
		/// <see cref="IResourceLoadingStrategy.InitAsync"/> Is Going To Init The Current Strategy & Returns A Task Over It Completion...
		/// </summary>
		public static UniTask InitAsync(CancellationToken cToken = default) => _strategy.InitAsync(cToken);

		// --------------------------------------------------------------------------
		// ASYNC API (Standard)
		// --------------------------------------------------------------------------

		/// <summary>
		/// Check Whether The Given Key's Asset Exists In Current Resources Or Not...
		/// </summary>
		public static UniTask<bool> HasResourceAsync<TResource>(string key, CancellationToken cToken = default)
		{
			return _strategy.HasResourceAsync(key, typeof(TResource), cToken);
		}

		/// <summary>
		/// Determines the required download size.
		/// </summary>
		public static UniTask<long> GetRemoteResourcesSizeAsync(IEnumerable<string> keys,
			CancellationToken cToken = default)
		{
			return _strategy.GetRemoteDependenciesSizeAsync(keys, cToken);
		}

		/// <summary>
		/// Downloads dependencies of assets identified by a list of keys.
		/// </summary>
		public static UniTask GetRemoteDependenciesAsync(IEnumerable<string> keys, out IOperationStatusProvider provider,
			MergeMode mode = MergeMode.Union,
			CancellationToken cToken = default)
		{
			return _strategy.GetRemoteDependenciesAsync(keys, out provider, mode, cToken);
		}

		/// <summary>
		/// Loads a single asset asynchronously (String Key).
		/// </summary>
		public static UniTask<TObject> LoadAssetAsync<TObject>(string key, IProgress<float> progress = default,
			CancellationToken cToken = default)
		{
			return _strategy.LoadAssetAsync<TObject>(key, progress, cToken);
		}

		/// <summary>
		/// Loads a single asset asynchronously (Type-Safe AssetReference).
		/// </summary>
		public static UniTask<TObject> LoadAssetAsync<TObject>(AssetReferenceT<TObject> reference, IProgress<float> progress = default,
			CancellationToken cToken = default)
			where TObject : Object
		{
			return _strategy.LoadAssetAsync<TObject>(reference, progress, cToken);
		}

		/// <summary>
		/// Loads a single asset asynchronously (Generic AssetReference).
		/// </summary>
		public static UniTask<TObject> LoadAssetAsync<TObject>(AssetReference reference, IProgress<float> progress = default,
			CancellationToken cToken = default)
		{
			return _strategy.LoadAssetAsync<TObject>(reference, progress, cToken);
		}

		/// <summary>
		/// Loads multiple assets asynchronously.
		/// </summary>
		public static UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IEnumerable<string> keys,
			MergeMode mode = MergeMode.Union,
			IProgress<float> progress = default,
			CancellationToken cToken = default)
		{
			return _strategy.LoadAssetsAsync<TObject>(keys, mode, progress, cToken);
		}

		/// <summary>
		/// Loads multiple assets from AssetReferences asynchronously.
		/// </summary>
		public static UniTask<AssetsGroup<TObject>> LoadAssetsAsync<TObject>(IEnumerable<AssetReference> references,
			MergeMode mode = MergeMode.Union,
			IProgress<float> progress = default,
			CancellationToken cToken = default)
		{
			var validKeys = references
				.Where(r => r != null && r.RuntimeKeyIsValid())
				.Select(r => r.RuntimeKey.ToString());

			return _strategy.LoadAssetsAsync<TObject>(validKeys, mode, progress, cToken);
		}

		/// <summary>
		/// Spawn Single GameObject In The Game asynchronously (String Key).
		/// </summary>
		public static UniTask<GameObject> SpawnAsync(string key, Transform root, IProgress<float> progress = default,
			CancellationToken cToken = default)
		{
			return _strategy.SpawnAsync(key, root, progress, cToken);
		}

		/// <summary>
		/// Spawn Single GameObject In The Game asynchronously (AssetReference).
		/// </summary>
		public static UniTask<GameObject> SpawnAsync(AssetReference reference, Transform root = null, IProgress<float> progress = default,
			CancellationToken cToken = default)
		{
			return _strategy.SpawnAsync(reference, root, progress, cToken);
		}

		/// <summary>
		/// Spawn and get Component asynchronously.
		/// </summary>
		public static async UniTask<TComponent> SpawnAsync<TComponent>(AssetReference reference, Transform root = null, IProgress<float> progress = default,
			CancellationToken cToken = default)
			where TComponent : Component
		{
			var go = await _strategy.SpawnAsync(reference, root, progress, cToken);

			if (go.TryGetComponent<TComponent>(out var component))
			{
				return component;
			}

			DisposeInstance(go);
			throw new InvalidOperationException($"Spawned object '{go.name}' does not have component '{typeof(TComponent).Name}'");
		}

		// --------------------------------------------------------------------------
		// SYNCHRONOUS API (Blocking - Mimics Resources.Load)
		// --------------------------------------------------------------------------

		/// <summary>
		/// Loads an asset synchronously (Blocks frame).
		/// Use this to replace Resources.Load().
		/// </summary>
		public static TObject LoadAsset<TObject>(string key)
		{
			return _strategy.LoadAsset<TObject>(key);
		}

		/// <summary>
		/// Loads an asset synchronously from Reference (Blocks frame).
		/// </summary>
		public static TObject LoadAsset<TObject>(AssetReference reference)
		{
			return _strategy.LoadAsset<TObject>(reference);
		}

		/// <summary>
		/// Loads an asset synchronously from Typed Reference (Blocks frame).
		/// </summary>
		public static TObject LoadAsset<TObject>(AssetReferenceT<TObject> reference) where TObject : Object
		{
			return _strategy.LoadAsset<TObject>(reference);
		}

		/// <summary>
		/// Spawns a GameObject synchronously (Blocks frame).
		/// </summary>
		public static GameObject Spawn(string key, Transform root = null)
		{
			return _strategy.Spawn(key, root);
		}

		/// <summary>
		/// Spawns a GameObject synchronously from Reference (Blocks frame).
		/// </summary>
		public static GameObject Spawn(AssetReference reference, Transform root = null)
		{
			return _strategy.Spawn(reference, root);
		}

		/// <summary>
		/// Spawns a GameObject synchronously and gets Component (Blocks frame).
		/// </summary>
		public static TComponent Spawn<TComponent>(AssetReference reference, Transform root = null) 
			where TComponent : Component
		{
			var go = _strategy.Spawn(reference, root);
			
			if (go.TryGetComponent<TComponent>(out var component))
			{
				return component;
			}

			DisposeInstance(go);
			throw new InvalidOperationException($"Spawned object '{go.name}' does not have component '{typeof(TComponent).Name}'");
		}

		// --------------------------------------------------------------------------
		// CLEANUP
		// --------------------------------------------------------------------------

		/// <summary>
		/// Release the operation and its associated resources.
		/// </summary>
		public static void DisposeAsset(Object uObject) => _strategy.DisposeAsset(uObject);

		/// <summary>
		/// Release the assets group and its associated resources.
		/// </summary>
		public static void DisposeAssetsGroup<T>(AssetsGroup<T> group) => _strategy.DisposeAssetsGroup(group);

		/// <summary>
		/// Releases and destroys an object that was created via Spawn.
		/// </summary>
		public static bool DisposeInstance(GameObject gObject) => _strategy.DisposeInstance(gObject);

		/// <summary>
		///   <para>Unloads assets that are not used.</para>
		/// </summary>
		public static UniTask RemoveUnusedResources(CancellationToken cToken = default)
		{
			return Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: cToken);
		}
	}
}