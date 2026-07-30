using System.Collections.Generic;
using AK.Core;
using UnityEngine;
using GameObjectPool = UnityEngine.Pool.ObjectPool<UnityEngine.GameObject>; // disambiguates AK.Core.ObjectPool<T>

namespace AK.Utilities
{
	/// <summary>
	/// Default <see cref="IObjectPoolService"/>. POCO - register in Reflex as IObjectPoolService
	/// (see ExampleGameBindings for the pattern), or instantiate directly in tests.
	/// Backed by UnityEngine.Pool.ObjectPool per definition.
	/// </summary>
	public class ObjectPoolService : IObjectPoolService
	{
		private sealed class PoolEntry
		{
			public PoolableObjectDefinition Definition;
			public GameObjectPool Pool;
			public readonly HashSet<GameObject> Active = new();
		}

		private readonly Dictionary<PoolableObjectDefinition, PoolEntry> _pools = new();

		// Insertion-ordered definitions backing the null-UID "first pool" fallback.
		private readonly List<PoolableObjectDefinition> _poolOrder = new();

		private ObjectPoolRegistry _registry;
		private Transform _poolRoot;

		// Cache of IPoolable components per created instance (GetComponents allocates).
		private readonly Dictionary<GameObject, IPoolable[]> _poolableCache = new();

		private Transform PoolRoot
		{
			get
			{
				if (_poolRoot == null)
				{
					var go = new GameObject("[ObjectPools]");
					go.SetActive(false); // pooled instances inherit inactivity
					Object.DontDestroyOnLoad(go);
					_poolRoot = go.transform;
				}

				return _poolRoot;
			}
		}

		public ObjectPoolService(ObjectPoolRegistry registry = null)
		{
			if (registry != null)
			{
				RegisterPools(registry);
			}
		}

		public void RegisterPools(ObjectPoolRegistry registry)
		{
			if (registry == null)
			{
				Debug.LogWarning("[ObjectPoolService] RegisterPools called with a null registry.");
				return;
			}

			registry.Initialize();
			_registry = registry;

			foreach (var definition in registry.GetAllObjects())
			{
				if (definition == null || definition.Prefab == null) continue;

				if (definition.PrewarmOnRegister)
				{
					Prewarm(definition);
				}
				else
				{
					GetOrCreatePool(definition);
				}
			}
		}

		public void Prewarm(PoolableObjectDefinition definition)
		{
			var entry = GetOrCreatePool(definition);
			if (entry == null) return;

			var warmed = new List<GameObject>(definition.InitialPoolSize);
			for (var i = 0; i < definition.InitialPoolSize; i++)
			{
				var instance = entry.Pool.Get();
				if (instance == null) break; // pool already at cap
				warmed.Add(instance);
			}

			foreach (var instance in warmed)
			{
				entry.Pool.Release(instance);
			}
		}

		public GameObject Get(PoolableObjectDefinition definition, Vector3 position = default, Quaternion rotation = default,
		                      Transform parent = null)
		{
			var entry = GetOrCreatePool(definition);
			if (entry == null) return null;

			var instance = entry.Pool.Get();
			if (instance == null)
			{
				// ObjectPool.Get returns null when at MaxPoolSize and empty.
				Debug.LogWarning($"[ObjectPoolService] Pool for '{definition.name}' is empty and at MaxPoolSize ({definition.MaxPoolSize}).");
				return null;
			}

			entry.Active.Add(instance);

			var t = instance.transform;
			t.SetParent(parent, false);
			t.SetPositionAndRotation(position, rotation);

			return instance;
		}

		public T Get<T>(PoolableObjectDefinition definition, Vector3 position = default, Quaternion rotation = default,
		                Transform parent = null) where T : Component
		{
			var instance = Get(definition, position, rotation, parent);
			return instance == null ? null : instance.GetComponent<T>();
		}

		public GameObject Get(UID definitionUID = null, Vector3 position = default, Quaternion rotation = default,
		                      Transform parent = null)
		{
			var definition = ResolveDefinition(definitionUID);
			return definition == null ? null : Get(definition, position, rotation, parent);
		}

		public T Get<T>(UID definitionUID = null, Vector3 position = default, Quaternion rotation = default,
		                Transform parent = null) where T : Component
		{
			var instance = Get(definitionUID, position, rotation, parent);
			return instance == null ? null : instance.GetComponent<T>();
		}

