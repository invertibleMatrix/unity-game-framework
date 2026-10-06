using System;
using System.Numerics;
using AK.Kernel.Gestures;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class PinchGestureTests
	{
		private const float Tolerance = 1e-5f;

		[Test]
		public void New_TracksNothing()
		{
			var gesture = new PinchGesture();

			Assert.AreEqual(0, gesture.PointerCount);
			Assert.IsFalse(gesture.IsPinching);
			Assert.AreEqual(GestureStep.None, gesture.Move(0, new Vector2(5f, 5f)));
		}

		[Test]
		public void OnePointer_MovesAreDrags()
		{
			var gesture = new PinchGesture();
			gesture.Press(0, new Vector2(10f, 10f));

			GestureStep step = gesture.Move(0, new Vector2(13f, 14f));

			Assert.AreEqual(new Vector2(3f, 4f), step.Drag);
			Assert.AreEqual(1f, step.Spread);

			step = gesture.Move(0, new Vector2(13f, 10f));
			Assert.AreEqual(new Vector2(0f, -4f), step.Drag, "each step is relative to the last one");
		}

		[Test]
		public void TwoPointers_MovesArePinches_MeasuredAsSeparationRatio()
		{
			var gesture = new PinchGesture();
			gesture.Press(0, new Vector2(0f, 0f));
			gesture.Press(1, new Vector2(100f, 0f));

			GestureStep spread = gesture.Move(1, new Vector2(200f, 0f));
			Assert.AreEqual(Vector2.Zero, spread.Drag, "no drag while pinching");
			Assert.AreEqual(2f, spread.Spread, Tolerance);

			GestureStep close = gesture.Move(0, new Vector2(100f, 0f));
			Assert.AreEqual(0.5f, close.Spread, Tolerance);
		}

		[Test]
		public void PinchSteps_Compose_IntoTheTotalRatio()
		{
			var gesture = new PinchGesture();
			gesture.Press(0, new Vector2(0f, 0f));
			gesture.Press(1, new Vector2(50f, 0f));

			float product = 1f;
			for (int i = 1; i <= 10; i++)
			{
				product *= gesture.Move(i % 2, i % 2 == 0 ? new Vector2(-5f * i, 0f) : new Vector2(50f + 7f * i, 0f)).Spread;
			}

			// Final positions: pointer 0 at -50, pointer 1 at 50 + 63 = 113.
			Assert.AreEqual((113f + 50f) / 50f, product, 1e-4f);
		}

		[Test]
		public void ThirdPointer_IsIgnored()
		{
			var gesture = new PinchGesture();
			gesture.Press(0, new Vector2(0f, 0f));
			gesture.Press(1, new Vector2(10f, 0f));

			Assert.IsFalse(gesture.Press(2, new Vector2(50f, 50f)));
			Assert.IsFalse(gesture.IsTracking(2));
			Assert.AreEqual(GestureStep.None, gesture.Move(2, new Vector2(60f, 60f)));
			Assert.IsFalse(gesture.Release(2));
			Assert.AreEqual(2, gesture.PointerCount);
		}

		[Test]
		public void FirstPointerLifts_SecondDragsOn_WithoutAJump()
		{
			var gesture = new PinchGesture();
			gesture.Press(0, new Vector2(0f, 0f));
			gesture.Press(1, new Vector2(100f, 0f));

			Assert.IsTrue(gesture.Release(0));
			Assert.IsFalse(gesture.IsPinching);

			GestureStep step = gesture.Move(1, new Vector2(110f, 5f));
			Assert.AreEqual(new Vector2(10f, 5f), step.Drag);
			Assert.AreEqual(1f, step.Spread);
		}

		[Test]
		public void SecondPointerLifts_FirstDragsOn()
		{
			var gesture = new PinchGesture();
			gesture.Press(0, new Vector2(0f, 0f));
			gesture.Press(1, new Vector2(100f, 0f));

			Assert.IsTrue(gesture.Release(1));

			Assert.AreEqual(new Vector2(5f, 0f), gesture.Move(0, new Vector2(5f, 0f)).Drag);
		}

		[Test]
		public void PressingATrackedPointer_JustMovesIt()
		{
			var gesture = new PinchGesture();
			gesture.Press(0, new Vector2(0f, 0f));

			Assert.IsTrue(gesture.Press(0, new Vector2(50f, 0f)));
			Assert.AreEqual(1, gesture.PointerCount);
			Assert.AreEqual(new Vector2(10f, 0f), gesture.Move(0, new Vector2(60f, 0f)).Drag);
		}

		[Test]
		public void CoincidentPointers_StayFinite()
		{
			var gesture = new PinchGesture();
			gesture.Press(0, new Vector2(3f, 3f));
			gesture.Press(1, new Vector2(3f, 3f));

			float spread = gesture.Move(1, new Vector2(5f, 3f)).Spread;

			Assert.IsFalse(float.IsNaN(spread) || float.IsInfinity(spread));
			Assert.AreEqual(2f / PinchGesture.MinSeparation, spread, Tolerance);
		}

		[Test]
		public void ReservedPointerId_IsRejected()
		{
			var gesture = new PinchGesture();

			Assert.Throws<ArgumentOutOfRangeException>(() => gesture.Press(PinchGesture.NoPointer, Vector2.Zero));
			Assert.IsFalse(gesture.Release(PinchGesture.NoPointer));
			Assert.AreEqual(GestureStep.None, gesture.Move(PinchGesture.NoPointer, Vector2.One));
		}

		[Test]
		public void Reset_ForgetsEveryPointer()
		{
			var gesture = new PinchGesture();
			gesture.Press(0, Vector2.Zero);
			gesture.Press(1, Vector2.One);

			gesture.Reset();

			Assert.AreEqual(0, gesture.PointerCount);
			Assert.AreEqual(GestureStep.None, gesture.Move(0, new Vector2(9f, 9f)));
		}

		[Test]
		public void Steps_DoNotAllocate()
		{
			var gesture = new PinchGesture();

			int allocations = GcAllocations.Count(() =>
			{
				for (int i = 0; i < 100; i++)
				{
					gesture.Press(0, new Vector2(i, 0f));
					gesture.Move(0, new Vector2(i + 1f, 0f));
					gesture.Press(1, new Vector2(i + 50f, 0f));
					gesture.Move(1, new Vector2(i + 60f, 0f));
					gesture.Release(0);
					gesture.Release(1);
				}
			});

			Assert.AreEqual(0, allocations);
		}
	}
}
