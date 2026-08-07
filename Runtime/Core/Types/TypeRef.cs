using System;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// Serializable reference to a Type. Stores the assembly-qualified name and resolves
	/// lazily. Pair with DerivedFromAttribute to constrain the editor dropdown.
	/// Types carry no GUID, so a class rename breaks the reference — the drawer surfaces
	/// unresolvable names in red for re-picking.
	/// </summary>
	[Serializable]
	public class TypeRef
	{
		[SerializeField, HideInInspector] private string _typeName;

		private Type _cached;

		public string TypeName => _typeName;

		public bool IsSet => !string.IsNullOrEmpty(_typeName);

		public Type Value
		{
			get
			{
				if (_cached == null && !string.IsNullOrEmpty(_typeName))
				{
					_cached = Type.GetType(_typeName);
				}

				return _cached;
			}
		}

		public TypeRef() { }

		public TypeRef(Type type)
		{
			_typeName = type?.AssemblyQualifiedName;
		}

		public static implicit operator Type(TypeRef reference) => reference?.Value;
	}

	/// <summary>
	/// Constrains a TypeRef field's editor dropdown to types derived from the given base.
	/// </summary>
	public class DerivedFromAttribute : PropertyAttribute
	{
		public readonly Type BaseType;

		public DerivedFromAttribute(Type baseType)
		{
			BaseType = baseType;
		}
	}
}
