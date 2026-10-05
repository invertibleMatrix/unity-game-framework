using System;

namespace AK.Kernel.Purchasing
{
	/// <summary>Where a purchase stands. See <see cref="PurchaseSaga"/>.</summary>
	public enum PurchaseStage : byte
	{
		/// <summary>
		/// On record, not paid for yet. Found after a restart, it is abandoned: whether its
		/// payment went through is unknown, and nothing is granted without one.
		/// </summary>
		Unpaid = 0,

		/// <summary>Paid for, so its rewards are owed, from <see cref="PurchaseSaga.Granted"/> on.</summary>
		Owed = 1,

		/// <summary>Every reward is granted. The purchase is done.</summary>
		Credited = 2,

		/// <summary>Abandoned before its payment. Nothing was paid, and nothing is owed.</summary>
		Abandoned = 3,
	}

	/// <summary>What the shell does next for a purchase. See <see cref="PurchaseSaga.Next"/>.</summary>
	public enum PurchaseStep : byte
	{
		/// <summary>Nothing: the purchase is credited or abandoned.</summary>
		None = 0,

		/// <summary>Take the payment, then report <see cref="PurchaseSaga.Paid"/>, or <see cref="PurchaseSaga.Abandon"/> when it was declined.</summary>
		Pay = 1,

		/// <summary>
		/// Grant the reward at <see cref="PurchaseSaga.Granted"/>, then report
		/// <see cref="PurchaseSaga.RewardGranted"/>. A grant that fails ends the run: the purchase
		/// stays owed, and a later run resumes from that reward.
		/// </summary>
		Grant = 2,

		/// <summary>Record the purchase credited (<see cref="PurchaseSaga.Credit"/>).</summary>
		Credit = 3,

		/// <summary>Record the purchase abandoned (<see cref="PurchaseSaga.Abandon"/>). Only after a restart, for one found unpaid.</summary>
		Abandon = 4,
	}

