using System;
using System.Collections.Generic;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Object pool for UIViews. Generalised from V1's FragmentPool — works for any UIView.
	/// </summary>
	public class ViewPool
	{
		private readonly Dictionary<PoolKey, Stack<UIView>> _pools = new();

		private struct PoolKey : IEquatable<PoolKey>
		{
			public Type   Type;
			public string ViewId;

			public bool Equals(PoolKey other) => Type == other.Type && ViewId == other.ViewId;
			public override bool Equals(object obj) => obj is PoolKey other && Equals(other);
			public override int GetHashCode() => HashCode.Combine(Type, ViewId);
		}

		public TView Get<TView>(TView prefab, Transform parent) where TView : UIView
		{
			var key = new PoolKey { Type = prefab.GetType(), ViewId = prefab.ViewId };

			if (_pools.TryGetValue(key, out var stack) && stack.Count > 0)
			{
				var view = stack.Pop();
				var rect = view.transform as RectTransform;

				rect.SetParent(parent, false);
				rect.localScale = Vector3.one;
				rect.localRotation = Quaternion.identity;
				rect.anchoredPosition = Vector2.zero;

				view.gameObject.SetActive(true);
				return view as TView;
			}

			return UnityEngine.Object.Instantiate(prefab, parent);
		}

		public void Release(UIView view)
		{
			var key = new PoolKey { Type = view.GetType(), ViewId = view.ViewId };

			if (!_pools.ContainsKey(key))
			{
				_pools[key] = new Stack<UIView>();
			}

			// Let the view close its dynamic children before pooling
			// This prevents orphaned child views when parent is pooled
			view.OnBeforePool();

			// InternalCleanup runs full close lifecycle (OnPrepareHide → OnHide → UnRegisterResources → NullifyContext).
			// Idempotent — safe even if InternalHideAsync already ran the close hooks.
			view.InternalCleanup();

			// OnReset lets the view clear custom state (text, images, references) for reuse.
			view.OnReset();
			view.gameObject.SetActive(false);
			view.transform.SetParent(null);

			_pools[key].Push(view);
		}

		public void Clear()
		{
			foreach (var kvp in _pools)
			{
				while (kvp.Value.Count > 0)
				{
					var view = kvp.Value.Pop();
					if (view != null && view.gameObject != null)
					{
						UnityEngine.Object.Destroy(view.gameObject);
					}
				}
			}

			_pools.Clear();
		}
	}
}

