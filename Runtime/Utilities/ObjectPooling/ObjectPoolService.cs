using System.Collections.Generic;
using AK.Core;
using AK.Core.Collections;
using UnityEngine;

namespace AK.Utilities
{
	/// <summary>
	/// Default <see cref="IObjectPoolService"/>. POCO - register in Reflex as IObjectPoolService
	/// (see ExampleGameBindings for the pattern), or instantiate directly in tests.
	///
	/// Every checked-out instance lives in one <see cref="SlotMap{T}"/> and is addressed by the
	/// <see cref="Handle{T}"/> returned from <c>Lease</c>. Releasing frees the slot, so a handle
	/// kept past the release resolves to nothing instead of to whoever leased the object next.
	/// Get/Release are O(1) and allocate nothing once a pool has reached its working size.
	/// </summary>
	public class ObjectPoolService : IObjectPoolService
	{
		private sealed class Pool
		{
			public readonly PoolableObjectDefinition Definition;
			public readonly Stack<PooledInstance>    Resting;
			public int Created;
			public int Active;

			public Pool(PoolableObjectDefinition definition)
			{
				Definition = definition;
				Resting    = new Stack<PooledInstance>(Mathf.Max(definition.InitialPoolSize, 8));
			}

			public int Cap => Definition.MaxPoolSize > 0 ? Mathf.Max(Definition.MaxPoolSize, Definition.InitialPoolSize) : int.MaxValue;
		}

		private readonly Dictionary<PoolableObjectDefinition, Pool> _pools     = new();
		private readonly List<PoolableObjectDefinition>             _poolOrder = new();
		private readonly Dictionary<GameObject, PooledInstance>     _byObject  = new();
		private readonly SlotMap<PooledInstance>                    _leased    = new(64);

		private ObjectPoolRegistry _registry;
		private Transform          _poolRoot;

