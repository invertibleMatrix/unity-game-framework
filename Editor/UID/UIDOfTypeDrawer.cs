using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Draws a UID-typed field marked with UIDOfTypeAttribute as an object field filtered
	/// to the attribute's type, while the property keeps serializing a UID reference.
	/// </summary>
	[CustomPropertyDrawer(typeof(UIDOfTypeAttribute))]
	public class UIDOfTypeDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var typed = (UIDOfTypeAttribute)attribute;

			// List/array roots keep the default list UI; the constraint reaches the
			// element drawers through the field's attributes.
			if (property.isArray)
			{
				EditorGUI.PropertyField(position, property, label, true);
				return;
			}

			// UIDRef fields are GUID links — the shared UIDRef GUI handles them,
			// using this attribute's type as the picker filter.
			if (typeof(UIDRef).IsAssignableFrom(fieldInfo.FieldType))
			{
				UIDRefGUI.Draw(position, property, label, typed.Type ?? typeof(UID));
				return;
			}

			if (property.propertyType != SerializedPropertyType.ObjectReference ||
			    typed.Type == null || !typeof(UID).IsAssignableFrom(typed.Type))
			{
				EditorGUI.PropertyField(position, property, label);
				return;
			}

			EditorGUI.BeginProperty(position, label, property);
			EditorGUI.BeginChangeCheck();
			var newValue = EditorGUI.ObjectField(position, label, property.objectReferenceValue, typed.Type, false);
			if (EditorGUI.EndChangeCheck())
			{
				property.objectReferenceValue = newValue;
			}
			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (property.isArray)
			{
				return EditorGUI.GetPropertyHeight(property, label, true);
			}

			return EditorGUIUtility.singleLineHeight;
		}
	}
}
