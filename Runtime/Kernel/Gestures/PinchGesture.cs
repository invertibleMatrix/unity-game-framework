using System;
using System.Numerics;

namespace AK.Kernel.Gestures
{
	/// <summary>One step of a <see cref="PinchGesture"/>: a one-pointer drag or a two-pointer pinch.</summary>
	public readonly struct GestureStep : IEquatable<GestureStep>
	{
		/// <summary>No movement: zero drag, neutral spread.</summary>
		public static readonly GestureStep None = new(Vector2.Zero, 1f);

		/// <summary>How far the only pointer moved since the last step. Zero while pinching.</summary>
		public readonly Vector2 Drag;

		/// <summary>
		/// Current pointer separation divided by the previous one: above 1 while the fingers
		/// spread, below 1 while they close, exactly 1 when not pinching. Ratios compose, so
		/// the product of every step's spread is the total change, whatever the frame rate.
		/// </summary>
		public readonly float Spread;

		public GestureStep(Vector2 drag, float spread)
		{
			Drag   = drag;
			Spread = spread;
		}

		public bool Equals(GestureStep other) => Drag.Equals(other.Drag) && Spread.Equals(other.Spread);

		public override bool Equals(object obj) => obj is GestureStep other && Equals(other);

		public override int GetHashCode() => Drag.GetHashCode() * 397 ^ Spread.GetHashCode();

		public override string ToString() => $"Drag {Drag}, Spread x{Spread:0.###}";
	}

	/// <summary>
	/// Turns raw pointer events into drag and pinch steps, for one target.
	///
	/// Feed it only the pointers that went down on the target, so a touch elsewhere on screen
	/// never takes part. The first two pointers are tracked and any further ones are ignored.
	/// With one pointer down, each move is a drag. With two down, each move of either is a pinch
	/// measured as a separation ratio, which keeps zoom independent of resolution and frame rate.
	/// When one of two pointers lifts, the other carries on dragging from where it is, so the
	/// view never jumps. Positions are in any consistent unit, typically screen pixels.
	///
	/// No allocations. Not thread-safe.
	/// </summary>
	public sealed class PinchGesture
	{
		/// <summary>Marks an empty pointer slot. Never a valid pointer id here.</summary>
		public const int NoPointer = int.MinValue;

		/// <summary>Separation floor, so two pointers at one spot can't divide by zero.</summary>
		public const float MinSeparation = 1f;

		private int _firstId  = NoPointer;
		private int _secondId = NoPointer;
		private Vector2 _first;
		private Vector2 _second;
		private float _separation;

		/// <summary>Pointers currently tracked: 0, 1 or 2.</summary>
		public int PointerCount => (_firstId != NoPointer ? 1 : 0) + (_secondId != NoPointer ? 1 : 0);

		public bool IsPinching => _secondId != NoPointer;

		public bool IsTracking(int pointerId) => pointerId != NoPointer && (pointerId == _firstId || pointerId == _secondId);

		/// <summary>
		/// Starts tracking a pointer at <paramref name="position"/>. False when two pointers are
		/// already tracked; that pointer is then ignored until it is pressed again. Pressing a
		/// pointer that is already tracked just moves it, with no step.
		/// </summary>
		public bool Press(int pointerId, Vector2 position)
		{
			if (pointerId == NoPointer)
			{
				throw new ArgumentOutOfRangeException(nameof(pointerId), "int.MinValue is reserved for an empty pointer slot.");
			}

			if (pointerId == _firstId)
			{
				_first = position;
				ResetSeparation();
				return true;
			}

			if (pointerId == _secondId)
			{
				_second = position;
				ResetSeparation();
				return true;
			}

			if (_firstId == NoPointer)
			{
				_firstId = pointerId;
				_first   = position;
				return true;
			}

			if (_secondId == NoPointer)
			{
				_secondId = pointerId;
				_second   = position;
				ResetSeparation();
				return true;
			}

			return false;
		}

		/// <summary>
		/// Moves a tracked pointer to <paramref name="position"/> and returns the resulting step:
		/// a drag while it is the only pointer, a pinch while two are down.
		/// <see cref="GestureStep.None"/> for a pointer that isn't tracked.
		/// </summary>
		public GestureStep Move(int pointerId, Vector2 position)
		{
			Vector2 delta;

			if (pointerId != NoPointer && pointerId == _firstId)
			{
				delta  = position - _first;
				_first = position;
			}
			else if (pointerId != NoPointer && pointerId == _secondId)
			{
				delta   = position - _second;
				_second = position;
			}
			else
			{
				return GestureStep.None;
			}

			if (_secondId == NoPointer)
			{
				return new GestureStep(delta, 1f);
			}

			float previous = _separation;
			_separation = Separation();
			return new GestureStep(Vector2.Zero, _separation / previous);
		}

		/// <summary>
		/// Stops tracking a pointer. If another pointer is still down it becomes the dragging
		/// pointer from its current position. False for a pointer that wasn't tracked.
		/// </summary>
		public bool Release(int pointerId)
		{
			if (pointerId == NoPointer)
			{
				return false;
			}

			if (pointerId == _secondId)
			{
				_secondId = NoPointer;
				return true;
			}

			if (pointerId == _firstId)
			{
				_firstId  = _secondId;
				_first    = _second;
				_secondId = NoPointer;
				return true;
			}

			return false;
		}

		/// <summary>Forgets every pointer, e.g. when the target is disabled mid-gesture.</summary>
		public void Reset()
		{
			_firstId  = NoPointer;
			_secondId = NoPointer;
		}

		private void ResetSeparation()
		{
			if (_secondId != NoPointer)
			{
				_separation = Separation();
			}
		}

		private float Separation() => MathF.Max(Vector2.Distance(_first, _second), MinSeparation);
	}
}
