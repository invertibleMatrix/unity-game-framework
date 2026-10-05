using AK.Core;
using AK.Kernel.Collections;
using UnityEngine;

namespace AK.Utilities
{
	/// <summary>
	/// Game-wide GameObject pooling. Each pool is described by a <see cref="PoolableObjectDefinition"/>:
	/// pass the definition directly, or register an <see cref="ObjectPoolRegistry"/> and pass a
	/// definition's id.
	///
	/// An instance is placed the way <c>Object.Instantiate</c> places a new one: under a parent
	/// with its prefab's local pose, or at a world position and rotation with its prefab's scale.
	///
	/// Two ways to hold a checked-out object:
	/// <code>
	/// // Reference API: simplest. The caller holds a GameObject and must not touch it after releasing it.
	/// var bullet = _poolService.Get&lt;Bullet&gt;(bulletDefinition, muzzle.position, muzzle.rotation);
	/// bullet.ReturnToPool(); // or _poolService.Release(bullet.gameObject)
	///
	/// // Lease API: for anything that outlives a frame (timers, AI targets, VFX bookkeeping).
	/// // A stale lease resolves to nothing instead of to whoever got the object next.
	/// Handle&lt;PooledInstance&gt; lease = _poolService.Lease(bulletDefinition, muzzle.position, muzzle.rotation);
	/// if (_poolService.TryGet(lease, out Bullet b)) b.Fire();
	/// _poolService.Release(lease); // false if it was already released
	/// </code>
	///
	/// An instance destroyed outside its pool, by a scene unload or a stray Destroy, ends its
	/// lease and frees its place under the pool's cap.
	/// </summary>
	public interface IObjectPoolService
	{
		/// <summary>
		/// Checks an instance out under <paramref name="parent"/> with its prefab's local pose, as
		/// <c>Object.Instantiate(prefab, parent)</c> places a new one, and returns a lease for it.
		/// Invalid, logged, when the definition has no prefab or its pool is empty and at its cap.
		/// </summary>
		Handle<PooledInstance> Lease(PoolableObjectDefinition definition, Transform parent = null);

		/// <summary>
		/// Checks an instance out at a world <paramref name="position"/> and <paramref name="rotation"/>,
		/// under <paramref name="parent"/> with its prefab's scale, as
		/// <c>Object.Instantiate(prefab, position, rotation, parent)</c> places a new one.
		/// </summary>
		Handle<PooledInstance> Lease(PoolableObjectDefinition definition, Vector3 position, Quaternion rotation,
		                             Transform parent = null);

		/// <summary>
		/// <see cref="Lease(PoolableObjectDefinition, Transform)"/> from the registered definition
		/// with <paramref name="definitionId"/>. Invalid, logged, for an unset or unknown id.
		/// </summary>
		Handle<PooledInstance> Lease(Uid<PoolableObjectDefinition> definitionId, Transform parent = null);

		/// <summary>
		/// <see cref="Lease(PoolableObjectDefinition, Vector3, Quaternion, Transform)"/> from the
		/// registered definition with <paramref name="definitionId"/>. Invalid, logged, for an
		/// unset or unknown id.
		/// </summary>
		Handle<PooledInstance> Lease(Uid<PoolableObjectDefinition> definitionId, Vector3 position, Quaternion rotation,
		                             Transform parent = null);

		/// <summary>Resolves a lease to its GameObject. False once the lease has ended.</summary>
		bool TryGet(Handle<PooledInstance> lease, out GameObject instance);

		/// <summary>Resolves a lease to a component on its GameObject. False once the lease has ended, or when the component is absent.</summary>
		bool TryGet<T>(Handle<PooledInstance> lease, out T component) where T : Component;

		/// <summary>True while the lease is checked out and its instance alive.</summary>
		bool IsLeased(Handle<PooledInstance> lease);

		/// <summary>
		/// Returns the leased instance to its pool. False, without a log, when the lease has
		/// already ended: released before, or its instance destroyed.
		/// </summary>
		bool Release(Handle<PooledInstance> lease);

		/// <summary>
		/// Registers the registry for identity lookups and creates its pools, prewarming those
		/// with PrewarmOnRegister. Call once at boot.
		/// </summary>
		void RegisterPools(ObjectPoolRegistry registry);

		/// <summary>Creates the pool if needed and fills it to its InitialPoolSize, counting instances already leased.</summary>
		void Prewarm(PoolableObjectDefinition definition);

		/// <summary>
		/// <see cref="Lease(PoolableObjectDefinition, Transform)"/> that returns the instance:
		/// null when none could be checked out.
		/// </summary>
		GameObject Get(PoolableObjectDefinition definition, Transform parent = null);

		/// <summary>
		/// <see cref="Lease(PoolableObjectDefinition, Vector3, Quaternion, Transform)"/> that
		/// returns the instance: null when none could be checked out.
		/// </summary>
		GameObject Get(PoolableObjectDefinition definition, Vector3 position, Quaternion rotation, Transform parent = null);

		/// <summary>
		/// Get that returns the instance's <typeparamref name="T"/>: null when none could be checked
		/// out, or when the instance has no <typeparamref name="T"/>, which then goes back, logged.
		/// </summary>
		T Get<T>(PoolableObjectDefinition definition, Transform parent = null) where T : Component;

		/// <summary>
		/// Get that returns the instance's <typeparamref name="T"/>: null when none could be checked
		/// out, or when the instance has no <typeparamref name="T"/>, which then goes back, logged.
		/// </summary>
		T Get<T>(PoolableObjectDefinition definition, Vector3 position, Quaternion rotation, Transform parent = null)
			where T : Component;

		/// <summary>Get from the registered definition with <paramref name="definitionId"/>. Null, logged, for an unset or unknown id.</summary>
		GameObject Get(Uid<PoolableObjectDefinition> definitionId, Transform parent = null);

		/// <summary>Get from the registered definition with <paramref name="definitionId"/>. Null, logged, for an unset or unknown id.</summary>
		GameObject Get(Uid<PoolableObjectDefinition> definitionId, Vector3 position, Quaternion rotation, Transform parent = null);

		/// <summary>Get from the registered definition with <paramref name="definitionId"/>. Null, logged, for an unset or unknown id.</summary>
		T Get<T>(Uid<PoolableObjectDefinition> definitionId, Transform parent = null) where T : Component;

		/// <summary>Get from the registered definition with <paramref name="definitionId"/>. Null, logged, for an unset or unknown id.</summary>
		T Get<T>(Uid<PoolableObjectDefinition> definitionId, Vector3 position, Quaternion rotation, Transform parent = null)
			where T : Component;

		/// <summary>
		/// Returns a checked-out instance to its pool. An instance this service didn't make is
		/// left as it is, with a warning, and so is one already returned. Prefer
		/// PoolableObject.ReturnToPool() where there is one.
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
