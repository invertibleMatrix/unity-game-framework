using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Draws a TypeRef field marked with DerivedFromAttribute as a dropdown of all
	/// non-abstract types derived from the attribute's base type, backed by TypeCache
	/// (indexed, so it stays fast as the project grows). Unresolvable stored names
	/// (class renamed) are tinted red with the previous name shown for re-picking.
	/// </summary>
	[CustomPropertyDrawer(typeof(DerivedFromAttribute))]
	public class TypeRefDrawer : PropertyDrawer
	{
		private const string NoneLabel = "(None)";

		private static readonly Dictionary<Type, Type[]> _candidatesCache = new();

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var typed = (DerivedFromAttribute)attribute;
			var nameProp = property.FindPropertyRelative("_typeName");

			if (nameProp == null || typed.BaseType == null)
			{
				EditorGUI.PropertyField(position, property, label, true);
				return;
			}

			Type[] candidates = GetCandidates(typed.BaseType);
			int currentIndex = string.IsNullOrEmpty(nameProp.stringValue)
				? 0
				: Array.FindIndex(candidates, t => t.AssemblyQualifiedName == nameProp.stringValue) + 1;

			bool missing = !string.IsNullOrEmpty(nameProp.stringValue) && currentIndex == 0;

			var labels = new GUIContent[candidates.Length + 1];
			labels[0] = new GUIContent(NoneLabel);
			for (int i = 0; i < candidates.Length; i++)
			{
				labels[i + 1] = new GUIContent(candidates[i].FullName);
			}

			EditorGUI.BeginProperty(position, label, property);

			if (missing)
			{
				label = new GUIContent($"{label.text} (Missing Type)", label.tooltip);
				GUI.color = new Color(1f, 0.6f, 0.6f);
			}

			EditorGUI.BeginChangeCheck();
			int nextIndex = EditorGUI.Popup(position, label, currentIndex, labels);
			if (EditorGUI.EndChangeCheck())
			{
				nameProp.stringValue = nextIndex <= 0
					? string.Empty
					: candidates[nextIndex - 1].AssemblyQualifiedName;
			}

			if (missing)
			{
				GUI.color = Color.white;

				var helpRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
					position.width - EditorGUIUtility.labelWidth, position.height);
				GUI.Label(helpRect, $"was: {ShortName(nameProp.stringValue)}", EditorStyles.miniLabel);
			}

			EditorGUI.EndProperty();
		}

		private static Type[] GetCandidates(Type baseType)
		{
			if (_candidatesCache.TryGetValue(baseType, out Type[] cached))
			{
				return cached;
			}

			var candidates = TypeCache.GetTypesDerivedFrom(baseType)
			                          .Where(t => !t.IsAbstract && !t.IsInterface && !t.IsGenericTypeDefinition)
			                          .OrderBy(t => t.FullName, StringComparer.Ordinal)
			                          .ToArray();

			_candidatesCache[baseType] = candidates;
			return candidates;
		}

		private static string ShortName(string assemblyQualifiedName)
		{
			if (string.IsNullOrEmpty(assemblyQualifiedName)) return string.Empty;

			int comma = assemblyQualifiedName.IndexOf(',');
			string fullName = comma > 0 ? assemblyQualifiedName.Substring(0, comma) : assemblyQualifiedName;
			int dot = fullName.LastIndexOf('.');
			return dot >= 0 ? fullName.Substring(dot + 1) : fullName;
		}
	}
}
