using AK.Kernel.Collections;
using UnityEngine;

namespace AK.Utilities
{
	/// <summary>
	/// What the pool stores per instantiated prefab: the object, its poolable components
	/// (captured once — <c>GetComponents</c> allocates), and the definition it belongs to.
	/// </summary>
	public sealed class PooledInstance
	{
		public readonly GameObject               GameObject;
		public readonly Transform                Transform;
		public readonly IPoolable[]              Poolables;
		public readonly PoolableObjectDefinition Definition;

		/// <summary>Handle of the current check-out. <see cref="Handle{T}.Invalid"/> while resting in the pool.</summary>
		public Handle<PooledInstance> Lease;

		public PooledInstance(GameObject gameObject, IPoolable[] poolables, PoolableObjectDefinition definition)
		{
			GameObject = gameObject;
			Transform  = gameObject.transform;
			Poolables  = poolables;
			Definition = definition;
		}
	}
}
