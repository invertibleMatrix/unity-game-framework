using System.Collections.Generic;
using AK.Core;
using UnityEngine;

namespace AK.CoreDomain
{
	/// <summary>
	/// Bootstrap-scene asset holding every Meta container and, through them, every domain
	/// registry. Untyped resolution walks the registered registries — the aggregate is a
	/// view over the typed registries, so it cannot drift from them.
	///
	/// The order of operations at startup matters: bindings register metas, then call
	/// InitializeRegistries, which initializes each meta (they register their registries
	/// here) and finally hands the redirect table to every registry it collected.
	/// </summary>
	[CreateAssetMenu(fileName = "MetaDataRepository", menuName = "AK/MetaData/MetaDataRepository")]
	public class MetaDataRepository : ScriptableObject, IMetaDataRepository
	{
		[SerializeField, Tooltip("Optional. Human-authored identity replacements consulted on every registry miss.")]
		private UidRedirectTable _redirects;

		[SerializeField, Tooltip("Registries with no owning Meta (audio, particles, cameras, pools) that should still resolve through this repository.")]
		private List<UidRegistryAssetBase> _standaloneRegistries = new();

		private readonly Dictionary<System.Type, IMeta>  _metas      = new();
		private readonly List<UidRegistryAssetBase>      _registries = new();
		private readonly Dictionary<Uid, UID>            _cache      = new();

		public UidRedirectTable Redirects => _redirects;

		// ---------------------------------------------------------------- metas

		public void RegisterMeta<T>(T meta) where T : class, IMeta
		{
			if (meta == null) return;
			_metas[typeof(T)] = meta;
		}

		public T GetMeta<T>() where T : class, IMeta
		{
			return _metas.TryGetValue(typeof(T), out IMeta meta) ? meta as T : null;
		}

		public bool TryGetMeta<T>(out T meta) where T : class, IMeta
		{
			if (_metas.TryGetValue(typeof(T), out IMeta m))
			{
				meta = m as T;
				return meta != null;
			}

			meta = null;
			return false;
		}

		// ---------------------------------------------------------------- registries

		public void RegisterRegistry(UidRegistryAssetBase registry)
		{
			if (registry == null || _registries.Contains(registry)) return;

			_registries.Add(registry);
			registry.SetRedirects(_redirects);
			_cache.Clear();
		}

		public void InitializeRegistries()
		{
			_registries.Clear();
			_cache.Clear();

			foreach (UidRegistryAssetBase standalone in _standaloneRegistries)
			{
				RegisterRegistry(standalone);
			}

			foreach (KeyValuePair<System.Type, IMeta> kvp in _metas)
			{
				kvp.Value.InitializeMeta();

				if (kvp.Value is IMetaWithRegistry withRegistry)
				{
					RegisterRegistry(withRegistry.RegistryAsset);
				}
			}

			foreach (UidRegistryAssetBase registry in _registries)
			{
				foreach (UID tracked in registry.GetTrackedObjects())
				{
					UidDebugNames.Register(tracked);
				}
			}
		}

		// ---------------------------------------------------------------- resolve

		public bool TryResolve(Uid id, out UID asset)
		{
			if (id.IsNone)
			{
				asset = null;
				return false;
			}

			if (_cache.TryGetValue(id, out asset))
			{
				return asset != null;
			}

			for (int i = 0; i < _registries.Count; i++)
			{
				if (_registries[i].TryResolveUntyped(id, out asset))
				{
					_cache[id] = asset;
					return true;
				}
			}

			asset = null;
			return false;
		}

		public bool TryResolve<T>(Uid id, out T asset) where T : UID
		{
			if (TryResolve(id, out UID untyped) && untyped is T typed)
			{
				asset = typed;
				return true;
			}

			asset = null;
			return false;
		}

		public bool TryResolve<T>(Uid<T> id, out T asset) where T : UID => TryResolve(id.Value, out asset);
	}
}
