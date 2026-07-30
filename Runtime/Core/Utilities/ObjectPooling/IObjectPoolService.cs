using System.Collections.Generic;
using AK.Core;
using UnityEngine;

namespace AK.Utilities
{
	/// <summary>
	/// Game-wide GameObject pooling service. Data-driven via <see cref="PoolableObjectDefinition"/>
	/// assets registered in an <see cref="ObjectPoolRegistry"/>, instantly usable from code by
	/// passing a definition directly.
	///
	/// Usage:
	/// <code>
	/// // DI (Reflex): builder.RegisterType(typeof(ObjectPoolService), new[] { typeof(IObjectPoolService) });
	/// var bullet = _poolService.Get&lt;Bullet&gt;(bulletDefinition, muzzle.position, muzzle.rotation);
	/// bullet.ReturnToPool(); // or _poolService.Release(bullet.gameObject)
	/// </code>
	/// </summary>
	public interface IObjectPoolService
	{
		/// <summary>
		/// Registers the registry for UID-based lookups and creates/prewarms all its pools that
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
		/// Get by UID. The UID is OPTIONAL: pass null (or empty) to use the first registered pool.
		/// Use UIDs only when variants need individual addressing.
		/// </summary>
		GameObject Get(UID definitionUID = null, Vector3 position = default, Quaternion rotation = default,
		               Transform parent = null);

		/// <summary>UID variant of Get, optionally specifying the component type to return.</summary>
		T Get<T>(UID definitionUID = null, Vector3 position = default, Quaternion rotation = default,
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
		/// Disposes pools and destroys their idle instances (active ones are left alone).
		/// Pass null to clear everything.
		/// </summary>
		void Clear(PoolableObjectDefinition definition = null);
	}
}
