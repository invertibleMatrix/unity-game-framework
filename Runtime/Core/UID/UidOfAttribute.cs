using System;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// Constrains the editor picker for an untyped <see cref="Uid"/> field to assets of the
	/// given kind. Prefer <see cref="Uid{T}"/>, which carries the constraint in the type;
	/// use this only where the field must stay untyped (polymorphic slots, lists mixing kinds).
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class UidOfAttribute : PropertyAttribute
	{
		public readonly Type Kind;

		public UidOfAttribute(Type kind)
		{
			Kind = kind;
		}
	}
}
