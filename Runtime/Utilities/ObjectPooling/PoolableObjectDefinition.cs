using AK.Core;
using UnityEngine;

namespace AK.Utilities
{
	/// <summary>
	/// Defines one poolable prefab and its pool sizing. Registered in an
	/// <see cref="ObjectPoolRegistry"/> and spawned through <see cref="IObjectPoolService"/>.
	///
	/// Pools are primarily addressed by this definition asset directly. Use its identity
	/// (<see cref="UID.Id"/>) only for data-driven lookup (e.g. "spawn pool 'EnemyRed'" from
	/// metadata) or when variants share prefab types.
	/// </summary>
	[CreateAssetMenu(fileName = "PoolableObjectDefinition", menuName = "AK/Pooling/Poolable Object Definition")]
	public class PoolableObjectDefinition : UID
	{
		[Tooltip("Prefab to pool. Components implementing IPoolable get lifecycle callbacks.")]
		[SerializeField] private GameObject _prefab;

		[Tooltip("Instances created up-front on Prewarm().")]
		[Min(0)] [SerializeField] private int _initialPoolSize = 8;

		[Tooltip("Most instances alive at once, leased and resting: a lease past it is refused. Never below " +
		         "the initial pool size. 0 = unlimited.")]
		[Min(0)] [SerializeField] private int _maxPoolSize = 64;

		[Tooltip("Pre-warm this pool when its registry is registered with the service.")]
		[SerializeField] private bool _prewarmOnRegister = true;

		public GameObject Prefab            => _prefab;
		public int        InitialPoolSize   => _initialPoolSize;
		public int        MaxPoolSize       => _maxPoolSize;
		public bool       PrewarmOnRegister => _prewarmOnRegister;
	}
}
