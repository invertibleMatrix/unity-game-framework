using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace AK.Core.Editor
{
	public static class UIDContextMenu
	{
		private const string UID_MENU_PATH        = "Assets/Create/Utilities/UID";
		private const string CREATE_UID_IN_FOLDER = "Create UID In This Folder";

		[MenuItem(UID_MENU_PATH, false, 1)]
		private static void CreateUIDAsset()
		{
			var selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
			var folderPath = string.IsNullOrEmpty(selectedPath)
				? "Assets"
				: (AssetDatabase.IsValidFolder(selectedPath) ? selectedPath : Path.GetDirectoryName(selectedPath));

			CreateUIDAssetInFolder(folderPath, "NewUID");
		}

		[InitializeOnLoadMethod]
		private static void Initialize()
		{
			EditorApplication.contextualPropertyMenu += OnContextMenuGUI;
		}

		private static void OnContextMenuGUI(GenericMenu menu, SerializedProperty property)
		{
			// Check if this property is a UID field
			if (property.propertyType == SerializedPropertyType.ObjectReference &&
			    property.objectReferenceValue == null)
			{
				// Get the actual field type using reflection
				var fieldInfo = GetFieldInfoFromProperty(property);
				if (fieldInfo != null && fieldInfo.FieldType == typeof(UID))
				{
					menu.AddItem(new GUIContent(CREATE_UID_IN_FOLDER), false, () => { CreateUIDForField(property); });

					menu.AddSeparator("");
				}
			}
		}

		private static FieldInfo GetFieldInfoFromProperty(SerializedProperty property)
		{
			var targetObject = property.serializedObject.targetObject;
			var targetType = targetObject.GetType();

			// Navigate to the field using the property path
			var pathParts = property.propertyPath.Split('.');
			var currentType = targetType;

			for (int i = 0; i < pathParts.Length; i++)
			{
				var part = pathParts[i];

				// Handle array/list elements like "array[i]"
				if (part.Contains("["))
				{
					var cleanPart = part.Substring(0, part.IndexOf("["));
					var field = currentType.GetField(cleanPart, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					if (field == null) return null;

					currentType = field.FieldType;

					// Handle array/list types
					if (currentType.IsArray)
					{
						currentType = currentType.GetElementType();
					}
					else if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(List<>))
					{
						currentType = currentType.GetGenericArguments()[0];
					}
				}
				else
				{
					var field = currentType.GetField(part, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					if (field == null) return null;

					// If this is the last part, return the field info
					if (i == pathParts.Length - 1)
					{
						return field;
					}

					currentType = field.FieldType;
				}
			}

			return null;
		}

		private static void CreateUIDForField(SerializedProperty property)
		{
			// Get the ScriptableObject that contains this UID field
			var targetObject = property.serializedObject.targetObject;
			var assetPath = AssetDatabase.GetAssetPath(targetObject);
			var folderPath = string.IsNullOrEmpty(assetPath) ? "Assets" : Path.GetDirectoryName(assetPath);

			// Create the UID
			var uidAsset = CreateUIDAssetInFolder(folderPath, targetObject.name);

			if (uidAsset != null)
			{
				// Assign the created UID to the field
				property.objectReferenceValue = uidAsset;
				property.serializedObject.ApplyModifiedProperties();

				EditorUtility.SetDirty(targetObject);
				EditorUtility.FocusProjectWindow();

				// Select the created asset
				Selection.activeObject = uidAsset;
				EditorGUIUtility.PingObject(uidAsset);
			}
		}

		private static UID CreateUIDAssetInFolder(string folderPath, string targetName)
		{
			// Ensure folder exists
			if (!Directory.Exists(folderPath))
			{
				Directory.CreateDirectory(folderPath);
				AssetDatabase.Refresh();
			}

			// Generate unique asset name based on target object name
			var baseAssetName = $"{targetName}_UID";
			var uidAsset = ScriptableObject.CreateInstance<UID>();

			// Manually generate the GUID to ensure it's not null
			var guidString = System.Guid.NewGuid().ToString();
			var uidType = typeof(UID);
			var idField = uidType.GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance);
			if (idField != null)
			{
				idField.SetValue(uidAsset, guidString);
			}

			// Use target name + UID for filename
			var assetName = baseAssetName;
			var fullPath = Path.Combine(folderPath, $"{assetName}.asset");

			// If file exists, append counter
			var counter = 1;
			while (File.Exists(fullPath))
			{
				assetName = $"{baseAssetName}_{counter}";
				fullPath = Path.Combine(folderPath, $"{assetName}.asset");
				counter++;
			}

			// Create and save the asset
			AssetDatabase.CreateAsset(uidAsset, fullPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			EditorUtility.DisplayDialog("UID Created",
				$"Created UID asset at:\n{fullPath}\n\nGUID: {guidString}",
				"OK");

			return uidAsset;
		}
	}

	// Custom property drawer for UID fields with extra functionality
	[CustomPropertyDrawer(typeof(UID))]
	public class UIDPropertyDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			// Calculate button position
			var buttonWidth = 60f;
			var fieldPosition = new Rect(position.x, position.y, position.width - buttonWidth - 5, position.height);
			var buttonPosition = new Rect(position.x + position.width - buttonWidth, position.y, buttonWidth, position.height);

			// Draw the object field
			EditorGUI.PropertyField(fieldPosition, property, label);

			// Draw "Create" button if null
			if (property.objectReferenceValue == null)
			{
				if (GUI.Button(buttonPosition, "Create"))
				{
					CreateUIDForProperty(property);
				}
			}
			else
			{
				// Draw "Select" button if has value
				if (GUI.Button(buttonPosition, "Select"))
				{
					Selection.activeObject = property.objectReferenceValue;
					EditorGUIUtility.PingObject(property.objectReferenceValue);
				}
			}

			EditorGUI.EndProperty();
		}

		private void CreateUIDForProperty(SerializedProperty property)
		{
			var targetObjects = property.serializedObject.targetObjects;
			var assetPath = AssetDatabase.GetAssetPath(targetObjects[0]);
			var folderPath = string.IsNullOrEmpty(assetPath) ? "Assets" : Path.GetDirectoryName(assetPath);

			// Create UIDs for all selected objects that don't have one
			foreach (var targetObject in targetObjects)
			{
				var targetProperties = new SerializedObject(targetObject).FindProperty(property.propertyPath);
				if (targetProperties.objectReferenceValue == null)
				{
					var uidAsset = CreateUIDAssetInFolder(folderPath, targetObject.name);
					if (uidAsset != null)
					{
						targetProperties.objectReferenceValue = uidAsset;
						targetProperties.serializedObject.ApplyModifiedProperties();
						EditorUtility.SetDirty(targetObject);
					}
				}
			}
		}

		private UID CreateUIDAssetInFolder(string folderPath, string targetName)
		{
			// Ensure folder exists
			if (!Directory.Exists(folderPath))
			{
				Directory.CreateDirectory(folderPath);
				AssetDatabase.Refresh();
			}

			// Generate unique asset name based on target object name
			var baseAssetName = $"{targetName}_UID";
			var uidAsset = ScriptableObject.CreateInstance<UID>();

			// Manually generate the GUID to ensure it's not null
			var guidString = System.Guid.NewGuid().ToString();
			var uidType = typeof(UID);
			var idField = uidType.GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance);
			if (idField != null)
			{
				idField.SetValue(uidAsset, guidString);
			}

			// Use target name + UID for filename
			var assetName = baseAssetName;
			var fullPath = Path.Combine(folderPath, $"{assetName}.asset");

			// If file exists, append counter
			var counter = 1;
			while (File.Exists(fullPath))
			{
				assetName = $"{baseAssetName}_{counter}";
				fullPath = Path.Combine(folderPath, $"{assetName}.asset");
				counter++;
			}

			// Create and save the asset
			AssetDatabase.CreateAsset(uidAsset, fullPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			return uidAsset;
		}
	}
}