		public void Release(GameObject instance)
		{
			if (instance == null) return;

			foreach (var entry in _pools.Values)
			{
				if (!entry.Active.Remove(instance)) continue;

				entry.Pool.Release(instance);
				return;
			}

			Debug.LogWarning($"[ObjectPoolService] '{instance.name}' is not tracked by any pool (double release?) - destroying it.");
			Object.Destroy(instance);
		}

		public int ActiveCount(PoolableObjectDefinition definition)
		{
			return _pools.TryGetValue(definition, out var entry) ? entry.Active.Count : 0;
		}

		public int InactiveCount(PoolableObjectDefinition definition)
		{
			return _pools.TryGetValue(definition, out var entry) ? entry.Pool.CountInactive : 0;
		}

		public void Clear(PoolableObjectDefinition definition = null)
		{
			if (definition != null)
			{
				if (_pools.TryGetValue(definition, out var entry))
				{
					entry.Pool.Clear();
					entry.Pool.Dispose();
					_pools.Remove(definition);
					_poolOrder.Remove(definition);
				}

				return;
			}

			foreach (var entry in _pools.Values)
			{
				entry.Pool.Clear();
				entry.Pool.Dispose();
			}

			_pools.Clear();
			_poolOrder.Clear();
		}

		// -----------------------------------------------------------------

		private PoolEntry GetOrCreatePool(PoolableObjectDefinition definition)
		{
			if (definition == null)
			{
				Debug.LogError("[ObjectPoolService] Get/Prewarm called with a null definition.");
				return null;
			}

			if (definition.Prefab == null)
			{
				Debug.LogError($"[ObjectPoolService] Definition '{definition.name}' has no prefab assigned.");
				return null;
			}

			if (_pools.TryGetValue(definition, out var entry))
			{
				return entry;
			}

			entry = new PoolEntry { Definition = definition };

#if UNITY_EDITOR
			var collectionCheck = true;
#else
			var collectionCheck = false;
#endif

			entry.Pool = new GameObjectPool(
				createFunc: () => CreateInstance(definition),
				actionOnGet: OnGet,
				actionOnRelease: OnRelease,
				actionOnDestroy: inst => { if (inst != null) Object.Destroy(inst); },
				collectionCheck: collectionCheck,
				defaultCapacity: Mathf.Max(definition.InitialPoolSize, 8),
				maxSize: definition.MaxPoolSize > 0 ? Mathf.Max(definition.MaxPoolSize, definition.InitialPoolSize) : int.MaxValue);

			_pools.Add(definition, entry);
			_poolOrder.Add(definition);
			return entry;
		}

		private GameObject CreateInstance(PoolableObjectDefinition definition)
		{
			var instance = Object.Instantiate(definition.Prefab, PoolRoot);
			instance.name = definition.Prefab.name;

			_poolableCache[instance] = instance.GetComponents<IPoolable>();

			if (instance.GetComponent<PoolableObject>() is { } poolable)
			{
				poolable.SetReturnAction(po => Release(po.gameObject));
				poolable.IsInPool = false;
			}

			return instance;
		}

		private void OnGet(GameObject instance)
		{
			if (instance == null) return;

			instance.SetActive(true);

			if (_poolableCache.TryGetValue(instance, out var poolables))
			{
				foreach (var p in poolables)
				{
					if (p is PoolableObject po) po.IsInPool = false;
					p.OnGetFromPool();
				}
			}
		}

		private void OnRelease(GameObject instance)
		{
			if (instance == null) return;

			if (_poolableCache.TryGetValue(instance, out var poolables))
			{
				foreach (var p in poolables)
				{
					if (p is PoolableObject po) po.IsInPool = true;
					p.OnReturnToPool();
				}
			}

			instance.transform.SetParent(PoolRoot, false);
			instance.SetActive(false);
		}

		private PoolableObjectDefinition ResolveDefinition(UID definitionUID)
		{
			if (definitionUID != null && !definitionUID.IsEmpty())
			{
				if (_registry == null)
				{
					Debug.LogError("[ObjectPoolService] UID lookup requires a registry - call RegisterPools() first.");
					return null;
				}

				return _registry.GetObjectByUID(definitionUID);
			}

			// Null/empty UID: first registered pool (consistent with CameraSystem's fallback).
			if (_poolOrder.Count > 0) return _poolOrder[0];

			Debug.LogWarning("[ObjectPoolService] No pools registered yet.");
			return null;
		}
	}
}
