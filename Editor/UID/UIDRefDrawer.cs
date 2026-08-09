using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Draws UIDRef fields as a flat object field filtered by an optional UIDOfTypeAttribute
	/// on the field (default: any UID). Selection writes only the GUID and asset name
	/// strings — never a hard reference. The stored GUID is resolved against the project
	/// for display; unresolvable GUIDs (asset deleted) are tinted red.
	/// The actual GUI lives in UIDRefGUI so the UIDOfType attribute drawer can share it.
	/// </summary>
	[CustomPropertyDrawer(typeof(UIDRef))]
	public class UIDRefDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var attrs = fieldInfo.GetCustomAttributes(typeof(UIDOfTypeAttribute), true);
			Type filterType = attrs.Length > 0 ? ((UIDOfTypeAttribute)attrs[0]).Type : typeof(UID);

			UIDRefGUI.Draw(position, property, label, filterType);
		}
	}

	internal static class UIDRefGUI
	{
		private static readonly Dictionary<string, UID> _resolveCache = new();
		private static readonly HashSet<string>         _unresolvable = new();

		public static void Draw(Rect position, SerializedProperty property, GUIContent label, Type filterType)
		{
			var guidProp = property.FindPropertyRelative("_guid");
			var nameProp = property.FindPropertyRelative("_assetName");

			if (guidProp == null || filterType == null)
			{
				EditorGUI.PropertyField(position, property, label, true);
				return;
			}

			EditorGUI.BeginProperty(position, label, property);

			UID current = null;
			if (!string.IsNullOrEmpty(guidProp.stringValue))
			{
				current = Resolve(filterType, guidProp.stringValue);

				// GUID went stale (identity regenerated) — heal by the stored asset
				// name and persist the fresh GUID, so the link repairs itself on sight.
				if (current == null && nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
				{
					UID healed = FindByName(filterType, nameProp.stringValue);
					if (healed != null)
					{
						guidProp.stringValue = healed.Id;
						nameProp.stringValue = healed.name;
						_resolveCache[healed.Id] = healed;
						current = healed;

						Debug.Log($"[UIDRef] Healed '{label.text}' → '{healed.name}' by asset name (its GUID had changed). Link persisted.",
						          property.serializedObject.targetObject);
					}
				}
			}

			bool missing = !string.IsNullOrEmpty(guidProp.stringValue) && current == null;
			if (missing)
			{
				label = new GUIContent($"{label.text} (Missing UID)", label.tooltip);
				GUI.color = new Color(1f, 0.6f, 0.6f);
			}

			EditorGUI.BeginChangeCheck();
			var newValue = EditorGUI.ObjectField(position, label, current, filterType, false);
			if (EditorGUI.EndChangeCheck())
			{
				var uid = newValue as UID;
				guidProp.stringValue = uid != null ? uid.Id : string.Empty;
				if (nameProp != null)
				{
					nameProp.stringValue = uid != null ? uid.name : string.Empty;
				}
			}

			GUI.color = Color.white;
			EditorGUI.EndProperty();
		}

		private static UID Resolve(Type filterType, string guid)
		{
			if (_resolveCache.TryGetValue(guid, out UID cached))
			{
				return cached;
			}

			if (_unresolvable.Contains(guid))
			{
				return null;
			}

			UID resolved = FindByGuid(filterType, guid);

			if (resolved != null)
			{
				_resolveCache[guid] = resolved;
			}
			else
			{
				_unresolvable.Add(guid);
			}

			return resolved;
		}

		private static UID FindByGuid(Type filterType, string guid)
		{
			foreach (string assetGuid in AssetDatabase.FindAssets($"t:{filterType.Name}"))
			{
				var asset = AssetDatabase.LoadAssetAtPath<UID>(AssetDatabase.GUIDToAssetPath(assetGuid));
				if (asset != null && asset.Id == guid)
				{
					return asset;
				}
			}

			return null;
		}

		private static UID FindByName(Type filterType, string assetName)
		{
			foreach (string assetGuid in AssetDatabase.FindAssets($"t:{filterType.Name}"))
			{
				var asset = AssetDatabase.LoadAssetAtPath<UID>(AssetDatabase.GUIDToAssetPath(assetGuid));
				if (asset != null && asset.name == assetName && !asset.IsEmpty())
				{
					return asset;
				}
			}

			return null;
		}
	}
}
