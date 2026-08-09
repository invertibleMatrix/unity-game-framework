using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// Constrains a UID-typed field's object picker to assets assignable to the given type,
	/// while the field still serializes a plain UID reference. Use when the field must stay
	/// UID-typed (framework code that cannot name the concrete type).
	/// </summary>
	public class UIDOfTypeAttribute : PropertyAttribute
	{
		public readonly System.Type Type;

		public UIDOfTypeAttribute(System.Type type)
		{
			Type = type;
		}
	}
}
