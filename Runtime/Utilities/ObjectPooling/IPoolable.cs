using System;
using UnityEngine;

namespace AK.Utilities
{
	/// <summary>
	/// Implement on any pooled component to receive lifecycle callbacks.
	/// </summary>
	public interface IPoolable
	{
		/// <summary>Called after the instance is taken from the pool and activated.</summary>
		void OnGetFromPool();

		/// <summary>Called before the instance is returned to the pool and deactivated.</summary>
		void OnReturnToPool();
	}

	/// <summary>
	/// Convenience base class for pooled objects. Adds ReturnToPool() so instances can release
	/// themselves without holding a reference to the pool service.
	/// </summary>
	public abstract class PoolableObject : MonoBehaviour, IPoolable
	{
		private Action<PoolableObject> _returnAction;

		/// <summary>True while the instance rests in its pool; false while it is checked out, or when no pool made it.</summary>
		public bool IsInPool { get; internal set; }

		/// <summary>Internal: wired up by ObjectPoolService at creation time.</summary>
		internal void SetReturnAction(Action<PoolableObject> returnAction)
		{
			_returnAction = returnAction;
		}

		/// <summary>
		/// Returns this instance to its pool. One already back in its pool, or made by no pool, is
		/// left as it is, with a warning.
		/// </summary>
		public void ReturnToPool()
		{
			if (IsInPool)
			{
				Debug.LogWarning($"[Pooling] '{name}' returned to pool twice.", this);
				return;
			}

			if (_returnAction == null)
			{
				Debug.LogWarning($"[Pooling] '{name}' was not made by an ObjectPoolService; left as it is.", this);
				return;
			}

			_returnAction(this);
		}

		public virtual void OnGetFromPool() { }

		public virtual void OnReturnToPool() { }
	}
}
