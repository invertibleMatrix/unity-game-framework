using System;
using AK.Systems;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace AK.Tests
{
	/// <summary>
	/// The input gate's contract: a hold blocks input until it is released, exactly once, and
	/// releasing it can never end anyone else's hold; a hold with a timeout ends by itself on
	/// the gate's unscaled clock, with a warning naming its owner.
	/// </summary>
	public class UIInputGateTests
	{
		private UIInputGate _gate;

		[SetUp] public void SetUp() => _gate = new UIInputGate();

		/// <summary>Advances the gate's clock frame by frame, in steps it takes whole.</summary>
		private void Advance(float seconds)
		{
			while (seconds > 0f)
			{
				float step = Math.Min(seconds, UIInputGate.MaxFrameGapSeconds);
				_gate.Tick(step);
				seconds -= step;
			}
		}

		// ---------------------------------------------------------------
		// Holding and releasing
		// ---------------------------------------------------------------

		[Test]
		public void NewGate_HoldsNothing()
		{
			Assert.That(_gate.IsHeld, Is.False);
			Assert.That(_gate.HoldCount, Is.EqualTo(0));
			Assert.That(_gate.DescribeHolders(), Is.EqualTo("none"));
		}

		[Test]
		public void Hold_BlocksInput_UntilItIsReleased_Once()
		{
			InputHold hold = _gate.Hold("owner");

			Assert.That(_gate.IsHeld, Is.True);
			Assert.That(hold.IsSet, Is.True);
			Assert.That(hold.IsHeld, Is.True);

			Assert.That(hold.Release(), Is.True, "the first release ends the hold");
			Assert.That(_gate.IsHeld, Is.False);
			Assert.That(hold.IsHeld, Is.False);
			Assert.That(hold.IsSet, Is.True, "a hold the gate handed out stays set after it ends");

			Assert.That(hold.Release(), Is.False, "a second release does nothing");
		}

		[Test]
		public void NestedHolds_KeepInputBlocked_UntilEveryHolderReleases()
		{
			InputHold first  = _gate.Hold("first");
			InputHold second = _gate.Hold("second");

			first.Release();
			first.Release();

			Assert.That(_gate.IsHeld, Is.True, "releasing one hold twice can't end the other");
			Assert.That(second.IsHeld, Is.True);
			Assert.That(_gate.HoldCount, Is.EqualTo(1));

			second.Release();

			Assert.That(_gate.IsHeld, Is.False);
		}

		[Test]
		public void ACopyOfAHold_EndsTheSameHold_AndAnEndedHold_CannotEndTheOneThatReusedItsSlot()
		{
			InputHold hold = _gate.Hold("original");
			InputHold copy = hold;

			Assert.That(copy.Release(), Is.True);
			Assert.That(hold.IsHeld, Is.False, "the copy ended the original");

			InputHold next = _gate.Hold("next");

			Assert.That(hold.Release(), Is.False);
			Assert.That(next.IsHeld, Is.True, "an ended hold is stale, even though its slot was reused");
		}

		[Test]
		public void DefaultHold_HoldsNothing_AndReleasesNothing()
		{
			InputHold other = _gate.Hold("other");
			InputHold none  = default;

			Assert.That(none.IsSet, Is.False);
			Assert.That(none.IsHeld, Is.False);
			Assert.That(none.Release(), Is.False);
			none.Dispose();

			Assert.That(other.IsHeld, Is.True);
		}

		[Test]
		public void UsingScope_ReleasesTheHold()
		{
			using (_gate.Hold("scope"))
			{
				Assert.That(_gate.IsHeld, Is.True);
			}

			Assert.That(_gate.IsHeld, Is.False);
		}

		[Test]
		public void ReleaseAll_EndsEveryHold_AndTheEndedHoldsCannotEndNewOnes()
		{
			InputHold first  = _gate.Hold("first");
			InputHold second = _gate.Hold("second", 5f);

			_gate.ReleaseAll();

			Assert.That(_gate.IsHeld, Is.False);
			Assert.That(first.IsHeld || second.IsHeld, Is.False);

			InputHold next = _gate.Hold("next");

			Assert.That(first.Release(), Is.False);
			Assert.That(second.Release(), Is.False);
			Assert.That(next.IsHeld, Is.True);
		}

		[Test]
		public void DescribeHolders_NamesEveryOutstandingHolder()
		{
			InputHold named = _gate.Hold("Spotlight");
			_gate.Hold(null);

			Assert.That(_gate.DescribeHolders(), Is.EqualTo("Spotlight, (unnamed)"));

			named.Release();

			Assert.That(_gate.DescribeHolders(), Is.EqualTo("(unnamed)"));
		}

		// ---------------------------------------------------------------
		// Timeouts
		// ---------------------------------------------------------------

		[Test]
		public void Timeout_EndsTheHold_WithAWarningNamingItsOwner()
		{
			InputHold hold = _gate.Hold("Spotlight intro", 1f);

			Advance(0.75f);
			Assert.That(hold.IsHeld, Is.True, "held until the timeout runs out");

			using (ExpectedLog.Warning("'Spotlight intro' timed out"))
			{
				Advance(0.25f);
			}

			Assert.That(hold.IsHeld, Is.False);
			Assert.That(_gate.IsHeld, Is.False);
			Assert.That(hold.Release(), Is.False, "the holder's late release does nothing");
		}

		[Test]
		public void Timeout_CountsFromTheHold_NotFromTheGatesFirstTick()
		{
			Advance(5f);
			InputHold hold = _gate.Hold("late", 1f);

			Advance(0.75f);
			Assert.That(hold.IsHeld, Is.True);

			using (ExpectedLog.Warning("'late' timed out"))
			{
				Advance(0.25f);
			}

			Assert.That(hold.IsHeld, Is.False);
		}

		[Test]
		public void Timeouts_EndOnlyTheHoldsThatRanOut()
		{
			InputHold shorter  = _gate.Hold("shorter", 1f);
			InputHold longer   = _gate.Hold("longer", 2f);
			InputHold untimed  = _gate.Hold("untimed");

			using (ExpectedLog.Warning("'shorter' timed out"))
			{
				Advance(1f);
			}

			Assert.That(shorter.IsHeld, Is.False);
			Assert.That(longer.IsHeld && untimed.IsHeld, Is.True);

			using (ExpectedLog.Warning("'longer' timed out"))
			{
				Advance(1f);
			}

			Assert.That(longer.IsHeld, Is.False);
			Assert.That(untimed.IsHeld, Is.True);

			Advance(1000f);

			Assert.That(untimed.IsHeld, Is.True, "a hold without a timeout lasts until it is released");
		}

		[Test]
		public void ReleasedHold_NeverTimesOut()
		{
			InputHold hold = _gate.Hold("released", 1f);
			hold.Release();

			using var log = new LogRecorder();
			Advance(2f);

			Assert.That(log.Count(LogType.Warning, "timed out"), Is.EqualTo(0));
		}

		[Test]
		public void ZeroTimeout_EndsTheHold_AtTheNextTick()
		{
			InputHold hold = _gate.Hold("instant", 0f);

			Assert.That(hold.IsHeld, Is.True, "nothing ends between ticks");

			using (ExpectedLog.Warning("'instant' timed out"))
			{
				_gate.Tick(0f);
			}

			Assert.That(hold.IsHeld, Is.False);
		}

		[Test]
		public void InfiniteTimeout_NeverEnds()
		{
			InputHold hold = _gate.Hold("forever", float.PositiveInfinity);

			Advance(1000f);

			Assert.That(hold.IsHeld, Is.True);
		}

		[TestCase(-1f)]
		[TestCase(float.NegativeInfinity)]
		[TestCase(float.NaN)]
		public void Hold_RejectsATimeoutThatIsNegativeOrNaN(float timeout)
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => _gate.Hold("bad", timeout));
			Assert.That(_gate.IsHeld, Is.False, "a rejected hold holds nothing");
		}

		[TestCase(-1f)]
		[TestCase(float.NegativeInfinity)]
		[TestCase(float.NaN)]
		public void Tick_LeavesTheClock_ForADeltaThatIsNegativeOrNaN(float delta)
		{
			InputHold hold = _gate.Hold("owner", 0.25f);

			_gate.Tick(delta);
			_gate.Tick(0.125f);

			Assert.That(hold.IsHeld, Is.True, "the clock neither jumped nor ran back");

			using (ExpectedLog.Warning("'owner' timed out"))
			{
				_gate.Tick(0.125f);
			}
		}

		[TestCase(1f)]
		[TestCase(3600f)]
		[TestCase(float.PositiveInfinity)]
		public void ALongFrame_CountsAsTheMaxFrameGap(float delta)
		{
			InputHold hold = _gate.Hold("owner", 2f * UIInputGate.MaxFrameGapSeconds);

			_gate.Tick(delta);
			Assert.That(hold.IsHeld, Is.True, "a stall can't run the hold out in one frame");

			using (ExpectedLog.Warning("'owner' timed out"))
			{
				_gate.Tick(delta);
			}
		}

		// ---------------------------------------------------------------
		// Cost
		// ---------------------------------------------------------------

		[Test]
		public void HoldTickRelease_DoNotAllocate_InSteadyState()
		{
			void Frame()
			{
				InputHold hold = _gate.Hold("owner", 1f);
				_gate.Tick(0.016f);
				hold.Release();
				_gate.Tick(0.016f);
			}

			Frame(); // the first pass compiles the path

			int allocations = GcAllocations.Count(() =>
			{
				for (int i = 0; i < 100; i++) Frame();
			});

			Assert.That(allocations, Is.EqualTo(0));
		}
	}
}
