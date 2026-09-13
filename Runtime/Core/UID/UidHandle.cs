using System;
using System.Runtime.CompilerServices;

namespace AK.Core
{
	/// <summary>
	/// A resolved identity with a dense slot into its registry's object array. Hot systems
	/// (spawner pools, camera stacks) store this instead of the asset or a string: the slot
	/// makes a lookup one bounds check and one load, and the embedded Uid plus registry
	/// version let the registry detect a stale handle and re-resolve it once, silently.
	/// </summary>
	public readonly struct UidHandle<T> : IEquatable<UidHandle<T>> where T : UID
	{
		public readonly Uid<T> Id;
		public readonly int    Slot;
		public readonly int    Version;

		public static readonly UidHandle<T> Invalid = new(Uid<T>.None, -1, 0);

		public UidHandle(Uid<T> id, int slot, int version)
		{
			Id      = id;
			Slot    = slot;
			Version = version;
		}

		public bool IsValid => Slot >= 0 && Id.IsSet;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(UidHandle<T> other) => Id.Equals(other.Id);

		public override bool Equals(object obj) => obj is UidHandle<T> other && Equals(other);

		public override int GetHashCode() => Id.GetHashCode();

		public override string ToString() => IsValid ? $"{Id.Value.ToShortString()}@{Slot}" : "invalid";
	}
}
