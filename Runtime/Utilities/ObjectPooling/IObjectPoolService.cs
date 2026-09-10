using System.Collections.Generic;
using AK.Core;
using AK.Core.Collections;
using UnityEngine;

namespace AK.Utilities
{
	/// <summary>
	/// Game-wide GameObject pooling service. Data-driven via <see cref="PoolableObjectDefinition"/>
	/// assets registered in an <see cref="ObjectPoolRegistry"/>, instantly usable from code by
	/// passing a definition directly.
	///
	/// Two ways to hold a checked-out object:
	/// <code>
	/// // Reference API — simplest; the caller owns a GameObject and must not touch it after release.
	/// var bullet = _poolService.Get&lt;Bullet&gt;(bulletDefinition, muzzle.position, muzzle.rotation);
	/// bullet.ReturnToPool(); // or _poolService.Release(bullet.gameObject)
	///
	/// // Lease API — for anything that outlives a frame (timers, AI targets, VFX bookkeeping).
	/// // A stale lease resolves to nothing instead of to whoever got the object next.
	/// Handle&lt;PooledInstance&gt; lease = _poolService.Lease(bulletDefinition, muzzle.position, muzzle.rotation);
	/// if (_poolService.TryGet(lease, out Bullet b)) b.Fire();
	/// _poolService.Release(lease); // false if it was already released
	/// </code>
	/// </summary>
	public interface IObjectPoolService
	{
		/// <summary>
		/// Checks an instance out and returns a generational lease for it. Invalid when the pool
		/// is at MaxPoolSize and empty. Never throws on a bad definition; logs and returns Invalid.
		/// </summary>
		Handle<PooledInstance> Lease(PoolableObjectDefinition definition, Vector3 position = default, Quaternion rotation = default,
		                             Transform parent = null);

		/// <summary>Identity variant of <see cref="Lease(PoolableObjectDefinition, Vector3, Quaternion, Transform)"/>.</summary>
		Handle<PooledInstance> Lease(Uid<PoolableObjectDefinition> definitionId = default, Vector3 position = default, Quaternion rotation = default,
		                             Transform parent = null);

		/// <summary>Resolves a lease to its GameObject. False once the lease has been released.</summary>
		bool TryGet(Handle<PooledInstance> lease, out GameObject instance);

		/// <summary>Resolves a lease to a component on its GameObject. False if released or the component is absent.</summary>
		bool TryGet<T>(Handle<PooledInstance> lease, out T component) where T : Component;

		/// <summary>True while the lease is checked out.</summary>
		bool IsLeased(Handle<PooledInstance> lease);

		/// <summary>Returns the leased instance to its pool. False (no log) if the lease is already stale — releasing twice is not an error.</summary>
		bool Release(Handle<PooledInstance> lease);

		/// <summary>
		/// Registers the registry for identity lookups and creates/prewarms all its pools that
		/// have PrewarmOnRegister enabled. Call once at boot.
		/// </summary>
		void RegisterPools(ObjectPoolRegistry registry);

		/// <summary>Creates (if needed) and pre-warms the pool to its InitialPoolSize.</summary>
		void Prewarm(PoolableObjectDefinition definition);

		/// <summary>
		/// Takes an instance from the pool (creating one if empty and under MaxPoolSize),
		/// activates it, places it, and calls IPoolable.OnGetFromPool.
		/// Returns null if the pool is at MaxPoolSize and empty.
		/// </summary>
		GameObject Get(PoolableObjectDefinition definition, Vector3 position = default, Quaternion rotation = default,
		               Transform parent = null);

		/// <summary>Get with component access. Returns the requested component or null.</summary>
		T Get<T>(PoolableObjectDefinition definition, Vector3 position = default, Quaternion rotation = default,
		         Transform parent = null) where T : Component;

		/// <summary>
		/// Get by identity. None uses the first registered pool; pass an identity only when
		/// variants need individual addressing.
		/// </summary>
		GameObject Get(Uid<PoolableObjectDefinition> definitionId = default, Vector3 position = default, Quaternion rotation = default,
		               Transform parent = null);

		/// <summary>Identity variant of Get, optionally specifying the component type to return.</summary>
		T Get<T>(Uid<PoolableObjectDefinition> definitionId = default, Vector3 position = default, Quaternion rotation = default,
		         Transform parent = null) where T : Component;

		/// <summary>
		/// Returns an instance to its pool. Safe no-op (with warning) for instances not created
		/// by this service. Prefer PoolableObject.ReturnToPool() when available.
		/// </summary>
		void Release(GameObject instance);

		/// <summary>Number of currently checked-out instances for this definition's pool.</summary>
		int ActiveCount(PoolableObjectDefinition definition);

		/// <summary>Number of currently pooled (inactive) instances for this definition's pool.</summary>
		int InactiveCount(PoolableObjectDefinition definition);

		/// <summary>
		/// Destroys a pool's instances, idle and leased alike, and invalidates every lease into it.
		/// Pass null to clear everything.
		/// </summary>
		void Clear(PoolableObjectDefinition definition = null);
	}
}
