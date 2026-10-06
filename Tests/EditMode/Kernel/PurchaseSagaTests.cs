using System;
using System.Collections.Generic;
using AK.Kernel.Purchasing;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class PurchaseSagaTests
	{
		// ---------------------------------------------------------------- a run by the book

		[Test]
		public void ACurrencyPurchase_IsPaid_ThenGrantsEachReward_ThenIsCredited()
		{
			var world = new World(PurchaseSaga.Unpaid(2));

			world.Run();

			CollectionAssert.AreEqual(new[] { PurchaseStep.Pay, PurchaseStep.Grant, PurchaseStep.Grant, PurchaseStep.Credit }, world.Steps);
			Assert.AreEqual(PurchaseSaga.Restore(PurchaseStage.Credited, 2, 2), world.Saved);
			Assert.AreEqual(1, world.Payments);
			CollectionAssert.AreEqual(new[] { 0, 1 }, world.Grants);
		}

		[Test]
		public void AStorePurchase_StartsOwed_SoItIsNeverChargedHere()
		{
			var world = new World(PurchaseSaga.Owed(1));

			world.Run();

			CollectionAssert.AreEqual(new[] { PurchaseStep.Grant, PurchaseStep.Credit }, world.Steps);
			Assert.AreEqual(0, world.Payments);
			Assert.AreEqual(PurchaseStage.Credited, world.Saved.Stage);
		}

		[Test]
		public void APurchaseWithoutRewards_IsCreditedOncePaid()
		{
			var world = new World(PurchaseSaga.Unpaid(0));

			world.Run();

			CollectionAssert.AreEqual(new[] { PurchaseStep.Pay, PurchaseStep.Credit }, world.Steps);
			Assert.AreEqual(PurchaseStage.Credited, world.Saved.Stage);
		}

		[Test]
		public void ADeclinedPayment_AbandonsThePurchase_AndGrantsNothing()
		{
			var world = new World(PurchaseSaga.Unpaid(2)) { DeclinePayment = true };

			world.Run();

			Assert.AreEqual(PurchaseStage.Abandoned, world.Saved.Stage);
			Assert.IsTrue(world.Saved.IsDone);
			Assert.AreEqual(PurchaseStep.None, world.Saved.Next);
			Assert.AreEqual(0, world.Payments);
			CollectionAssert.IsEmpty(world.Grants);
		}

		[Test]
		public void AFailedGrant_EndsTheRun_AndTheNextRunResumesFromThatReward()
		{
			var world = new World(PurchaseSaga.Unpaid(3)) { FailGrantOf = 1 };

			world.Run();

			Assert.AreEqual(PurchaseSaga.Restore(PurchaseStage.Owed, 1, 3), world.Saved, "owed from the reward that failed");
			Assert.AreEqual(PurchaseStep.Grant, world.Saved.Next);

			world.Run();

			Assert.AreEqual(PurchaseStage.Credited, world.Saved.Stage);
			Assert.AreEqual(1, world.Payments, "paid once");
			CollectionAssert.AreEqual(new[] { 0, 1, 2 }, world.Grants, "each reward once, in order");
		}

		// ---------------------------------------------------------------- crashes

		[TestCase(0, false)]
		[TestCase(1, false)]
		[TestCase(3, false)]
		[TestCase(0, true)]
		[TestCase(1, true)]
		[TestCase(3, true)]
		public void ACrashAfterAnyStep_ThenARestart_NeverPaysTwice_GrantsTwice_OrGrantsUnpaid(int rewards, bool fromStore)
		{
			PurchaseSaga start = fromStore ? PurchaseSaga.Owed(rewards) : PurchaseSaga.Unpaid(rewards);
			int fullRun = fromStore ? rewards + 1 : rewards + 2;

			for (int crashAfter = 0; crashAfter <= fullRun; crashAfter++)
			{
				var world = new World(start);
				world.Run(maxSteps: crashAfter);
				world.Restart();

				string at = $"crash after {crashAfter} of {fullRun} steps";
				bool abandoned = !fromStore && crashAfter == 0;

				Assert.AreEqual(abandoned ? PurchaseStage.Abandoned : PurchaseStage.Credited, world.Saved.Stage, at);
				Assert.AreEqual(fromStore || abandoned ? 0 : 1, world.Payments, at);
				CollectionAssert.AreEqual(abandoned ? Array.Empty<int>() : Indices(rewards), world.Grants, at + ": each reward once, in order, and none unpaid");
			}
		}

		[TestCase(3, false)]
		[TestCase(3, true)]
		public void ACrashAfterEveryStep_StillFinishes_WithEachStepOnce(int rewards, bool fromStore)
		{
			var world = new World(fromStore ? PurchaseSaga.Owed(rewards) : PurchaseSaga.Unpaid(rewards));

			world.Run(maxSteps: 1);
			for (int restarts = 0; !world.Saved.IsDone; restarts++)
			{
				Assert.Less(restarts, 10, "a restart always moves the purchase forward");
				world.Restart(maxSteps: 1);
			}

			Assert.AreEqual(PurchaseStage.Credited, world.Saved.Stage);
			Assert.AreEqual(fromStore ? 0 : 1, world.Payments);
			CollectionAssert.AreEqual(Indices(rewards), world.Grants);
		}

		[Test]
		public void AfterARestart_OnlyAnUnpaidPurchase_TakesADifferentStep()
		{
			Assert.AreEqual(PurchaseStep.Abandon, PurchaseSaga.Unpaid(2).NextAfterRestart);

			foreach (PurchaseSaga saga in new[]
			         {
				         PurchaseSaga.Owed(2),
				         PurchaseSaga.Restore(PurchaseStage.Owed, 2, 2),
				         PurchaseSaga.Restore(PurchaseStage.Credited, 2, 2),
				         PurchaseSaga.Restore(PurchaseStage.Abandoned, 0, 2),
			         })
			{
				Assert.AreEqual(saga.Next, saga.NextAfterRestart, saga.ToString());
			}
		}

		// ---------------------------------------------------------------- misuse

		[Test]
		public void APaidPurchase_CantBeAbandoned_OrPaidAgain()
		{
			PurchaseSaga owed = PurchaseSaga.Unpaid(1).Paid();

			Assert.Throws<InvalidOperationException>(() => owed.Abandon());
			Assert.Throws<InvalidOperationException>(() => owed.Paid());
		}

		[Test]
		public void AnUnpaidPurchase_CantBeGranted_OrCredited()
		{
			PurchaseSaga unpaid = PurchaseSaga.Unpaid(1);

			Assert.Throws<InvalidOperationException>(() => unpaid.RewardGranted());
			Assert.Throws<InvalidOperationException>(() => PurchaseSaga.Unpaid(0).Credit());
		}

		[Test]
		public void RewardsCantBeGrantedPastTheLast_NorCreditedBeforeIt()
		{
			PurchaseSaga allGranted = PurchaseSaga.Owed(1).RewardGranted();
			Assert.Throws<InvalidOperationException>(() => allGranted.RewardGranted());

			Assert.Throws<InvalidOperationException>(() => PurchaseSaga.Owed(1).Credit());
		}

		[Test]
		public void AFinishedPurchase_TakesNoFurtherStep()
		{
			foreach (PurchaseSaga done in new[] { PurchaseSaga.Owed(0).Credit(), PurchaseSaga.Unpaid(1).Abandon() })
			{
				Assert.IsTrue(done.IsDone);
				Assert.AreEqual(PurchaseStep.None, done.Next);
				Assert.Throws<InvalidOperationException>(() => done.Paid());
				Assert.Throws<InvalidOperationException>(() => done.Abandon());
				Assert.Throws<InvalidOperationException>(() => done.RewardGranted());
				Assert.Throws<InvalidOperationException>(() => done.Credit());
			}
		}

		[Test]
		public void AMisuse_SaysWhereThePurchaseStands()
		{
			var thrown = Assert.Throws<InvalidOperationException>(() => PurchaseSaga.Owed(2).Abandon());

			StringAssert.Contains("Owed 0/2", thrown.Message);
			StringAssert.Contains("Grant", thrown.Message);
		}

		// ---------------------------------------------------------------- restore

		[Test]
		public void Restore_ClampsTheGrantedCount_IntoTheRewards()
		{
			Assert.AreEqual(0, PurchaseSaga.Restore(PurchaseStage.Owed, -4, 3).Granted);
			Assert.AreEqual(3, PurchaseSaga.Restore(PurchaseStage.Owed, 9, 3).Granted);
			Assert.AreEqual(PurchaseStep.Credit, PurchaseSaga.Restore(PurchaseStage.Owed, 9, 3).Next, "past the end means every reward was granted");
		}

		[Test]
		public void Restore_RefusesAStageOrCountThatCantBe()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => PurchaseSaga.Restore((PurchaseStage)4, 0, 0));
			Assert.Throws<ArgumentOutOfRangeException>(() => PurchaseSaga.Restore(PurchaseStage.Owed, 0, -1));
			Assert.Throws<ArgumentOutOfRangeException>(() => PurchaseSaga.Unpaid(-1));
			Assert.Throws<ArgumentOutOfRangeException>(() => PurchaseSaga.Owed(-1));
		}

		[Test]
		public void Equality_IsByValue()
		{
			PurchaseSaga a = PurchaseSaga.Owed(2).RewardGranted();
			PurchaseSaga b = PurchaseSaga.Restore(PurchaseStage.Owed, 1, 2);

			Assert.AreEqual(a, b);
			Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
			Assert.AreNotEqual(a, PurchaseSaga.Restore(PurchaseStage.Owed, 1, 3));
			Assert.AreNotEqual(a, PurchaseSaga.Restore(PurchaseStage.Credited, 1, 2));
			Assert.AreEqual("Owed 1/2", a.ToString());
		}

		// ---------------------------------------------------------------- helpers

		private static int[] Indices(int count)
		{
			var indices = new int[count];
			for (int i = 0; i < count; i++) indices[i] = i;
			return indices;
		}

		/// <summary>
		/// A shell that runs a purchase by its saga and saves the state after every step, the way
		/// the ledger does, with a count of what each step did. A run that stops early is a crash:
		/// the next run starts from the saved state.
		/// </summary>
		private sealed class World
		{
			public readonly List<PurchaseStep> Steps  = new();
			public readonly List<int>          Grants = new();

			public PurchaseSaga Saved;
			public int          Payments;
			public bool         DeclinePayment;

			/// <summary>The reward whose grant fails, once.</summary>
			public int FailGrantOf = -1;

			public World(PurchaseSaga start) => Saved = start;

			/// <summary>A run in the session that recorded the purchase.</summary>
			public void Run(int maxSteps = int.MaxValue) => RunFrom(afterRestart: false, maxSteps);

			/// <summary>A run in a later session.</summary>
			public void Restart(int maxSteps = int.MaxValue) => RunFrom(afterRestart: true, maxSteps);

			private void RunFrom(bool afterRestart, int maxSteps)
			{
				PurchaseSaga saga = Saved;

				for (int taken = 0; taken < maxSteps; taken++)
				{
					PurchaseStep step = afterRestart && taken == 0 ? saga.NextAfterRestart : saga.Next;
					if (step == PurchaseStep.None) return;

					switch (step)
					{
						case PurchaseStep.Pay when DeclinePayment:
							saga = saga.Abandon();
							break;

						case PurchaseStep.Pay:
							Payments++;
							saga = saga.Paid();
							break;

						case PurchaseStep.Grant when FailGrantOf == saga.Granted:
							FailGrantOf = -1;
							return;

						case PurchaseStep.Grant:
							Grants.Add(saga.Granted);
							saga = saga.RewardGranted();
							break;

						case PurchaseStep.Credit:
							saga = saga.Credit();
							break;

						case PurchaseStep.Abandon:
							saga = saga.Abandon();
							break;
					}

					Steps.Add(step);
					Saved = saga;
				}
			}
		}
	}
}