	/// <summary>
	/// A purchase as a saga: it is recorded, paid for, granted one reward at a time, then credited.
	/// The shell persists the state after every step, so a purchase cut off by a crash picks up
	/// from its last step after a restart (<see cref="NextAfterRestart"/>), and no step runs twice:
	/// <list type="bullet">
	/// <item>The payment is taken only from <see cref="PurchaseStage.Unpaid"/>, in the run that
	/// recorded the purchase. A purchase found unpaid after a restart is abandoned, never charged
	/// again and never granted.</item>
	/// <item><see cref="Granted"/> moves past a reward only once its grant succeeds, so no reward is
	/// granted twice, and a failed grant is retried from where it stopped.</item>
	/// <item>Once paid, a purchase only moves forward: what was paid for is owed until granted.</item>
	/// </list>
	/// A store purchase starts <see cref="PurchaseStage.Owed"/>: the store took the payment before
	/// its order reached the game.
	///
	/// Immutable: each transition returns the next state. A transition its stage doesn't allow
	/// throws, since the shell must follow <see cref="Next"/>.
	/// </summary>
	public readonly struct PurchaseSaga : IEquatable<PurchaseSaga>
	{
		public readonly PurchaseStage Stage;

		/// <summary>How many rewards, from the first, have been granted.</summary>
		public readonly int Granted;

		/// <summary>How many rewards the purchase grants.</summary>
		public readonly int RewardCount;

		private PurchaseSaga(PurchaseStage stage, int granted, int rewardCount)
		{
			Stage       = stage;
			Granted     = granted;
			RewardCount = rewardCount;
		}

		/// <summary>A purchase to be paid for in the run that records it.</summary>
		public static PurchaseSaga Unpaid(int rewardCount) => new(PurchaseStage.Unpaid, 0, CheckCount(rewardCount));

		/// <summary>A purchase paid for already, such as a store's, or with nothing to pay.</summary>
		public static PurchaseSaga Owed(int rewardCount) => new(PurchaseStage.Owed, 0, CheckCount(rewardCount));

		/// <summary>
		/// A state as persisted. <paramref name="granted"/> is clamped into 0 to
		/// <paramref name="rewardCount"/>, so a damaged count can't grant a reward twice or skip past the end.
		/// </summary>
		public static PurchaseSaga Restore(PurchaseStage stage, int granted, int rewardCount)
		{
			if (stage > PurchaseStage.Abandoned) throw new ArgumentOutOfRangeException(nameof(stage), stage, "Not a purchase stage.");

			CheckCount(rewardCount);
			int clamped = granted < 0 ? 0 : granted > rewardCount ? rewardCount : granted;
			return new PurchaseSaga(stage, clamped, rewardCount);
		}

		/// <summary>The next step, in the run that recorded the purchase.</summary>
		public PurchaseStep Next => Stage switch
		{
			PurchaseStage.Unpaid => PurchaseStep.Pay,
			PurchaseStage.Owed   => Granted < RewardCount ? PurchaseStep.Grant : PurchaseStep.Credit,
			_                    => PurchaseStep.None,
		};

		/// <summary>The next step for a purchase found after a restart: an unpaid one is abandoned.</summary>
		public PurchaseStep NextAfterRestart => Stage == PurchaseStage.Unpaid ? PurchaseStep.Abandon : Next;

		public bool IsDone => Stage >= PurchaseStage.Credited;

		/// <summary>The payment went through: <see cref="PurchaseStage.Unpaid"/> becomes <see cref="PurchaseStage.Owed"/>.</summary>
		public PurchaseSaga Paid()
		{
			Require(PurchaseStage.Unpaid, nameof(Paid));
			return new PurchaseSaga(PurchaseStage.Owed, 0, RewardCount);
		}

		/// <summary>
		/// The payment was declined, or the purchase was found unpaid after a restart:
		/// <see cref="PurchaseStage.Unpaid"/> becomes <see cref="PurchaseStage.Abandoned"/>.
		/// </summary>
		public PurchaseSaga Abandon()
		{
			Require(PurchaseStage.Unpaid, nameof(Abandon));
			return new PurchaseSaga(PurchaseStage.Abandoned, 0, RewardCount);
		}

		/// <summary>The reward at <see cref="Granted"/> was granted.</summary>
		public PurchaseSaga RewardGranted()
		{
			if (Next != PurchaseStep.Grant) throw Misuse(nameof(RewardGranted));
			return new PurchaseSaga(PurchaseStage.Owed, Granted + 1, RewardCount);
		}

		/// <summary>Every reward is granted: <see cref="PurchaseStage.Owed"/> becomes <see cref="PurchaseStage.Credited"/>.</summary>
		public PurchaseSaga Credit()
		{
			if (Next != PurchaseStep.Credit) throw Misuse(nameof(Credit));
			return new PurchaseSaga(PurchaseStage.Credited, Granted, RewardCount);
		}

		public bool Equals(PurchaseSaga other) => Stage == other.Stage && Granted == other.Granted && RewardCount == other.RewardCount;

		public override bool Equals(object obj) => obj is PurchaseSaga other && Equals(other);

		public override int GetHashCode() => ((int)Stage * 397 ^ Granted) * 397 ^ RewardCount;

		public override string ToString() => $"{Stage} {Granted}/{RewardCount}";

		private void Require(PurchaseStage stage, string transition)
		{
			if (Stage != stage) throw Misuse(transition);
		}

		private InvalidOperationException Misuse(string transition)
		{
			return new InvalidOperationException($"A purchase at {this} can't take {transition}; its next step is {Next}.");
		}

		private static int CheckCount(int rewardCount)
		{
			return rewardCount < 0 ? throw new ArgumentOutOfRangeException(nameof(rewardCount), rewardCount, "A purchase can't grant fewer than no rewards.") : rewardCount;
		}
	}
}
