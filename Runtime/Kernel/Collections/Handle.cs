using System;
using System.Runtime.CompilerServices;

namespace AK.Kernel.Collections
{
	/// <summary>
	/// A generational reference into a <see cref="SlotMap{T}"/>: which slot, and which
	/// lifetime of that slot. When the slot is freed and reused the generation advances, so a
	/// handle kept past its object's death stops resolving instead of silently pointing at the
	/// new occupant. That is the whole reason to hand these out instead of raw references.
	///
	/// Eight bytes, no heap, value-equal. <c>default</c> is <see cref="Invalid"/>.
	/// </summary>
	public readonly struct Handle<T> : IEquatable<Handle<T>>
	{
		public readonly int  Index;
		public readonly uint Generation;

		/// <summary>Never resolves. Generation zero is reserved so that <c>default</c> is invalid.</summary>
		public static readonly Handle<T> Invalid = default;

		internal Handle(int index, uint generation)
		{
			Index      = index;
			Generation = generation;
		}

		/// <summary>
		/// False for <see cref="Invalid"/>. True does not mean the object is still alive — only
		/// the owning <see cref="SlotMap{T}"/> knows that; ask it with <c>Contains</c> or <c>TryGet</c>.
		/// </summary>
		public bool IsSet => Generation != 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Handle<T> other) => Index == other.Index && Generation == other.Generation;

		public override bool Equals(object obj) => obj is Handle<T> other && Equals(other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => (int)(((uint)Index * 0x9E3779B1u) ^ Generation);

		public static bool operator ==(Handle<T> a, Handle<T> b) => a.Equals(b);
		public static bool operator !=(Handle<T> a, Handle<T> b) => !a.Equals(b);

		public override string ToString() => IsSet ? $"{typeof(T).Name}#{Index}.{Generation}" : $"{typeof(T).Name}#invalid";
	}
}
