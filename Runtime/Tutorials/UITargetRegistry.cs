using System.Collections.Generic;
using UnityEngine;

namespace AK.Tutorials
{
	public class UITargetRegistry : IUITargetRegistry
	{
		private readonly Dictionary<string, RectTransform> _targets = new();

		public void Register(UITargetId id, RectTransform target)
		{
			if (id == null || target == null) return;

			if (_targets.TryGetValue(id.Id, out var existing) && existing != null && existing != target)
			{
				Debug.LogWarning($"[UITargetRegistry] Id '{id.name}' reassigned from '{existing.name}' to '{target.name}'.");
			}

			_targets[id.Id] = target;
		}

		public void Unregister(UITargetId id, RectTransform target)
		{
			if (id == null) return;

			if (_targets.TryGetValue(id.Id, out var existing) && existing == target)
			{
				_targets.Remove(id.Id);
			}
		}

		public bool TryGet(UITargetId id, out RectTransform target)
		{
			if (id != null && _targets.TryGetValue(id.Id, out target) && target != null)
			{
				return true;
			}

			target = null;
			return false;
		}
	}
}
