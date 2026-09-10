using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems
{
	public class ViewPool
	{
		private readonly Dictionary<ViewKey, Stack<UIView>> _pools  = new();
		private readonly HashSet<UIView>                    _pooled = new();

		private readonly List<CanvasGroup>   _canvasGroupScratch   = new();
		private readonly List<RectTransform> _rectTransformScratch = new();

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

		public TView Get<TView>(TView prefab, Transform parent) where TView : UIView
		{
			if (_pools.TryGetValue(ViewKey.Of(prefab), out var stack) && stack.Count > 0)
			{
				var view = stack.Pop();
				_pooled.Remove(view);
				var rect = view.transform as RectTransform;

				if (rect == null)
				{
					Debug.LogError($"[ViewPool] Pooled view '{view.name}' has no RectTransform - cannot re-parent. Instantiating instead.");
					return Object.Instantiate(prefab, parent);
				}

				rect.SetParent(parent, false);
				rect.localScale = Vector3.one;
				rect.localRotation = Quaternion.identity;
				rect.anchoredPosition = Vector2.zero;

				view.gameObject.SetActive(true);
				return view as TView;
			}

			return Object.Instantiate(prefab, parent);
		}

		public void Release(UIView view)
		{
			if (view == null) return;

			if (!_pooled.Add(view))
			{
				Debug.LogWarning($"[ViewPool] View '{view.name}' released twice - ignoring the second release.");
				return;
			}

			var key = ViewKey.Of(view);

			if (!_pools.TryGetValue(key, out var stack))
			{
				stack = new Stack<UIView>();
				_pools[key] = stack;
			}

			// Let the view close its dynamic children before pooling
			// This prevents orphaned child views when parent is pooled
			view.OnBeforePool();

			view.Lifecycle().Teardown();

			if (view.TryGetComponent(out ViewHighlight highlight))
			{
				highlight.Restore();
			}

			// OnReset lets the view clear custom state (text, images, references) for reuse.
			view.OnReset();

			// Kill any tweens still targeting this view's hierarchy. Teardown handles the
			// view's own animation targets, but per-view leftovers (e.g. toast floaters) and
			// animation-strategy ambient loops can survive that - a surviving sequence that
			// completes later would call Close() on an unregistered view.
			view.GetComponentsInChildren(true, _canvasGroupScratch);
			for (int i = 0; i < _canvasGroupScratch.Count; i++)
			{
				DOTween.Kill(_canvasGroupScratch[i]);
			}

			_canvasGroupScratch.Clear();

			view.GetComponentsInChildren(true, _rectTransformScratch);
			for (int i = 0; i < _rectTransformScratch.Count; i++)
			{
				DOTween.Kill(_rectTransformScratch[i]);
			}

			_rectTransformScratch.Clear();

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
						if (Application.isPlaying) Object.Destroy(view.gameObject);
						else Object.DestroyImmediate(view.gameObject);
					}
				}
			}

			_pools.Clear();
			_pooled.Clear();
		}
	}
}
