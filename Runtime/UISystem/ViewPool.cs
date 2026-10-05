using System.Collections.Generic;
using AK.Core.Extensions;
using DG.Tweening;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Shelves closed view instances by kind for reuse, at most <see cref="CapacityPerKind"/> of
	/// each. A view arrives here already settled by the close pipeline — hooks run, children
	/// cascaded, state reset — so the pool only makes the object inert (kills every tween on it
	/// or inside it, deactivates, reparents) and hands it back on the next spawn of the same
	/// kind, posed like a fresh instance of its prefab.
	/// </summary>
	public class ViewPool
	{
		/// <summary>Idle instances kept per kind when the UI system is not told otherwise.</summary>
		public const int DefaultCapacityPerKind = 8;

		private readonly Dictionary<ViewKey, Stack<UIView>> _pools  = new();
		private readonly HashSet<UIView>                    _pooled = new();
		private readonly List<Tween>                        _tweens = new();

		private readonly Transform _viewsContainer;
		private          Transform _poolRoot;

		public ViewPool(Transform viewsContainer, int capacityPerKind = DefaultCapacityPerKind)
		{
			_viewsContainer = viewsContainer;
			CapacityPerKind = Mathf.Max(0, capacityPerKind);
		}

		/// <summary>Idle instances kept per kind. A view released while its kind is full is refused.</summary>
		public int CapacityPerKind { get; }

		/// <summary>Idle instances of every kind.</summary>
		public int Count => _pooled.Count;

		/// <summary>Idle instances by kind. Diagnostics and editor tooling.</summary>
		internal IReadOnlyDictionary<ViewKey, Stack<UIView>> Pools => _pools;

		/// <summary>True when a view of <paramref name="view"/>'s kind would be shelved, not refused.</summary>
		public bool HasRoomFor(UIView view)
		{
			return !_pools.TryGetValue(ViewKey.Of(view), out var stack) || stack.Count < CapacityPerKind;
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
			if (_pools.TryGetValue(ViewKey.Of(prefab), out var stack))
			{
				while (stack.Count > 0)
				{
					var view = stack.Pop();
					_pooled.Remove(view);

					// Destroyed while shelved: a scene unload, a stray Destroy.
					if (view == null) continue;

					if (view.transform is not RectTransform rect)
					{
						Debug.LogError($"[ViewPool] Pooled view '{view.name}' has no RectTransform - cannot re-parent. Instantiating instead.");
						Destroy(view);
						break;
					}

					rect.SetParent(parent, false);
					rect.MatchLocalPose(prefab.transform);

					view.gameObject.SetActive(true);
					return view as TView;
				}
			}

			return Object.Instantiate(prefab, parent);
		}

		/// <summary>
		/// Shelves <paramref name="view"/> for reuse. False when its kind already has
		/// <see cref="CapacityPerKind"/> idle instances: the view is left as it is, and the
		/// caller destroys it.
		/// </summary>
		public bool Release(UIView view)
		{
			if (view == null) return true;

			if (_pooled.Contains(view))
			{
				Debug.LogWarning($"[ViewPool] View '{view.name}' released twice - ignoring the second release.");
				return true;
			}

			var key = ViewKey.Of(view);

			if (!_pools.TryGetValue(key, out var stack))
			{
				stack = new Stack<UIView>();
				_pools[key] = stack;
			}

			if (stack.Count >= CapacityPerKind) return false;

			KillTweens(view.transform);

			view.gameObject.SetActive(false);
			view.transform.SetParent(PoolRoot, false);

			_pooled.Add(view);
			stack.Push(view);
			return true;
		}

		/// <summary>Destroys the idle instances of each kind beyond <paramref name="keepPerKind"/>. Zero empties the pool.</summary>
		public void Trim(int keepPerKind)
		{
			keepPerKind = Mathf.Max(0, keepPerKind);

			foreach (var stack in _pools.Values)
			{
				while (stack.Count > keepPerKind)
				{
					var view = stack.Pop();
					_pooled.Remove(view);
					Destroy(view);
				}
			}
		}

		public void Clear()
		{
			Trim(0);
			_pools.Clear();
			_pooled.Clear();
		}

		/// <summary>
		/// Kills every tween whose target or id is the view, anything on it, or anything inside
		/// it. A strategy's tweens are linked to the content and die as it deactivates; this
		/// catches the rest — tweens on other components (an image's colour, a label's fade),
		/// on children, or kept by id — whose callbacks would otherwise fire on a shelved view.
		/// One pass over the running tweens; a tween nested in a sequence goes with its sequence.
		/// </summary>
		private void KillTweens(Transform root)
		{
			_tweens.Clear();
			KillTweensUnder(root, DOTween.PlayingTweens(_tweens));
			_tweens.Clear();
			KillTweensUnder(root, DOTween.PausedTweens(_tweens));
			_tweens.Clear();
		}

		private static void KillTweensUnder(Transform root, List<Tween> tweens)
		{
			if (tweens == null) return;

			for (int i = 0; i < tweens.Count; i++)
			{
				Tween tween = tweens[i];

				// Already killed with an earlier match.
				if (!tween.active) continue;

				// Kills by the object found under the root, which takes that object's other tweens
				// too. Tween.Kill() would do nothing before DOTween initializes (edit mode);
				// DOTween.Kill works either way, like the view's own DOKill.
				if (IsUnder(tween.target, root)) DOTween.Kill(tween.target);
				else if (IsUnder(tween.id, root)) DOTween.Kill(tween.id);
			}
		}

		private static bool IsUnder(object targetOrId, Transform root)
		{
			return targetOrId switch
			{
				Component component   => component != null && component.transform.IsChildOf(root),
				GameObject gameObject => gameObject != null && gameObject.transform.IsChildOf(root),
				_                     => false,
			};
		}

		private static void Destroy(UIView view)
		{
			if (view == null) return;

			if (Application.isPlaying) Object.Destroy(view.gameObject);
			else Object.DestroyImmediate(view.gameObject);
		}
	}
}
