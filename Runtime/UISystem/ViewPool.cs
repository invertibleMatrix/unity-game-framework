using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Shelves closed view instances by kind for reuse. A view arrives here already settled by
	/// the close pipeline — hooks run, children cascaded, state reset — so the pool only makes
	/// the object inert (kills what still tweens inside it, deactivates, reparents) and hands
	/// it back on the next spawn of the same kind.
	/// </summary>
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

		/// <summary>Idle instances by kind. Diagnostics and editor tooling.</summary>
		internal IReadOnlyDictionary<ViewKey, Stack<UIView>> Pools => _pools;

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

			// Strategy tweens are linked to the content and die with it; this catches what is
			// not — per-view leftovers such as toast floaters, whose completion callbacks would
			// otherwise fire on a shelved view.
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
