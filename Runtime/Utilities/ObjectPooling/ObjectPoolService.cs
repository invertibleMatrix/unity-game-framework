using System;
using System.Collections.Generic;
using AK.Core;
using AK.Core.Extensions;
using AK.Kernel.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Utilities
{
	/// <summary>
	/// Default <see cref="IObjectPoolService"/>. A plain class: bind it in the container as
	/// IObjectPoolService, or construct it directly in tests.
	///
	/// Every checked-out instance lives in one <see cref="SlotMap{T}"/> and is addressed by the
	/// <see cref="Handle{T}"/> returned from <c>Lease</c>. Releasing frees the slot, so a handle
	/// kept past the release resolves to nothing instead of to whoever leased the object next.
	///
	/// An instance destroyed outside the pool is written off when the service next meets it:
	/// a resting one when it comes up for a lease, a leased one when its lease is resolved or
	/// released, and any of them when a pool's counts are read or a full pool is about to
	/// refuse a lease. Its lease ends and its place under the pool's cap is free again; its
	/// callbacks don't run. Lease, TryGet and Release are O(1) and allocate nothing once a pool
	/// has reached its working size. Main thread only.
	/// </summary>
	public class ObjectPoolService : IObjectPoolService
	{
		private sealed class Pool
		{
			public readonly PoolableObjectDefinition Definition;

			// Used as a stack: the last instance returned is the next one leased.
			public readonly List<PooledInstance> Resting;

			// Instances the pool made and hasn't lost, leased and resting: they count against the cap.
			public int Instances;

			// Instances leased.
			public int Active;

			public Pool(PoolableObjectDefinition definition)
			{
				Definition = definition;
				Resting    = new List<PooledInstance>(Mathf.Max(definition.InitialPoolSize, 8));
			}

			public int Cap => Definition.MaxPoolSize > 0 ? Mathf.Max(Definition.MaxPoolSize, Definition.InitialPoolSize) : int.MaxValue;
		}

		private readonly Dictionary<PoolableObjectDefinition, Pool> _pools    = new();
		private readonly Dictionary<GameObject, PooledInstance>     _byObject = new();
		private readonly SlotMap<PooledInstance>                    _leased   = new(64);
		private readonly Action<PoolableObject>                     _releasePoolable;

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
			_releasePoolable = poolable => Release(poolable.gameObject);

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

			ReclaimDestroyedLeases();
			PurgeDestroyedResting(pool);

			int target = Mathf.Min(definition.InitialPoolSize, pool.Cap);
			while (pool.Instances < target)
			{
				pool.Resting.Add(CreateInstance(pool));
			}
		}

		// ---------------------------------------------------------------- lease

		public Handle<PooledInstance> Lease(PoolableObjectDefinition definition, Transform parent = null)
		{
			Pool pool = GetOrCreatePool(definition);
			PooledInstance instance = pool != null ? Take(pool) : null;
			if (instance == null) return Handle<PooledInstance>.Invalid;

			Place(pool, instance, parent);
			return CheckOut(pool, instance);
		}

		public Handle<PooledInstance> Lease(PoolableObjectDefinition definition, Vector3 position, Quaternion rotation,
		                                    Transform parent = null)
		{
			Pool pool = GetOrCreatePool(definition);
			PooledInstance instance = pool != null ? Take(pool) : null;
			if (instance == null) return Handle<PooledInstance>.Invalid;

			Place(pool, instance, parent);
			instance.Transform.SetPositionAndRotation(position, rotation);
			return CheckOut(pool, instance);
		}

		public Handle<PooledInstance> Lease(Uid<PoolableObjectDefinition> definitionId, Transform parent = null)
		{
			PoolableObjectDefinition definition = ResolveDefinition(definitionId);
			return definition == null ? Handle<PooledInstance>.Invalid : Lease(definition, parent);
		}

		public Handle<PooledInstance> Lease(Uid<PoolableObjectDefinition> definitionId, Vector3 position, Quaternion rotation,
		                                    Transform parent = null)
		{
			PoolableObjectDefinition definition = ResolveDefinition(definitionId);
			return definition == null ? Handle<PooledInstance>.Invalid : Lease(definition, position, rotation, parent);
		}

		public bool TryGet(Handle<PooledInstance> lease, out GameObject instance)
		{
			if (TryGetLive(lease, out PooledInstance pooled))
			{
				instance = pooled.GameObject;
				return true;
			}

			instance = null;
			return false;
		}

		public bool TryGet<T>(Handle<PooledInstance> lease, out T component) where T : Component
		{
			if (TryGetLive(lease, out PooledInstance pooled))
			{
				return pooled.GameObject.TryGetComponent(out component);
			}

			component = null;
			return false;
		}

		public bool IsLeased(Handle<PooledInstance> lease) => TryGetLive(lease, out _);

		public bool Release(Handle<PooledInstance> lease)
		{
			if (!_leased.Remove(lease, out PooledInstance instance)) return false;

			if (instance.GameObject == null)
			{
				// Destroyed while leased: its lease had ended already.
				WriteOff(instance, wasLeased: true);
				return false;
			}

			ReturnToPool(instance);
			return true;
		}

		// ---------------------------------------------------------------- reference API

		public GameObject Get(PoolableObjectDefinition definition, Transform parent = null)
		{
			return InstanceOf(Lease(definition, parent));
		}

		public GameObject Get(PoolableObjectDefinition definition, Vector3 position, Quaternion rotation, Transform parent = null)
		{
			return InstanceOf(Lease(definition, position, rotation, parent));
		}

		public T Get<T>(PoolableObjectDefinition definition, Transform parent = null) where T : Component
		{
			return ComponentOf<T>(Get(definition, parent));
		}

		public T Get<T>(PoolableObjectDefinition definition, Vector3 position, Quaternion rotation, Transform parent = null)
			where T : Component
		{
			return ComponentOf<T>(Get(definition, position, rotation, parent));
		}

		public GameObject Get(Uid<PoolableObjectDefinition> definitionId, Transform parent = null)
		{
			return InstanceOf(Lease(definitionId, parent));
		}

		public GameObject Get(Uid<PoolableObjectDefinition> definitionId, Vector3 position, Quaternion rotation, Transform parent = null)
		{
			return InstanceOf(Lease(definitionId, position, rotation, parent));
		}

		public T Get<T>(Uid<PoolableObjectDefinition> definitionId, Transform parent = null) where T : Component
		{
			return ComponentOf<T>(Get(definitionId, parent));
		}

		public T Get<T>(Uid<PoolableObjectDefinition> definitionId, Vector3 position, Quaternion rotation, Transform parent = null)
			where T : Component
		{
			return ComponentOf<T>(Get(definitionId, position, rotation, parent));
		}

		public void Release(GameObject instance)
		{
			if (instance is null) return;

			if (!_byObject.TryGetValue(instance, out PooledInstance pooled))
			{
				// Not this service's to take back; a destroyed one has nothing left to take.
				if (instance != null)
				{
					Debug.LogWarning($"[ObjectPoolService] '{instance.name}' was not made by this service; left as it is.", instance);
				}

				return;
			}

			bool leased = _leased.Remove(pooled.Lease);

			if (instance == null)
			{
				// Destroyed outside the pool. A resting one is written off when it comes up.
				if (leased) WriteOff(pooled, wasLeased: true);
				return;
			}

			if (!leased)
			{
				Debug.LogWarning($"[ObjectPoolService] '{instance.name}' released twice.", instance);
				return;
			}

			ReturnToPool(pooled);
		}

		// ---------------------------------------------------------------- stats

		public int ActiveCount(PoolableObjectDefinition definition)
		{
			if (definition is null || !_pools.TryGetValue(definition, out Pool pool)) return 0;

			ReclaimDestroyedLeases();
			return pool.Active;
		}

		public int InactiveCount(PoolableObjectDefinition definition)
		{
			if (definition is null || !_pools.TryGetValue(definition, out Pool pool)) return 0;

			PurgeDestroyedResting(pool);
			return pool.Resting.Count;
		}

		/// <summary>Leases out across every pool.</summary>
		public int LeasedCount
		{
			get
			{
				ReclaimDestroyedLeases();
				return _leased.Count;
			}
		}

		// ---------------------------------------------------------------- teardown

		public void Clear(PoolableObjectDefinition definition = null)
		{
			if (definition is not null)
			{
				if (_pools.TryGetValue(definition, out Pool pool))
				{
					DestroyPool(pool);
					_pools.Remove(definition);
				}

				return;
			}

			foreach (Pool pool in _pools.Values)
			{
				DestroyPool(pool);
			}

			_pools.Clear();
			_byObject.Clear();
			_leased.Clear();
		}

		// ---------------------------------------------------------------- internals

		private Pool GetOrCreatePool(PoolableObjectDefinition definition)
		{
			if (definition == null)
			{
				Debug.LogError("[ObjectPoolService] Lease/Prewarm called with a null definition.");
				return null;
			}

			if (definition.Prefab == null)
			{
				Debug.LogError($"[ObjectPoolService] Definition '{definition.name}' has no prefab assigned.", definition);
				return null;
			}

			if (_pools.TryGetValue(definition, out Pool pool))
			{
				return pool;
			}

			pool = new Pool(definition);
			_pools.Add(definition, pool);
			return pool;
		}

		/// <summary>A resting instance, or a new one under the cap; null, logged, when the pool is empty and full.</summary>
		private PooledInstance Take(Pool pool)
		{
			PooledInstance instance = PopResting(pool);
			if (instance != null) return instance;

			if (pool.Instances >= pool.Cap)
			{
				// Instances destroyed while leased may still hold places under the cap.
				ReclaimDestroyedLeases();

				if (pool.Instances >= pool.Cap)
				{
					Debug.LogWarning($"[ObjectPoolService] Pool for '{pool.Definition.name}' is empty and at MaxPoolSize ({pool.Definition.MaxPoolSize}).");
					return null;
				}
			}

			return CreateInstance(pool);
		}

		private PooledInstance PopResting(Pool pool)
		{
			List<PooledInstance> resting = pool.Resting;
			while (resting.Count > 0)
			{
				int last = resting.Count - 1;
				PooledInstance instance = resting[last];
				resting.RemoveAt(last);

				if (instance.GameObject != null) return instance;

				WriteOff(instance, wasLeased: false);
			}

			return null;
		}

		/// <summary>Parents the instance and gives it its prefab's local pose, as Instantiate gives a new one.</summary>
		private static void Place(Pool pool, PooledInstance instance, Transform parent)
		{
			Transform t = instance.Transform;
			t.SetParent(parent, false);
			t.MatchLocalPose(pool.Definition.Prefab.transform);
		}

		private Handle<PooledInstance> CheckOut(Pool pool, PooledInstance instance)
		{
			pool.Active++;
			instance.Lease = _leased.Add(instance);
			instance.GameObject.SetActive(true);

			IPoolable[] poolables = instance.Poolables;
			for (int i = 0; i < poolables.Length; i++)
			{
				if (poolables[i] is PoolableObject po) po.IsInPool = false;
				poolables[i].OnGetFromPool();
			}

			return instance.Lease;
		}

		private GameObject InstanceOf(Handle<PooledInstance> lease)
		{
			return _leased.TryGet(lease, out PooledInstance instance) ? instance.GameObject : null;
		}

		/// <summary>The instance's <typeparamref name="T"/>. An instance without one goes straight back, logged.</summary>
		private T ComponentOf<T>(GameObject instance) where T : Component
		{
			if (instance == null) return null;
			if (instance.TryGetComponent(out T component)) return component;

			Debug.LogError($"[ObjectPoolService] '{instance.name}' has no {typeof(T).Name}; it went back to its pool.", instance);
			Release(instance);
			return null;
		}

		/// <summary>Resolves a lease to its instance. A destroyed instance is written off, and its lease ends.</summary>
		private bool TryGetLive(Handle<PooledInstance> lease, out PooledInstance instance)
		{
			if (!_leased.TryGet(lease, out instance)) return false;
			if (instance.GameObject != null) return true;

			_leased.Remove(lease);
			WriteOff(instance, wasLeased: true);
			instance = null;
			return false;
		}

		private PooledInstance CreateInstance(Pool pool)
		{
			GameObject prefab = pool.Definition.Prefab;
			GameObject go = Object.Instantiate(prefab, PoolRoot);
			go.name = prefab.name;

			var instance = new PooledInstance(go, go.GetComponents<IPoolable>(), pool.Definition);

			for (int i = 0; i < instance.Poolables.Length; i++)
			{
				if (instance.Poolables[i] is PoolableObject po)
				{
					po.SetReturnAction(_releasePoolable);
					po.IsInPool = true;
				}
			}

			_byObject.Add(go, instance);
			pool.Instances++;
			return instance;
		}

		private void ReturnToPool(PooledInstance instance)
		{
			instance.Lease = Handle<PooledInstance>.Invalid;

			IPoolable[] poolables = instance.Poolables;
			for (int i = 0; i < poolables.Length; i++)
			{
				if (poolables[i] is PoolableObject po) po.IsInPool = true;
				poolables[i].OnReturnToPool();
			}

			// A callback may have destroyed it.
			if (instance.GameObject == null)
			{
				WriteOff(instance, wasLeased: true);
				return;
			}

			instance.Transform.SetParent(PoolRoot, false);
			instance.GameObject.SetActive(false);

			if (_pools.TryGetValue(instance.Definition, out Pool pool))
			{
				pool.Active--;
				pool.Resting.Add(instance);
			}
		}

		/// <summary>Forgets an instance destroyed outside the pool, whose lease, if it had one, is already removed.</summary>
		private void WriteOff(PooledInstance instance, bool wasLeased)
		{
			instance.Lease = Handle<PooledInstance>.Invalid;
			_byObject.Remove(instance.GameObject);

			if (_pools.TryGetValue(instance.Definition, out Pool pool))
			{
				pool.Instances--;
				if (wasLeased) pool.Active--;
			}
		}

		/// <summary>Ends the leases of instances destroyed while leased. Walks every lease, so only counts and full pools call it.</summary>
		private void ReclaimDestroyedLeases()
		{
			SlotMap<PooledInstance>.Enumerator leased = _leased.GetEnumerator();
			while (leased.MoveNext())
			{
				PooledInstance instance = leased.Current;
				if (instance.GameObject != null) continue;

				_leased.Remove(leased.CurrentHandle);
				WriteOff(instance, wasLeased: true);
			}
		}

		private void PurgeDestroyedResting(Pool pool)
		{
			List<PooledInstance> resting = pool.Resting;
			int kept = 0;
			for (int i = 0; i < resting.Count; i++)
			{
				PooledInstance instance = resting[i];
				if (instance.GameObject == null)
				{
					WriteOff(instance, wasLeased: false);
					continue;
				}

				resting[kept++] = instance;
			}

			resting.RemoveRange(kept, resting.Count - kept);
		}

		private void DestroyPool(Pool pool)
		{
			List<PooledInstance> resting = pool.Resting;
			for (int i = 0; i < resting.Count; i++)
			{
				_byObject.Remove(resting[i].GameObject);
				resting[i].GameObject.DestroyInAnyMode();
			}

			resting.Clear();

			SlotMap<PooledInstance>.Enumerator leased = _leased.GetEnumerator();
			while (leased.MoveNext())
			{
				PooledInstance instance = leased.Current;
				if (!ReferenceEquals(instance.Definition, pool.Definition)) continue;

				_leased.Remove(leased.CurrentHandle);
				instance.Lease = Handle<PooledInstance>.Invalid;
				_byObject.Remove(instance.GameObject);
				instance.GameObject.DestroyInAnyMode();
			}

			pool.Instances = 0;
			pool.Active    = 0;
		}

		private PoolableObjectDefinition ResolveDefinition(Uid<PoolableObjectDefinition> definitionId)
		{
			if (!definitionId.IsSet)
			{
				Debug.LogError("[ObjectPoolService] No pool definition id given: pass the id of a registered definition.");
				return null;
			}

			if (_registry == null)
			{
				Debug.LogError("[ObjectPoolService] Identity lookup requires a registry - call RegisterPools() first.");
				return null;
			}

			if (!_registry.TryResolve(definitionId, out PoolableObjectDefinition definition))
			{
				Debug.LogError($"[ObjectPoolService] No pool definition for {UidDebugNames.Describe(definitionId)}.");
				return null;
			}

			return definition;
		}
	}
}
