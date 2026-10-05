using System;

namespace AK.Kernel.Collections
{
	/// <summary>
	/// Where an item sits in a <see cref="RankedList{T}"/>: higher priorities first, and equal
	/// priorities by <see cref="Order"/>, lowest first. Give each item a unique order that grows
	/// as items are added, and equal priorities keep the order they were added in. Lists that
	/// take their orders from one counter merge in that order too.
	/// </summary>
	public readonly struct Rank : IComparable<Rank>, IEquatable<Rank>
	{
		/// <summary>Ranks before every other: a walk that starts after it starts at the first item.</summary>
		public static readonly Rank BeforeAll = new(float.PositiveInfinity, long.MinValue);

		public readonly float Priority;
		public readonly long  Order;

		/// <exception cref="ArgumentOutOfRangeException"><paramref name="priority"/> is NaN, which has no place in an order.</exception>
		public Rank(float priority, long order)
		{
			if (float.IsNaN(priority))
			{
				throw new ArgumentOutOfRangeException(nameof(priority), priority, "A priority must be a number.");
			}

			Priority = priority;
			Order    = order;
		}

		/// <summary>Negative when this rank comes first, positive when <paramref name="other"/> does.</summary>
		public int CompareTo(Rank other)
		{
			if (Priority > other.Priority) return -1;
			if (Priority < other.Priority) return 1;
			return Order.CompareTo(other.Order);
		}

		public bool Equals(Rank other) => CompareTo(other) == 0;

		public override bool Equals(object obj) => obj is Rank other && Equals(other);

		// Normalized so that 0 and -0, which rank alike, hash alike.
		public override int GetHashCode() => ((Priority == 0f ? 0f : Priority).GetHashCode() * 397) ^ Order.GetHashCode();

		public override string ToString() => $"{Priority}#{Order}";

		public static bool operator ==(Rank a, Rank b) => a.Equals(b);
		public static bool operator !=(Rank a, Rank b) => !a.Equals(b);
	}
}