		private Transform PoolRoot
		{
			get
			{
				if (_poolRoot == null)
				{
					var go = new GameObject("[ObjectPools]");
					go.SetActive(false);
					if (Application.isPlaying) Object.DontDestroyOnLoad(go);
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

		// ---------------------------------------------------------------- setup

		public void RegisterPools(ObjectPoolRegistry registry)
		{
			if (registry == null)
			{
				Debug.LogWarning("[ObjectPoolService] RegisterPools called with a null registry.");
				return;
			}

			_registry = registry;

			IReadOnlyList<PoolableObjectDefinition> definitions = registry.Objects;
			for (int i = 0; i < definitions.Count; i++)
			{
				PoolableObjectDefinition definition = definitions[i];
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
			Pool pool = GetOrCreatePool(definition);
			if (pool == null) return;

			int target = Mathf.Min(definition.InitialPoolSize, pool.Cap);
			while (pool.Created < target)
			{
				pool.Resting.Push(CreateInstance(pool));
			}
		}

		// ---------------------------------------------------------------- lease

		public Handle<PooledInstance> Lease(PoolableObjectDefinition definition, Vector3 position = default, Quaternion rotation = default,
		                                    Transform parent = null)
		{
			Pool pool = GetOrCreatePool(definition);
			if (pool == null) return Handle<PooledInstance>.Invalid;

			PooledInstance instance;
			if (pool.Resting.Count > 0)
			{
				instance = pool.Resting.Pop();
			}
			else if (pool.Created < pool.Cap)
			{
				instance = CreateInstance(pool);
			}
			else
			{
				Debug.LogWarning($"[ObjectPoolService] Pool for '{definition.name}' is empty and at MaxPoolSize ({definition.MaxPoolSize}).");
				return Handle<PooledInstance>.Invalid;
			}

			pool.Active++;
			instance.Lease = _leased.Add(instance);

			Transform t = instance.Transform;
			t.SetParent(parent, false);
			t.SetPositionAndRotation(position, rotation);
			instance.GameObject.SetActive(true);

			IPoolable[] poolables = instance.Poolables;
			for (int i = 0; i < poolables.Length; i++)
			{
				if (poolables[i] is PoolableObject po) po.IsInPool = false;
				poolables[i].OnGetFromPool();
			}

			return instance.Lease;
		}

		public Handle<PooledInstance> Lease(Uid<PoolableObjectDefinition> definitionId = default, Vector3 position = default, Quaternion rotation = default,
		                                    Transform parent = null)
		{
			PoolableObjectDefinition definition = ResolveDefinition(definitionId);
			return definition == null ? Handle<PooledInstance>.Invalid : Lease(definition, position, rotation, parent);
		}

		public bool TryGet(Handle<PooledInstance> lease, out GameObject instance)
		{
			if (_leased.TryGet(lease, out PooledInstance pooled))
			{
				instance = pooled.GameObject;
				return true;
			}

			instance = null;
			return false;
		}

		public bool TryGet<T>(Handle<PooledInstance> lease, out T component) where T : Component
		{
			if (_leased.TryGet(lease, out PooledInstance pooled))
			{
				component = pooled.GameObject.GetComponent<T>();
				return component != null;
			}

			component = null;
			return false;
		}

		public bool IsLeased(Handle<PooledInstance> lease) => _leased.Contains(lease);

		public bool Release(Handle<PooledInstance> lease)
		{
			if (!_leased.Remove(lease, out PooledInstance instance)) return false;

			ReturnToPool(instance);
			return true;
		}

		// ---------------------------------------------------------------- reference API

		public GameObject Get(PoolableObjectDefinition definition, Vector3 position = default, Quaternion rotation = default,
		                      Transform parent = null)
		{
			Handle<PooledInstance> lease = Lease(definition, position, rotation, parent);
			return _leased.TryGet(lease, out PooledInstance instance) ? instance.GameObject : null;
		}

		public T Get<T>(PoolableObjectDefinition definition, Vector3 position = default, Quaternion rotation = default,
		                Transform parent = null) where T : Component
		{
			GameObject instance = Get(definition, position, rotation, parent);
			return instance == null ? null : instance.GetComponent<T>();
		}

		public GameObject Get(Uid<PoolableObjectDefinition> definitionId = default, Vector3 position = default, Quaternion rotation = default,
		                      Transform parent = null)
		{
			PoolableObjectDefinition definition = ResolveDefinition(definitionId);
			return definition == null ? null : Get(definition, position, rotation, parent);
		}

		public T Get<T>(Uid<PoolableObjectDefinition> definitionId = default, Vector3 position = default, Quaternion rotation = default,
		                Transform parent = null) where T : Component
		{
			GameObject instance = Get(definitionId, position, rotation, parent);
			return instance == null ? null : instance.GetComponent<T>();
		}

		public void Release(GameObject instance)
		{
			if (instance == null) return;

			if (!_byObject.TryGetValue(instance, out PooledInstance pooled))
			{
				Debug.LogWarning($"[ObjectPoolService] '{instance.name}' is not tracked by any pool - destroying it.");
				DestroyInstance(instance);
				return;
			}

			if (!_leased.Remove(pooled.Lease))
			{
				Debug.LogWarning($"[ObjectPoolService] '{instance.name}' released twice.", instance);
				return;
			}

			ReturnToPool(pooled);
		}

		// ---------------------------------------------------------------- stats

		public int ActiveCount(PoolableObjectDefinition definition)
		{
			return _pools.TryGetValue(definition, out Pool pool) ? pool.Active : 0;
		}

		public int InactiveCount(PoolableObjectDefinition definition)
		{
			return _pools.TryGetValue(definition, out Pool pool) ? pool.Resting.Count : 0;
		}

		public int LeasedCount => _leased.Count;

		// ---------------------------------------------------------------- teardown

		public void Clear(PoolableObjectDefinition definition = null)
		{
			if (definition != null)
			{
				if (_pools.TryGetValue(definition, out Pool pool))
				{
					DestroyPool(pool);
					_pools.Remove(definition);
					_poolOrder.Remove(definition);
				}

				return;
			}

			foreach (Pool pool in _pools.Values)
			{
				DestroyPool(pool);
			}

			_pools.Clear();
			_poolOrder.Clear();
			_byObject.Clear();
			_leased.Clear();
		}

		// ---------------------------------------------------------------- internals

		private Pool GetOrCreatePool(PoolableObjectDefinition definition)
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

			if (_pools.TryGetValue(definition, out Pool pool))
			{
				return pool;
			}

			pool = new Pool(definition);
			_pools.Add(definition, pool);
			_poolOrder.Add(definition);
			return pool;
		}

		private PooledInstance CreateInstance(Pool pool)
		{
			GameObject go = Object.Instantiate(pool.Definition.Prefab, PoolRoot);
			go.name = pool.Definition.Prefab.name;

			var instance = new PooledInstance(go, go.GetComponents<IPoolable>(), pool.Definition);

			for (int i = 0; i < instance.Poolables.Length; i++)
			{
				if (instance.Poolables[i] is PoolableObject po)
				{
					po.SetReturnAction(ReleasePoolable);
					po.IsInPool = true;
				}
			}

			_byObject.Add(go, instance);
			pool.Created++;
			return instance;
		}

		private void ReleasePoolable(PoolableObject poolable) => Release(poolable.gameObject);

		private void ReturnToPool(PooledInstance instance)
		{
			instance.Lease = Handle<PooledInstance>.Invalid;

			IPoolable[] poolables = instance.Poolables;
			for (int i = 0; i < poolables.Length; i++)
			{
				if (poolables[i] is PoolableObject po) po.IsInPool = true;
				poolables[i].OnReturnToPool();
			}

			if (instance.GameObject != null)
			{
				instance.Transform.SetParent(PoolRoot, false);
				instance.GameObject.SetActive(false);
			}

			if (_pools.TryGetValue(instance.Definition, out Pool pool))
			{
				pool.Active--;
				if (instance.GameObject != null) pool.Resting.Push(instance);
				else pool.Created--;
			}
		}

		private void DestroyPool(Pool pool)
		{
			while (pool.Resting.Count > 0)
			{
				PooledInstance resting = pool.Resting.Pop();
				_byObject.Remove(resting.GameObject);
				if (resting.GameObject != null) DestroyInstance(resting.GameObject);
			}

			SlotMap<PooledInstance>.Enumerator leased = _leased.GetEnumerator();
			while (leased.MoveNext())
			{
				PooledInstance instance = leased.Current;
				if (instance.Definition != pool.Definition) continue;

				_leased.Remove(leased.CurrentHandle);
				_byObject.Remove(instance.GameObject);
				if (instance.GameObject != null) DestroyInstance(instance.GameObject);
			}

			pool.Created = 0;
			pool.Active  = 0;
		}

		private static void DestroyInstance(GameObject go)
		{
			if (Application.isPlaying) Object.Destroy(go);
			else Object.DestroyImmediate(go);
		}

		private PoolableObjectDefinition ResolveDefinition(Uid<PoolableObjectDefinition> definitionId)
		{
			if (definitionId.IsSet)
			{
				if (_registry == null)
				{
					Debug.LogError("[ObjectPoolService] Identity lookup requires a registry - call RegisterPools() first.");
					return null;
				}

				if (!_registry.TryResolve(definitionId, out PoolableObjectDefinition definition))
				{
					Debug.LogError($"[ObjectPoolService] No pool definition for {UidDebugNames.Describe(definitionId)}.");
				}

				return definition;
			}

			if (_poolOrder.Count > 0) return _poolOrder[0];

			Debug.LogWarning("[ObjectPoolService] No pools registered yet.");
			return null;
		}
	}
}
