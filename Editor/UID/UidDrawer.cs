using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Inspector for <see cref="Uid"/> and <see cref="Uid{T}"/> fields. The field shows the
	/// asset that currently owns the identity (or the raw hex when none does), an object
	/// slot filtered to the kind the field accepts, and a copy button.
	///
	/// The filter comes from the field's static type: <c>Uid&lt;AudioConfig&gt;</c> offers only
	/// AudioConfig assets; an untyped Uid uses <see cref="UidOfAttribute"/> when present and
	/// falls back to every UID asset. Dropping an asset stores its Uid — never a reference —
	/// so the owning object stays free of hard asset links.
	/// </summary>
	[CustomPropertyDrawer(typeof(Uid))]
	[CustomPropertyDrawer(typeof(Uid<>))]
	[CustomPropertyDrawer(typeof(UidOfAttribute))]
	public sealed class UidDrawer : PropertyDrawer
	{
		private const float ButtonWidth = 24f;
		private const float Spacing     = 2f;

		private static readonly Dictionary<string, Type> _kindCache = new();

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return EditorGUIUtility.singleLineHeight;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			SerializedProperty valueProperty = FindValueString(property);
			if (valueProperty == null)
			{
				EditorGUI.LabelField(position, label, new GUIContent("Uid: unsupported layout"));
				return;
			}

			Type kind = ResolveKind(property);

			label = EditorGUI.BeginProperty(position, label, property);

			Rect field = EditorGUI.PrefixLabel(position, label);
			Rect copy  = new(field.xMax - ButtonWidth, field.y, ButtonWidth, field.height);
			Rect slot  = new(field.x, field.y, field.width - ButtonWidth - Spacing, field.height);

			Uid current = Uid.TryParse(valueProperty.stringValue, out Uid parsed) ? parsed : Uid.None;
			UID owner   = FindOwner(current, kind);

			bool dangling = current.IsSet && owner == null;

			Color previous = GUI.color;
			if (dangling) GUI.color = new Color(1f, 0.75f, 0.4f);

			EditorGUI.BeginChangeCheck();
			var picked = (UID)EditorGUI.ObjectField(slot, GUIContent.none, owner, kind, false);
			if (EditorGUI.EndChangeCheck())
			{
				if (picked == null)
				{
					valueProperty.stringValue = string.Empty;
				}
				else if (!picked.HasIdentity)
				{
					Debug.LogError($"[UID] '{picked.name}' has no identity and cannot be referenced by value. Run Tools → UGFW → UID → Audit.", picked);
				}
				else
				{
					valueProperty.stringValue = picked.Id.ToString();
				}
			}

			GUI.color = previous;

			if (dangling)
			{
				var hint = new GUIContent(string.Empty, $"Identity {current} is not owned by any {kind.Name} asset in the project.\nIt may have been deleted, or live in another branch. Consider a redirect.");
				EditorGUI.LabelField(slot, hint);
				DrawDanglingOverlay(slot, current);
			}

			using (new EditorGUI.DisabledScope(current.IsNone))
			{
				if (GUI.Button(copy, new GUIContent("⧉", current.IsSet ? current.ToString() : "No identity"), EditorStyles.miniButton))
				{
					UidEditorUtility.CopyToClipboard(current.ToString());
				}
			}

			EditorGUI.EndProperty();
		}

		private static void DrawDanglingOverlay(Rect slot, Uid id)
		{
			var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.9f, 0.55f, 0.2f) } };
			var text  = new Rect(slot.x + 18f, slot.y, slot.width - 40f, slot.height);
			GUI.Label(text, $"dangling {id.ToShortString()}…", style);
		}

		// ---------------------------------------------------------------- layout

		/// <summary>
		/// Uid serializes as { _value: string }; Uid&lt;T&gt; wraps it as { _value: { _value: string } }.
		/// Walk down until the string is reached.
		/// </summary>
		private static SerializedProperty FindValueString(SerializedProperty property)
		{
			SerializedProperty inner = property;
			for (int depth = 0; depth < 3 && inner != null; depth++)
			{
				if (inner.propertyType == SerializedPropertyType.String) return inner;
				inner = inner.FindPropertyRelative("_value");
			}

			return null;
		}

		// ---------------------------------------------------------------- kind

		private Type ResolveKind(SerializedProperty property)
		{
			if (attribute is UidOfAttribute of && of.Kind != null && typeof(UID).IsAssignableFrom(of.Kind))
			{
				return of.Kind;
			}

			Type fieldType = fieldInfo?.FieldType;
			if (fieldType != null)
			{
				Type element = ElementTypeOf(fieldType);
				if (element != null && element.IsGenericType && element.GetGenericTypeDefinition() == typeof(Uid<>))
				{
					return element.GetGenericArguments()[0];
				}
			}

			string key = property.propertyPath;
			if (_kindCache.TryGetValue(key, out Type cached)) return cached;

			Type resolved = FindKindByReflection(property) ?? typeof(UID);
			_kindCache[key] = resolved;
			return resolved;
		}

		private static Type ElementTypeOf(Type fieldType)
		{
			if (fieldType.IsArray) return fieldType.GetElementType();
			if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>)) return fieldType.GetGenericArguments()[0];
			return fieldType;
		}

		/// <summary>Walks the serialized path when fieldInfo is unavailable (nested arrays inside managed references).</summary>
		private static Type FindKindByReflection(SerializedProperty property)
		{
			Type current = property.serializedObject.targetObject.GetType();
			string[] parts = property.propertyPath.Replace(".Array.data[", "[").Split('.');

			foreach (string rawPart in parts)
			{
				string part = rawPart;
				int bracket = part.IndexOf('[');
				bool indexed = bracket >= 0;
				if (indexed) part = part.Substring(0, bracket);

				FieldInfo field = null;
				for (Type t = current; t != null && field == null; t = t.BaseType)
				{
					field = t.GetField(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}

				if (field == null) return null;

				current = field.FieldType;
				if (indexed) current = ElementTypeOf(current);
			}

			if (current != null && current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Uid<>))
			{
				return current.GetGenericArguments()[0];
			}

			return null;
		}

		// ---------------------------------------------------------------- owner

		private static UID FindOwner(Uid id, Type kind)
		{
			if (id.IsNone || !UidEditorIndex.TryGetOwners(id, out var owners)) return null;

			UID fallback = null;
			foreach (var owner in owners)
			{
				var asset = AssetDatabase.LoadAssetAtPath<UID>(owner.AssetPath);
				if (asset == null) continue;
				if (kind.IsInstanceOfType(asset)) return asset;
				fallback ??= asset;
			}

			return fallback;
		}
	}
}
