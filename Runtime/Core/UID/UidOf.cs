using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// A <see cref="Uid"/> whose type parameter names the kind of asset it identifies.
	/// Same 16 bytes at runtime; the parameter exists only at compile time, so a
	/// <c>Uid&lt;AudioConfig&gt;</c> cannot be handed to a fact ledger, and the editor
	/// drawer derives its picker filter from T with no attribute.
	///
	/// Use the untyped <see cref="Uid"/> only where identities are genuinely polymorphic
	/// (aggregate resolvers, redirect tables, wire formats) and convert at the boundary.
	/// </summary>
	[Serializable]
	public struct Uid<T> : IEquatable<Uid<T>>, IComparable<Uid<T>> where T : UID
	{
		[SerializeField] private Uid _value;

		public static readonly Uid<T> None = default;

		public Uid(Uid value)
		{
			_value = value;
		}

		public Uid  Value  => _value;
		public bool IsNone => _value.IsNone;
		public bool IsSet  => _value.IsSet;

		/// <summary>Widen to the untyped identity. Explicit by design.</summary>
		public Uid Untyped => _value;

		/// <summary>Reinterpret as another kind. Only for boundary code that has verified the kind.</summary>
		public Uid<TOther> As<TOther>() where TOther : UID => new(_value);

		public static Uid<T> From(Uid value) => new(value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Uid<T> other) => _value.Equals(other._value);

		public override bool Equals(object obj) => obj is Uid<T> other && Equals(other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => _value.GetHashCode();

		public int CompareTo(Uid<T> other) => _value.CompareTo(other._value);

		public override string ToString() => _value.ToString();

		public static bool operator ==(Uid<T> a, Uid<T> b) => a._value.Equals(b._value);
		public static bool operator !=(Uid<T> a, Uid<T> b) => !a._value.Equals(b._value);
	}
}
