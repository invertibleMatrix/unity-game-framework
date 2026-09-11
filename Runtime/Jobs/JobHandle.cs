using System;
using AK.Core.Collections;

namespace AK.Jobs
{
	/// <summary>
	/// Identifies one scheduled job. A value, not an object: copying is free, and a handle whose job
	/// has already completed fails the scheduler's generation check instead of aliasing a later job
	/// that reused the same slot.
	/// </summary>
	public readonly struct JobHandle : IEquatable<JobHandle>
	{
		internal readonly Handle<JobSlot> Slot;

		internal JobHandle(Handle<JobSlot> slot)
		{
			Slot = slot;
		}

		public static readonly JobHandle Invalid = default;

		/// <summary>False for <see cref="Invalid"/>. Does not say whether the job is still pending; ask the scheduler.</summary>
		public bool IsValid => Slot.IsSet;

		public bool Equals(JobHandle other) => Slot == other.Slot;

		public override bool Equals(object obj) => obj is JobHandle other && Equals(other);

		public override int GetHashCode() => Slot.GetHashCode();

		public static bool operator ==(JobHandle a, JobHandle b) => a.Equals(b);
		public static bool operator !=(JobHandle a, JobHandle b) => !a.Equals(b);

		public override string ToString() => IsValid ? $"Job#{Slot.Index}.{Slot.Generation}" : "Job#invalid";
	}
}
