using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems
{
	public class ViewPool
	{
		private readonly Dictionary<PoolKey, Stack<UIView>> _pools = new();

		private readonly Transform _viewsContainer;
		private          Transform _poolRoot;

		public ViewPool(Transform viewsContainer)
		{
			_viewsContainer = viewsContainer;
		}

		private Transform PoolRoot
		{
			get
			{
				if (_poolRoot == null)
				{
					var go = new GameObject("[UIViewPool]");
					go.SetActive(false);
					var t = go.transform;
					t.SetParent(_viewsContainer, false);
					_poolRoot = t;
				}

				return _poolRoot;
			}
		}

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

				if (rect == null)
				{
					Debug.LogError($"[ViewPool] Pooled view '{view.name}' has no RectTransform - cannot re-parent. Instantiating instead.");
					return UnityEngine.Object.Instantiate(prefab, parent);
				}

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
			if (view == null) return;

			var key = new PoolKey { Type = view.GetType(), ViewId = view.ViewId };

			if (!_pools.TryGetValue(key, out var stack))
			{
				stack = new Stack<UIView>();
				_pools[key] = stack;
			}

			if (stack.Contains(view))
			{
				Debug.LogWarning($"[ViewPool] View '{view.name}' released twice - ignoring the second release.");
				return;
			}

			// Let the view close its dynamic children before pooling
			// This prevents orphaned child views when parent is pooled
			view.OnBeforePool();

			// InternalCleanup runs full close lifecycle (OnPrepareHide → OnHide → UnRegisterResources → NullifyContext).
			// Idempotent — safe even if InternalHideAsync already ran the close hooks.
			view.InternalCleanup();

			// OnReset lets the view clear custom state (text, images, references) for reuse.
			view.OnReset();

			// Kill any tweens still targeting this view's hierarchy. InternalCleanup handles the
			// view's own animation targets, but per-view leftovers (e.g. toast floaters) and
			// animation-strategy ambient loops can survive that - a surviving sequence that
			// completes later would call Close() on an unregistered view.
			foreach (var canvasGroup in view.GetComponentsInChildren<CanvasGroup>(true))
			{
				DOTween.Kill(canvasGroup);
			}

			foreach (var rectTransform in view.GetComponentsInChildren<RectTransform>(true))
			{
				DOTween.Kill(rectTransform);
			}

			// Restore interaction state: pause-behaviours (PauseOnlyBelow etc.) flip
			// interactable/blocksRaycasts off, and nothing restored them on the reuse path.
			if (view.CanvasGroup != null)
			{
				view.CanvasGroup.interactable = true;
				view.CanvasGroup.blocksRaycasts = true;
			}

			view.gameObject.SetActive(false);
			view.transform.SetParent(PoolRoot, false);

			stack.Push(view);
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
