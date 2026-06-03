using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace AK.UISystem
{
	/// <summary>
	/// Central registry of all UIView prefabs. Replaces V1's separate Screen/Fragment lists.
	/// </summary>
	[CreateAssetMenu(fileName = "UIViewRepository", menuName = "AK/UI/View Repository")]
	public class UIViewRepository : ScriptableObject
	{
		[SerializeField] private List<UIView> _views = new();

		public IReadOnlyList<UIView> Views => _views;

#if UNITY_EDITOR
		[Button("Refresh All Views In Project")]
		[ContextMenu("Refresh All Views In Project")]
		private void RefreshAllViews()
		{
			var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
			var allViews = new List<UIView>();

			foreach (var guid in prefabGuids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

				if (prefab != null && prefab.GetComponent<UIView>() != null)
				{
					allViews.Add(prefab.GetComponent<UIView>());
				}
			}

			_views = allViews;
			EditorUtility.SetDirty(this);
			Debug.Log($"[UIViewRepository] Found {_views.Count} UIView prefabs.");
		}
#endif
	}
}

