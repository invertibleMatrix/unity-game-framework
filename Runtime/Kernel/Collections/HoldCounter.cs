using System;
using System.Collections.Generic;

namespace AK.Kernel.Collections
{
	/// <summary>
	/// The outstanding holds on one shared thing, such as input, a pause or a loading screen.
	/// The thing stays held while any hold is outstanding (<see cref="IsHeld"/>), and each hold
	/// is ended by its own <see cref="HoldToken"/>, exactly once.
	///
	/// Each hold carries a tag, for diagnostics, and may carry a deadline:
	/// <see cref="ReleaseExpired"/> ends the holds whose deadline has passed, so a holder that
	/// never gets to release can't keep the thing held forever. Deadlines and the time passed
	/// to <see cref="ReleaseExpired"/> are on whatever clock the caller chooses.
	///
	/// The holds live in a <see cref="SlotMap{T}"/>, and a token is its hold's handle there. A
	/// slot's generation advances whenever its hold ends, so a token stops matching the moment
	/// its hold ends: releasing it again, or after <see cref="ReleaseAll"/>, does nothing and
	/// never ends a newer hold that reuses the slot. <c>default(HoldToken)</c> never matches.
	///
	/// Slots are reused, so steady-state use does not allocate. Not thread-safe.
	/// </summary>
	public sealed class HoldCounter<TTag>
	{
		private readonly SlotMap<Hold> _holds;

		public HoldCounter(int capacity = 4)
		{
			_holds = new SlotMap<Hold>(capacity);
		}

		/// <summary>Holds outstanding.</summary>
		public int Count => _holds.Count;

		/// <summary>True while at least one hold is outstanding.</summary>
		public bool IsHeld => _holds.Count > 0;

		/// <summary>
		/// The earliest deadline among the outstanding holds; <see cref="double.PositiveInfinity"/>
		/// when none of them has one.
		/// </summary>
		public double NextDeadline
		{
			get
			{
				double next = double.PositiveInfinity;
				if (_holds.Count == 0) return next;

				foreach (Hold hold in _holds)
				{
					if (hold.Deadline < next) next = hold.Deadline;
				}

				return next;
			}
		}

		/// <summary>Takes a hold with no deadline.</summary>
		public HoldToken Acquire(TTag tag) => Acquire(tag, double.PositiveInfinity);

		/// <summary>
		/// Takes a hold that <see cref="ReleaseExpired"/> ends once its clock reaches
		/// <paramref name="deadline"/>. <see cref="double.PositiveInfinity"/> means no deadline.
		/// </summary>
		/// <exception cref="ArgumentException"><paramref name="deadline"/> is NaN.</exception>
		public HoldToken Acquire(TTag tag, double deadline)
		{
			if (double.IsNaN(deadline))
			{
				throw new ArgumentException("A hold's deadline can't be NaN.", nameof(deadline));
			}

			Handle<Hold> handle = _holds.Add(new Hold(tag, deadline));
			return new HoldToken(handle.Index, handle.Generation);
		}

		/// <summary>True while the hold <paramref name="token"/> names is outstanding.</summary>
		public bool IsLive(HoldToken token) => _holds.Contains(HandleOf(token));

		/// <summary>The tag of the hold <paramref name="token"/> names, while that hold is outstanding.</summary>
		public bool TryGetTag(HoldToken token, out TTag tag)
		{
			if (_holds.TryGet(HandleOf(token), out Hold hold))
			{
				tag = hold.Tag;
				return true;
			}

			tag = default;
			return false;
		}

		/// <summary>
		/// Ends the hold <paramref name="token"/> names. True when this call ended it; false when
		/// it had already ended (released, expired, or ended by <see cref="ReleaseAll"/>) or the
		/// token names no hold. Never throws: a late release is an expected case.
		/// </summary>
		public bool Release(HoldToken token) => _holds.Remove(HandleOf(token));

		/// <summary>
		/// Ends every hold whose deadline is at or before <paramref name="now"/>, appending their
		/// tags to <paramref name="expired"/> when one is given. Holds without a deadline never
		/// expire. Returns how many holds ended.
		/// </summary>
		/// <exception cref="ArgumentException"><paramref name="now"/> is NaN.</exception>
		public int ReleaseExpired(double now, List<TTag> expired = null)
		{
			if (double.IsNaN(now))
			{
				throw new ArgumentException("The time can't be NaN.", nameof(now));
			}

			if (_holds.Count == 0) return 0;

			int ended = 0;
			for (SlotMap<Hold>.Enumerator holds = _holds.GetEnumerator(); holds.MoveNext();)
			{
				Hold hold = holds.Current;
				if (hold.Deadline > now || double.IsPositiveInfinity(hold.Deadline)) continue;

				expired?.Add(hold.Tag);
				_holds.Remove(holds.CurrentHandle);
				ended++;
			}

			return ended;
		}

		/// <summary>
		/// Ends every hold, appending their tags to <paramref name="released"/> when one is given.
		/// Every outstanding token becomes stale. Returns how many holds ended.
		/// </summary>
		public int ReleaseAll(List<TTag> released = null)
		{
			if (_holds.Count == 0) return 0;

			int ended = 0;
			for (SlotMap<Hold>.Enumerator holds = _holds.GetEnumerator(); holds.MoveNext();)
			{
				released?.Add(holds.Current.Tag);
				_holds.Remove(holds.CurrentHandle);
				ended++;
			}

			return ended;
		}

		/// <summary>Appends the tag of every outstanding hold to <paramref name="destination"/>, in slot order.</summary>
		public void CopyTagsTo(List<TTag> destination)
		{
			if (destination == null)
			{
				throw new ArgumentNullException(nameof(destination));
			}

			foreach (Hold hold in _holds)
			{
				destination.Add(hold.Tag);
			}
		}

		private static Handle<Hold> HandleOf(HoldToken token) => new(token.Slot, token.Generation);

		private readonly struct Hold
		{
			public readonly TTag   Tag;
			public readonly double Deadline;

			public Hold(TTag tag, double deadline)
			{
				Tag      = tag;
				Deadline = deadline;
			}
		}
	}
}
