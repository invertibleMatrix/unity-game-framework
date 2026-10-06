using System.Collections.Generic;
using Reflex.Core;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Where view instances come from and go to: the repository's prefabs (cached by kind,
	/// with whether each is a screen so the show path never re-queries components), the
	/// pool, template clones, and destruction. A view the pool has no room for is destroyed.
	/// </summary>
	internal sealed class ViewFactory
	{
		public readonly struct PrefabEntry
		{
			public readonly UIView Prefab;
			public readonly bool   IsScreen;

			public PrefabEntry(UIView prefab, bool isScreen)
			{
				Prefab = prefab;
				IsScreen = isScreen;
			}
		}

		private readonly UIViewRepository _repository;
		private readonly Transform        _viewsRoot;
		private readonly ViewPool         _pool;

		private Dictionary<ViewKey, PrefabEntry> _prefabs;

		public ViewFactory(UIViewRepository repository, Transform viewsRoot, Container container,
		                   int poolCapacityPerKind = ViewPool.DefaultCapacityPerKind)
		{
			_repository = repository;
			_viewsRoot = viewsRoot;
			_pool = new ViewPool(viewsRoot, poolCapacityPerKind);
			Container = container;
		}

		/// <summary>DI container every attached view is injected from.</summary>
		public Container Container { get; }

		public ViewPool Pool => _pool;

		/// <summary>
		/// Set once the application is quitting. Views settling during teardown are destroyed
		/// instead of pooled: reparenting a GameObject that is itself being destroyed is an error.
		/// </summary>
		public bool IsShuttingDown { get; set; }

		/// <summary>True when a closing view of this kind goes to the pool: not shutting down, and room for its kind.</summary>
		public bool CanPool(UIView view) => !IsShuttingDown && _pool.HasRoomFor(view);

		public bool TryGetPrefab(ViewKey key, out PrefabEntry entry)
		{
			if (_prefabs == null)
			{
				_prefabs = new Dictionary<ViewKey, PrefabEntry>();
				IReadOnlyList<UIView> views = _repository.Views;
				for (int i = 0; i < views.Count; i++)
				{
					UIView view = views[i];
					if (view != null)
						_prefabs[ViewKey.Of(view)] = new PrefabEntry(view, view.GetComponent<UIViewChannel>() != null);
				}
			}

			return _prefabs.TryGetValue(key, out entry);
		}

		/// <summary>A pooled or fresh instance of <paramref name="prefab"/>, parented where its kind lives: screens under the views root, fragments in their parent's container.</summary>
		public TView Spawn<TView>(TView prefab, bool isScreen, UIView parent) where TView : UIView
		{
			Transform slot = isScreen || parent == null ? _viewsRoot : parent.FragmentContainer;
			return _pool.Get(prefab, slot);
		}

		/// <summary>A live copy of a template in the template's own layout slot, inactive like the template.</summary>
		public TView Clone<TView>(TView template) where TView : UIView
		{
			var go = Object.Instantiate(template.gameObject, template.transform.parent);
			go.name = template.name;
			return go.GetComponent<TView>();
		}

		/// <summary>Shelves a settled view, or destroys it when its kind's pool has filled up meanwhile.</summary>
		public void Release(UIView view)
		{
			if (!_pool.Release(view)) Destroy(view);
		}

		public static void Destroy(UIView view)
		{
			GameObject go = view.gameObject;
			if (Application.isPlaying) Object.Destroy(go);
			else Object.DestroyImmediate(go);
		}

		public void Clear() => _pool.Clear();
	}
}
