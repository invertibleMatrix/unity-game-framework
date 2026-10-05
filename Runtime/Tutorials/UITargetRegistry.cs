using System.Collections.Generic;
using AK.Core;
using UnityEngine;

namespace AK.Tutorials
{
	public class UITargetRegistry : IUITargetRegistry
	{
		// Lists in registration order. An id's list stays once emptied: views register their
		// targets again each time they show.
		private readonly Dictionary<Uid, List<RectTransform>> _targets = new();

		public void Register(UITargetId id, RectTransform target)
		{
			if (id == null || !id.HasIdentity || target == null) return;

			if (!_targets.TryGetValue(id.Id, out List<RectTransform> instances))
			{
				instances = new List<RectTransform>(1);
				_targets.Add(id.Id, instances);
			}

			PurgeDestroyed(instances);
			if (!instances.Contains(target)) instances.Add(target);
		}

		public void Unregister(UITargetId id, RectTransform target)
		{
			if (id == null || !id.HasIdentity) return;

			if (_targets.TryGetValue(id.Id, out List<RectTransform> instances))
			{
				instances.Remove(target);
				PurgeDestroyed(instances);
			}
		}

		public bool TryGet(UITargetId id, out RectTransform target)
		{
			target = null;
			if (id == null || !id.HasIdentity || !_targets.TryGetValue(id.Id, out List<RectTransform> instances))
			{
				return false;
			}

			PurgeDestroyed(instances);

			DrawOrder best = default;
			for (int i = 0; i < instances.Count; i++)
			{
				RectTransform candidate = instances[i];
				DrawOrder order = DrawOrder.Of(candidate);

				// Later registrations win ties, so the view shown last wins between equals.
				if (target == null || order.CompareTo(best) >= 0)
				{
					target = candidate;
					best = order;
				}
			}

			return target != null;
		}

		private static void PurgeDestroyed(List<RectTransform> instances)
		{
			for (int i = instances.Count - 1; i >= 0; i--)
			{
				if (instances[i] == null) instances.RemoveAt(i);
			}
		}

		/// <summary>Where a target draws relative to the others, for picking the topmost.</summary>
		private readonly struct DrawOrder
		{
			private readonly bool _active;
			private readonly bool _overlay;
			private readonly int  _layer;
			private readonly int  _order;

			private DrawOrder(bool active, bool overlay, int layer, int order)
			{
				_active = active;
				_overlay = overlay;
				_layer = layer;
				_order = order;
			}

			public static DrawOrder Of(RectTransform target)
			{
				if (!target.gameObject.activeInHierarchy) return default;

				Canvas canvas = SortingCanvasOf(target);
				if (canvas == null) return new DrawOrder(true, false, int.MinValue, int.MinValue);

				Canvas root = canvas.rootCanvas;
				bool overlay = root != null && root.renderMode == RenderMode.ScreenSpaceOverlay;
				return new DrawOrder(true, overlay, SortingLayer.GetLayerValueFromID(canvas.sortingLayerID), canvas.sortingOrder);
			}

			public int CompareTo(in DrawOrder other)
			{
				if (_active != other._active) return _active ? 1 : -1;
				if (_overlay != other._overlay) return _overlay ? 1 : -1;
				if (_layer != other._layer) return _layer.CompareTo(other._layer);
				return _order.CompareTo(other._order);
			}

			/// <summary>The canvas whose sorting the target draws with: the nearest that overrides sorting, or the root.</summary>
			private static Canvas SortingCanvasOf(Transform target)
			{
				Canvas canvas = target.GetComponentInParent<Canvas>();
				while (canvas != null && !canvas.isRootCanvas && !canvas.overrideSorting)
				{
					Transform parent = canvas.transform.parent;
					canvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;
				}

				return canvas;
			}
		}
	}
}
