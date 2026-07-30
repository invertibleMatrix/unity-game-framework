using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Utilities
{
	/// <summary>
	/// Defines one poolable prefab and its pool sizing. Registered in an
	/// <see cref="ObjectPoolRegistry"/> and spawned through <see cref="IObjectPoolService"/>.
	///
	/// The UID (inherited) is OPTIONAL: pools are primarily addressed by this definition asset
	/// directly. Assign a UID only when you need data-driven lookup (e.g. "spawn pool 'EnemyRed'"
	/// from metadata) or when variants share prefab types.
	/// </summary>
	[CreateAssetMenu(fileName = "PoolableObjectDefinition", menuName = "AK/Pooling/Poolable Object Definition")]
	public class PoolableObjectDefinition : UID
	{
		[Tooltip("Prefab to pool. Components implementing IPoolable get lifecycle callbacks.")]
		[SerializeField] private GameObject _prefab;

		[Tooltip("Instances created up-front on Prewarm().")]
		[Min(0)] [SerializeField] private int _initialPoolSize = 8;

		[Tooltip("Hard cap on total live instances (active + pooled). Released instances beyond the cap " +
		         "are destroyed. 0 = unlimited.")]
		[Min(0)] [SerializeField] private int _maxPoolSize = 64;

		[Tooltip("Pre-warm this pool when its registry is registered with the service.")]
		[SerializeField] private bool _prewarmOnRegister = true;

		public GameObject Prefab            => _prefab;
		public int        InitialPoolSize   => _initialPoolSize;
		public int        MaxPoolSize       => _maxPoolSize;
		public bool       PrewarmOnRegister => _prewarmOnRegister;
	}
}
