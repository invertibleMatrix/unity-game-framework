using System;
using System.Runtime.CompilerServices;

namespace AK.Kernel.Collections
{
	/// <summary>
	/// Names one hold taken from a <see cref="HoldCounter{TTag}"/>: which slot, and which
	/// lifetime of that slot. The slot's generation advances when the hold ends, so the token
	/// stops matching then and can never end a later hold that reuses the slot.
	///
	/// A token means something only to the counter that issued it. Eight bytes, no heap,
	/// value-equal. <c>default</c> names no hold.
	/// </summary>
	public readonly struct HoldToken : IEquatable<HoldToken>
	{
		public readonly int  Slot;
		public readonly uint Generation;

		internal HoldToken(int slot, uint generation)
		{
			Slot       = slot;
			Generation = generation;
		}

		/// <summary>
		/// False for <c>default</c>. True doesn't mean the hold is still outstanding; only the
		/// counter that issued the token knows that.
		/// </summary>
		public bool IsSet => Generation != 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(HoldToken other) => Slot == other.Slot && Generation == other.Generation;

		public override bool Equals(object obj) => obj is HoldToken other && Equals(other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => (int)(((uint)Slot * 0x9E3779B1u) ^ Generation);

		public static bool operator ==(HoldToken a, HoldToken b) => a.Equals(b);
		public static bool operator !=(HoldToken a, HoldToken b) => !a.Equals(b);

		public override string ToString() => IsSet ? $"Hold#{Slot}.{Generation}" : "Hold#none";
	}
}
