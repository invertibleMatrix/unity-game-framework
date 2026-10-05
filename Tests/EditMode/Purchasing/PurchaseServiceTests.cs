using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Transactions;
using AK.Kernel.Persistence;
using AK.Kernel.Purchasing;
using AK.Kernel.Results;
using AK.Services;
using AK.Services.Costs;
using AK.Services.Rewards;
using AK.Services.Transactions;
using AK.Tests.Support;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests.Purchasing
{
	public class PurchaseServiceTests
	{
		private const string Gems100 = "com.test.gems_100";

		private readonly List<Object> _created = new();

		private PrefsStore       _disk;
		private FakeCosts        _costs;
		private RecordingRewards _rewards;
		private RewardStub       _coins;
		private RewardStub       _gems;

		[SetUp]
		public void SetUp()
		{
			_disk    = new PrefsStore(new InMemoryKeyValueStore());
			_costs   = new FakeCosts();
			_rewards = new RecordingRewards();
			_coins   = Reward("Coins");
			_gems    = Reward("Gems");
		}

		[TearDown]
		public void TearDown()
		{
			foreach (var o in _created) Object.DestroyImmediate(o);
			_created.Clear();
		}

		// ---------------------------------------------------------------- currency

		[Test]
		public void ACurrencyPurchase_IsRecordedUnpaid_ThenPaid_ThenGranted()
		{
			TransactionService ledger = NewLedger();
			var service = new PurchaseService(_costs, ledger);
			Item item = CurrencyItem("Chest", _coins, _gems);

			_costs.OnDeduct = () =>
			{
				Transaction recorded = Single(ledger.Query(item.TransactionType));
				Assert.AreEqual(TransactionStatus.Pending, recorded.Status);
				Assert.IsTrue(recorded.AwaitingPayment, "on record before the payment, as unpaid");
				CollectionAssert.IsEmpty(_rewards.Granted, "nothing granted before the payment");
			};
			_rewards.OnGrant = _ => Assert.IsFalse(Single(ledger.Query(item.TransactionType)).AwaitingPayment, "granted only once marked paid");

			Result result = Completed(service.Purchase(item));

			Assert.IsTrue(result.IsOk, result.ToString());
			Assert.AreEqual(90, _costs.Balance);
			CollectionAssert.AreEqual(new IReward[] { _coins, _gems }, _rewards.Granted);
			Assert.AreEqual(TransactionStatus.Credited, Single(ledger.Query(item.TransactionType)).Status);
			Assert.AreEqual(1, ledger.Count(item.TransactionType));
		}

		[Test]
		public void ADeclinedDeduction_AbandonsThePurchase_AndGrantsNothing()
		{
			TransactionService ledger = NewLedger();
			var service = new PurchaseService(_costs, ledger);
			Item item = CurrencyItem("Chest", _coins);
			_costs.DeclineDeduct = true;

			Result result = Completed(service.Purchase(item));

			Assert.AreEqual(ErrorCode.DeductDeclined, result.Code);
			Assert.AreEqual(TransactionStatus.Failed, Single(ledger.Query(item.TransactionType)).Status);
			CollectionAssert.IsEmpty(_rewards.Granted);
			Assert.AreEqual(0, Completed(service.GrantPendingCredits()), "nothing is owed");
		}

		[Test]
		public void APurchaseThatCantBeAfforded_RecordsNothing()
		{
			TransactionService ledger = NewLedger();
			var service = new PurchaseService(_costs, ledger);
			_costs.Balance = 5;

			Result result = Completed(service.Purchase(CurrencyItem("Chest", _coins)));

			Assert.AreEqual(ErrorCode.CannotAfford, result.Code);
			Assert.AreEqual(0, _costs.DeductCalls);
			CollectionAssert.IsEmpty(ledger.Query(default));
		}

		[Test]
		public void AnItemWithoutACostType_IsRefused_RatherThanGivenAway()
		{
			var service = new PurchaseService(_costs, NewLedger());
			var free = new Item("Free chest", null, new Cost(Uid.None, 0), Type("Free chest"), _coins);

			Result result = Completed(service.Purchase(free));

			Assert.AreEqual(ErrorCode.NoIdentity, result.Code);
			Assert.AreEqual(0, _costs.CanAffordCalls);
			CollectionAssert.IsEmpty(_rewards.Granted);
		}

		[Test]
		public void AGrantThatFails_KeepsThePaidPurchaseOwed_AndGrantPendingCreditsFinishesIt()
		{
			var service = new PurchaseService(_costs, NewLedger());
			_rewards.Decline = _gems;

			Result result = Completed(service.Purchase(CurrencyItem("Chest", _coins, _gems)));

			Assert.AreEqual(ErrorCode.RewardDeclined, result.Code, "the grant's failure, passed on");
			Assert.AreEqual(90, _costs.Balance, "paid for");

			_rewards.Decline = null;

			Assert.AreEqual(1, Completed(service.GrantPendingCredits()));
			CollectionAssert.AreEqual(new IReward[] { _coins, _gems }, _rewards.Granted, "the coins weren't granted twice");
			Assert.AreEqual(90, _costs.Balance, "and paid for once");
		}

		[Test]
		public void APurchaseCreditedLater_IsPaidNow_AndGrantedByGrantPendingCredits()
		{
			var service = new PurchaseService(_costs, NewLedger());

			Result result = Completed(service.Purchase(CurrencyItem("Chest", _coins), immediateCredit: false));

			Assert.IsTrue(result.IsOk, result.ToString());
			Assert.AreEqual(90, _costs.Balance);
			CollectionAssert.IsEmpty(_rewards.Granted);

			Assert.AreEqual(1, Completed(service.GrantPendingCredits()));
			CollectionAssert.AreEqual(new IReward[] { _coins }, _rewards.Granted);
		}

		[Test]
		public void APurchaseCutOffBeforeItWasMarkedPaid_IsAbandonedAfterARestart()
		{
			Item item = CurrencyItem("Chest", _coins);
			_costs.OnDeduct = () => throw new AppStopped();

			UniTask<Result> cutOff = new PurchaseService(_costs, NewLedger()).Purchase(item);
			Assert.Throws<AppStopped>(() => cutOff.GetAwaiter().GetResult());

			_costs.OnDeduct = null;
			var restarted = new PurchaseService(_costs, NewLedger());

			int credited;
			using (ExpectedLog.Warning("was never marked paid"))
			{
				credited = Completed(restarted.GrantPendingCredits());
			}

			Assert.AreEqual(0, credited);
			CollectionAssert.IsEmpty(_rewards.Granted, "nothing is granted without a payment");
			Assert.AreEqual((int)TransactionStatus.Failed, TransactionLedgerState.Load(_disk).Entries[0].Status);
		}

		// ---------------------------------------------------------------- store

		[Test]
		public void AStoreOrder_IsFinishedOnlyOnceItIsCredited()
		{
			TransactionService ledger = NewLedger();
			var store = new FakeStore();
			var service = new PurchaseService(_costs, ledger, store);
			_rewards.OnGrant = _ => CollectionAssert.IsEmpty(store.Finished, "the order is still unfinished while it is granted");

			Result result = Completed(service.Purchase(StoreItem(Gems100, _coins, _gems)));

			Assert.IsTrue(result.IsOk, result.ToString());
			CollectionAssert.AreEqual(new IReward[] { _coins, _gems }, _rewards.Granted);
			CollectionAssert.AreEqual(new[] { "T1" }, store.Finished);
			Assert.IsTrue(ledger.TryFindByExternalId("T1", out Transaction recorded));
			Assert.AreEqual(TransactionStatus.Credited, recorded.Status);
			Assert.AreEqual(Gems100, recorded.Source);
			Assert.AreEqual(0, _costs.CanAffordCalls + _costs.DeductCalls, "the store took the payment; the item's currency cost is ignored");
		}

		[Test]
		public void AGrantThatFails_LeavesTheOrderUnfinished_AndARetryGrantsTheRestAndFinishesIt()
		{
			var store = new FakeStore();
			var service = new PurchaseService(_costs, NewLedger(), store);
			_rewards.Decline = _gems;

			Result result = Completed(service.Purchase(StoreItem(Gems100, _coins, _gems)));

			Assert.AreEqual(ErrorCode.RewardDeclined, result.Code);
			CollectionAssert.IsEmpty(store.Finished);

			_rewards.Decline = null;

			Assert.AreEqual(1, Completed(service.GrantPendingCredits()));
			CollectionAssert.AreEqual(new IReward[] { _coins, _gems }, _rewards.Granted);
			CollectionAssert.AreEqual(new[] { "T1" }, store.Finished);
		}

		[Test]
		public void AnOrderDeliveredAgainInTheSameSession_IsFinished_WithoutGrantingTwice()
		{
			var store = new FakeStore();
			var service = new PurchaseService(_costs, NewLedger(), store);
			Completed(service.Purchase(StoreItem(Gems100, _coins)));

			store.Deliver(store.Order("T1"));

			CollectionAssert.AreEqual(new IReward[] { _coins }, _rewards.Granted);
			Assert.AreEqual(OrderFulfilment.Fulfilled, store.FulfilmentOf("T1"));
		}

		[Test]
		public void AnOrderCreditedButNotFinished_IsFinishedAfterARestart_WithoutGrantingTwice()
		{
			var store = new FakeStore { LoseFinishes = true };
			Completed(new PurchaseService(_costs, NewLedger(), store).Purchase(StoreItem(Gems100, _coins)));
			CollectionAssert.IsEmpty(store.Finished, "the app stopped before the store finished the order");

			var nextLaunch = new FakeStore();
			nextLaunch.Hold(store.Order("T1"));
			new PurchaseService(_costs, NewLedger(), nextLaunch);

			CollectionAssert.AreEqual(new[] { "T1" }, nextLaunch.Finished);
			CollectionAssert.AreEqual(new IReward[] { _coins }, _rewards.Granted);
		}

		[Test]
		public void AnInvalidReceipt_GrantsAndRecordsNothing_AndTheOrderIsRejected()
		{
			TransactionService ledger = NewLedger();
			var store = new FakeStore();
			var validator = new FakeValidator { Verdict = ReceiptVerdict.Invalid };
			var service = new PurchaseService(_costs, ledger, store, validator: validator);

			Result result = Completed(service.Purchase(StoreItem(Gems100, _coins)));

			Assert.AreEqual(ErrorCode.ReceiptRejected, result.Code);
			CollectionAssert.IsEmpty(_rewards.Granted);
			Assert.IsFalse(ledger.TryFindByExternalId("T1", out _));
			CollectionAssert.AreEqual(new[] { "T1" }, store.Rejected);

			Assert.AreEqual(0, Completed(service.GrantPendingCredits()));
			CollectionAssert.AreEqual(new[] { "receipt-T1" }, validator.Checked, "a rejected order isn't retried");
		}

		[Test]
		public void AReceiptThatCantBeCheckedNow_LeavesTheOrderUnfinished_AndARetryGrantsIt()
		{
			var store = new FakeStore();
			var validator = new FakeValidator { Verdict = ReceiptVerdict.Unavailable };
			var service = new PurchaseService(_costs, NewLedger(), store, validator: validator);

			Result result = Completed(service.Purchase(StoreItem(Gems100, _coins)));

			Assert.AreEqual(ErrorCode.ReceiptUnverified, result.Code);
			CollectionAssert.IsEmpty(_rewards.Granted);
			CollectionAssert.IsEmpty(store.Finished);

			validator.Verdict = ReceiptVerdict.Valid;

			Assert.AreEqual(1, Completed(service.GrantPendingCredits()), "the retry's credit counts");
			CollectionAssert.AreEqual(new IReward[] { _coins }, _rewards.Granted);
			CollectionAssert.AreEqual(new[] { "T1" }, store.Finished);
		}

		[Test]
		public void TwoDeliveriesOfAnOrderBeingValidated_RecordAndGrantItOnce()
		{
			TransactionService ledger = NewLedger();
			var store = new FakeStore();
			var validator = new FakeValidator { Held = new UniTaskCompletionSource<ReceiptVerdict>() };
			var service = new PurchaseService(_costs, ledger, store, new Catalog(StoreItem(Gems100, _coins)), validator);

			StoreOrder order = store.Pay(Gems100);
			store.Deliver(order);
			validator.Held.TrySetResult(ReceiptVerdict.Valid);

			CollectionAssert.AreEqual(new IReward[] { _coins }, _rewards.Granted);
			Assert.AreEqual(1, ledger.Query(default).Count);
			CollectionAssert.AreEqual(new[] { "T1", "T1" }, store.Finished, "both deliveries end finished");
		}

		[Test]
		public void AnOrderWithoutAPurchaseUnderWay_IsGrantedAsTheCatalogSays()
		{
			var store = new FakeStore();
			var service = new PurchaseService(_costs, NewLedger(), store, new Catalog(StoreItem(Gems100, _gems)));

			store.Pay(Gems100);

			CollectionAssert.AreEqual(new IReward[] { _gems }, _rewards.Granted);
			CollectionAssert.AreEqual(new[] { "T1" }, store.Finished);
			Assert.IsNotNull(service);
		}

		[Test]
		public void AnOrderForAProductNothingSells_StaysUnfinished()
		{
			var store = new FakeStore();
			var service = new PurchaseService(_costs, NewLedger(), store);

			using (ExpectedLog.Warning("Nothing is sold as store product 'com.test.unknown'"))
			{
				store.Pay("com.test.unknown");
			}

			Assert.AreEqual(OrderFulfilment.Unfinished, store.FulfilmentOf("T1"));
			CollectionAssert.IsEmpty(_rewards.Granted);
			Assert.IsNotNull(service);
		}

		[Test]
		public void AStoreProduct_WithoutAStore_IsRefused_AndNotChargedInCurrency()
		{
			var service = new PurchaseService(_costs, NewLedger());

			Result result = Completed(service.Purchase(StoreItem(Gems100, _coins)));

			Assert.AreEqual(ErrorCode.StoreNotInitialized, result.Code);
			Assert.AreEqual(0, _costs.DeductCalls);
		}

		[Test]
		public void AStoreNotInitialized_RefusesThePurchase()
		{
			var store = new FakeStore { IsInitialized = false };
			var service = new PurchaseService(_costs, NewLedger(), store);

			Assert.AreEqual(ErrorCode.StoreNotInitialized, Completed(service.Purchase(StoreItem(Gems100, _coins))).Code);
			CollectionAssert.IsEmpty(store.Asked);
		}

		[Test]
		public void ADeferredPurchase_IsReportedSo_AndGrantedWhenItsApprovalArrives()
		{
			var store = new FakeStore { FailWith = IAPFailureType.Deferred };
			var service = new PurchaseService(_costs, NewLedger(), store);

			Result result = Completed(service.Purchase(StoreItem(Gems100, _coins)));

			Assert.AreEqual(ErrorCode.PurchaseDeferred, result.Code);
			CollectionAssert.IsEmpty(_rewards.Granted);

			store.Pay(Gems100);

			CollectionAssert.AreEqual(new IReward[] { _coins }, _rewards.Granted, "the approved order, granted as the item bought this session");
			CollectionAssert.AreEqual(new[] { "T1" }, store.Finished);
		}

		[Test]
		public void CancellingTheWait_Throws_AndTheOrderIsStillGrantedWhenItArrives()
		{
			var store = new FakeStore { HoldPayment = true };
			var service = new PurchaseService(_costs, NewLedger(), store);
			using var cts = new CancellationTokenSource();

			UniTask<Result> purchase = service.Purchase(StoreItem(Gems100, _coins), ct: cts.Token);
			cts.Cancel();

			Assert.Catch<OperationCanceledException>(() => purchase.GetAwaiter().GetResult());

			store.Pay(Gems100);

			CollectionAssert.AreEqual(new IReward[] { _coins }, _rewards.Granted);
			CollectionAssert.AreEqual(new[] { "T1" }, store.Finished);
		}

		[Test]
		public void ASecondPurchase_WhileOneIsUnderWay_IsRefused()
		{
			var store = new FakeStore { HoldPayment = true };
			var service = new PurchaseService(_costs, NewLedger(), store);

			UniTask<Result> first = service.Purchase(StoreItem(Gems100, _coins));
			Result second = Completed(service.Purchase(StoreItem("com.test.gems_500", _gems)));

			Assert.AreEqual(ErrorCode.PurchaseInProgress, second.Code);

			store.Pay(Gems100);

			Assert.IsTrue(Completed(first).IsOk);
			CollectionAssert.AreEqual(new[] { Gems100 }, store.Asked);
		}

		[Test]
		public void AnOlderOrderOfTheSameProduct_DoesntAnswerThePurchaseUnderWay()
		{
			// An order of the product left unfinished, because its reward couldn't be granted.
			var store = new FakeStore();
			var service = new PurchaseService(_costs, NewLedger(), store, new Catalog(StoreItem(Gems100, _coins)));
			_rewards.Decline = _coins;
			store.Pay(Gems100);
			Assert.AreEqual(OrderFulfilment.Unfinished, store.FulfilmentOf("T1"));

			// The product is bought again, and the older order is retried while the store takes the payment.
			store.HoldPayment = true;
			UniTask<Result> purchase = service.Purchase(StoreItem(Gems100, _coins));
			Completed(service.GrantPendingCredits());

			Assert.IsFalse(purchase.Status.IsCompleted(), "the older order isn't this purchase's");

			_rewards.Decline = null;
			store.Pay(Gems100);

			Assert.IsTrue(Completed(purchase).IsOk, "the purchase's own order was granted, whatever became of the older one");
			CollectionAssert.AreEqual(new[] { "T2" }, store.Finished);
		}

		[Test]
		public void AStorePurchaseCreditedLater_IsOwed_AndFinishedOnceGrantPendingCreditsCreditsIt()
		{
			var store = new FakeStore();
			var service = new PurchaseService(_costs, NewLedger(), store);

			Result result = Completed(service.Purchase(StoreItem(Gems100, _coins), immediateCredit: false));

			Assert.IsTrue(result.IsOk, result.ToString());
			CollectionAssert.IsEmpty(_rewards.Granted);
			CollectionAssert.IsEmpty(store.Finished, "unfinished until credited");

			Assert.AreEqual(1, Completed(service.GrantPendingCredits()));
			CollectionAssert.AreEqual(new IReward[] { _coins }, _rewards.Granted);
			CollectionAssert.AreEqual(new[] { "T1" }, store.Finished);
		}

		[Test]
		public void AStoreThatReportsSuccess_WithoutDeliveringTheOrder_IsAnUnknownOutcome()
		{
			var store = new FakeStore { SucceedWithoutOrder = true };
			var service = new PurchaseService(_costs, NewLedger(), store);

			Assert.AreEqual(ErrorCode.StoreUnknown, Completed(service.Purchase(StoreItem(Gems100, _coins))).Code);
		}

		[TestCase(IAPFailureType.UserCancelled, ErrorCode.Cancelled)]
		[TestCase(IAPFailureType.PaymentDeclined, ErrorCode.PaymentDeclined)]
		[TestCase(IAPFailureType.ProductUnavailable, ErrorCode.ProductUnavailable)]
		[TestCase(IAPFailureType.StoreError, ErrorCode.StoreError)]
		[TestCase(IAPFailureType.DuplicateTransaction, ErrorCode.DuplicateTransaction)]
		[TestCase(IAPFailureType.NotInitialized, ErrorCode.StoreNotInitialized)]
		[TestCase(IAPFailureType.Timeout, ErrorCode.Timeout)]
		[TestCase(IAPFailureType.ExistingPurchasePending, ErrorCode.PurchaseInProgress)]
		[TestCase(IAPFailureType.Deferred, ErrorCode.PurchaseDeferred)]
		[TestCase(IAPFailureType.Unknown, ErrorCode.StoreUnknown)]
		public void AStoreFailure_ComesBackAsItsCode(IAPFailureType failure, ErrorCode code)
		{
			var service = new PurchaseService(_costs, NewLedger(), new FakeStore { FailWith = failure });

			Assert.AreEqual(code, Completed(service.Purchase(StoreItem(Gems100, _coins))).Code);
			CollectionAssert.IsEmpty(_rewards.Granted);
		}

		// ---------------------------------------------------------------- arguments

		[Test]
		public void ANullItem_IsRefused()
		{
			Assert.AreEqual(ErrorCode.NullArgument, Completed(new PurchaseService(_costs, NewLedger()).Purchase(null)).Code);
		}

		[Test]
		public void TheCostsAndTheLedger_AreRequired()
		{
			Assert.Throws<ArgumentNullException>(() => new PurchaseService(null, NewLedger()));
			Assert.Throws<ArgumentNullException>(() => new PurchaseService(_costs, null));
		}

		// ---------------------------------------------------------------- helpers

		// Every ledger shares one in-memory disk, so a new one reads what an earlier one saved,
		// the way the next launch does.
		private TransactionService NewLedger() => new(_rewards, new CreatedAssets(_created), null, _disk);

		// The fakes answer at once, so a purchase has finished by the time it returns.
		private static T Completed<T>(UniTask<T> task)
		{
			Assert.IsTrue(task.Status.IsCompleted(), "finished before returning");
			return task.GetAwaiter().GetResult();
		}

		private static Transaction Single(IReadOnlyList<Transaction> transactions)
		{
			Assert.AreEqual(1, transactions.Count);
			return transactions[0];
		}

		private TransactionType Type(string name)
		{
			var asset = ScriptableObject.CreateInstance<TransactionType>();
			asset.name = name;
			asset.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);
			_created.Add(asset);
			return asset;
		}

		private RewardStub Reward(string name)
		{
			var reward = ScriptableObject.CreateInstance<RewardStub>();
			reward.name = name;
			reward.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);
			_created.Add(reward);
			return reward;
		}

		private Item CurrencyItem(string name, params IReward[] rewards) => new(name, null, new Cost(FakeCosts.Gold, 10), Type(name), rewards);

		private Item StoreItem(string productId, params IReward[] rewards) => new(productId, productId, new Cost(FakeCosts.Gold, 10), Type(productId), rewards);

		private sealed class AppStopped : Exception { }

		private sealed class RewardStub : MetaDataAsset, IReward
		{
			public Uid RewardType => Uid.None;
			public void CollectRewards(List<IReward> rewards) => rewards.Add(this);
		}

		private sealed class Cost : ICostInfo
		{
			public Cost(Uid costType, int amount)
			{
				CostType = costType;
				Amount   = amount;
			}

			public Uid CostType { get; }
			public int Amount   { get; }
		}

		private sealed class Item : IPurchasable
		{
			private readonly IReward[] _rewards;

			public Item(string name, string productId, ICostInfo cost, TransactionType type, params IReward[] rewards)
			{
				DisplayName     = name;
				ProductID       = productId;
				Cost            = cost;
				TransactionType = type.IdAs<TransactionType>();
				_rewards        = rewards;
			}

			public string               DisplayName     { get; }
			public string               ProductID       { get; }
			public ICostInfo            Cost            { get; }
			public Uid<TransactionType> TransactionType { get; }

			public void CollectRewards(List<IReward> rewards) => rewards.AddRange(_rewards);
		}

		private sealed class Catalog : IPurchasableCatalog
		{
			private readonly IPurchasable[] _items;
			public Catalog(params IPurchasable[] items) => _items = items;

			public bool TryGetByProductId(string productId, out IPurchasable item)
			{
				item = Array.Find(_items, candidate => candidate.ProductID == productId);
				return item != null;
			}
		}

		/// <summary>One currency, gold, with a balance the test sets.</summary>
		private sealed class FakeCosts : ICostService
		{
			public static readonly Uid Gold = Uid.NewRandom();

			public int    Balance = 100;
			public bool   DeclineDeduct;
			public Action OnDeduct;
			public int    CanAffordCalls;
			public int    DeductCalls;

			public Result CanAfford(ICostInfo cost)
			{
				CanAffordCalls++;
				return Balance >= cost.Amount ? Result.Ok : Result.Fail(ErrorCode.CannotAfford, "the test's balance is too low");
			}

			public Result Deduct(ICostInfo cost)
			{
				DeductCalls++;
				OnDeduct?.Invoke();
				if (DeclineDeduct) return Result.Fail(ErrorCode.DeductDeclined, "declined by the test");

				Balance -= cost.Amount;
				return Result.Ok;
			}

			public void          RegisterProvider(ICostProvider provider)   { }
			public bool          UnregisterProvider(ICostProvider provider) => false;
			public ICostProvider GetProvider(Uid costType)                  => null;
		}

		private sealed class RecordingRewards : IRewardService
		{
			public readonly List<IReward> Granted = new();

			public IReward         Decline;
			public Action<IReward> OnGrant;

			public Result Grant(IReward reward)
			{
				if (ReferenceEquals(reward, Decline)) return Result.Fail(ErrorCode.RewardDeclined, "declined by the test");

				OnGrant?.Invoke(reward);
				Granted.Add(reward);
				return Result.Ok;
			}

			public void            RegisterProvider(IRewardProvider provider)   { }
			public bool            UnregisterProvider(IRewardProvider provider) => false;
			public IRewardProvider GetProvider(Uid rewardType)                  => null;
		}

		private sealed class CreatedAssets : IUidResolver
		{
			private readonly List<Object> _assets;
			public CreatedAssets(List<Object> assets) => _assets = assets;

			public bool TryResolve(Uid id, out UID asset)
			{
				foreach (Object candidate in _assets)
				{
					if (candidate is UID uid && uid.HasIdentity && uid.Id == id)
					{
						asset = uid;
						return true;
					}
				}

				asset = null;
				return false;
			}

			public bool TryResolve<T>(Uid id, out T asset) where T : UID
			{
				asset = TryResolve(id, out UID untyped) ? untyped as T : null;
				return asset != null;
			}

			public bool TryResolve<T>(Uid<T> id, out T asset) where T : UID => TryResolve(id.Value, out asset);
		}

		private sealed class FakeValidator : IReceiptValidator
		{
			public readonly List<string> Checked = new();

			public ReceiptVerdict Verdict;

			/// <summary>When set, every check waits for the test to answer it.</summary>
			public UniTaskCompletionSource<ReceiptVerdict> Held;

			public UniTask<ReceiptVerdict> ValidateAsync(StoreOrder order, CancellationToken ct)
			{
				Checked.Add(order.Receipt);
				return Held != null ? Held.Task : UniTask.FromResult(Verdict);
			}
		}

		/// <summary>
		/// A store the test drives, keeping Unity IAP's contract: a paid order is held until the
		/// handler fulfils it and finished only then, the first new order of the product being
		/// bought answers the purchase, and unfinished orders are handed over again on a retry.
		/// </summary>
		private sealed class FakeStore : IIAPService
		{
			public readonly List<string> Asked    = new();
			public readonly List<string> Finished = new();
			public readonly List<string> Rejected = new();

			public IAPFailureType FailWith;
			public bool           HoldPayment;
			public bool           SucceedWithoutOrder;

			/// <summary>The app stops before the store finishes a fulfilled order, so it stays unfinished.</summary>
			public bool LoseFinishes;

			private readonly Dictionary<string, StoreOrder>      _orders       = new();
			private readonly Dictionary<string, OrderFulfilment> _fulfilments  = new();
			private readonly List<string>                        _unfinished   = new();
			private readonly HashSet<string>                     _fulfilling   = new();

			private IStoreOrderHandler                         _handler;
			private UniTaskCompletionSource<IAPPurchaseResult> _wait;
			private string                                     _waitingFor;
			private string                                     _answer;
			private int                                        _transactions;

			public bool IsInitialized { get; set; } = true;

			public StoreOrder Order(string transactionId) => _orders[transactionId];

			public OrderFulfilment FulfilmentOf(string transactionId) => _fulfilments[transactionId];

			public void SetOrderHandler(IStoreOrderHandler handler)
			{
				_handler = handler;
				foreach (string id in _unfinished.ToArray()) Fulfil(id).Forget();
			}

			/// <summary>An unfinished order from an earlier session, delivered once a handler is set.</summary>
			public void Hold(StoreOrder order)
			{
				_orders[order.TransactionId] = order;
				_unfinished.Add(order.TransactionId);
			}

			/// <summary>The store takes a payment for the product and delivers the new order.</summary>
			public StoreOrder Pay(string productId)
			{
				string id = "T" + ++_transactions;
				var order = new StoreOrder(productId, id, "receipt-" + id, new Money(499, "USD"));
				Hold(order);

				if (_wait != null && _answer == null && _waitingFor == productId) _answer = id;

				Fulfil(id).Forget();
				return order;
			}

			/// <summary>Delivers an order again, as the store does at a launch or a restore.</summary>
			public void Deliver(StoreOrder order)
			{
				_orders[order.TransactionId] = order;
				if (!_unfinished.Contains(order.TransactionId)) _unfinished.Add(order.TransactionId);

				Fulfil(order.TransactionId, evenIfBusy: true).Forget();
			}

			public async UniTask<IAPPurchaseResult> PurchaseAsync(string productId, CancellationToken ct = default)
			{
				if (!IsInitialized) return IAPPurchaseResult.Failed(productId, IAPFailureType.NotInitialized, "test");

				Asked.Add(productId);
				if (FailWith != IAPFailureType.None) return IAPPurchaseResult.Failed(productId, FailWith, "test");
				if (SucceedWithoutOrder) return IAPPurchaseResult.Succeeded(productId, null, null);

				var wait = new UniTaskCompletionSource<IAPPurchaseResult>();
				_wait       = wait;
				_waitingFor = productId;
				_answer     = null;

				try
				{
					using (ct.Register(() => wait.TrySetCanceled(ct)))
					{
						if (!HoldPayment) Pay(productId);
						return await wait.Task;
					}
				}
				finally
				{
					_wait   = null;
					_answer = null;
				}
			}

			public async UniTask RetryUnfinishedOrdersAsync(CancellationToken ct = default)
			{
				foreach (string id in _unfinished.ToArray()) await Fulfil(id);
			}

			private async UniTask Fulfil(string id, bool evenIfBusy = false)
			{
				if (_handler == null || (!evenIfBusy && _fulfilling.Contains(id))) return;

				StoreOrder held    = _orders[id];
				bool       answers = _wait != null && _answer == id;
				var        order   = new StoreOrder(held.ProductId, held.TransactionId, held.Receipt, held.Price, answers);

				_fulfilling.Add(id);
				OrderFulfilment fulfilment = await _handler.FulfilAsync(order, CancellationToken.None);
				_fulfilling.Remove(id);

				_fulfilments[id] = fulfilment;
				if (fulfilment == OrderFulfilment.Fulfilled && !LoseFinishes)
				{
					_unfinished.Remove(id);
					Finished.Add(id);
				}
				else if (fulfilment == OrderFulfilment.Rejected)
				{
					_unfinished.Remove(id);
					Rejected.Add(id);
				}

				if (answers) _wait?.TrySetResult(IAPPurchaseResult.Succeeded(order.ProductId, order.Receipt, order.TransactionId));
			}

			public UniTask<bool> InitializeAsync(IEnumerable<IAPProductRegistration> products, CancellationToken ct = default) => UniTask.FromResult(IsInitialized = true);

			public IAPProductInfo                GetProductInfo(string productId)                 => null;
			public IReadOnlyList<IAPProductInfo> GetAllProducts()                                 => Array.Empty<IAPProductInfo>();
			public UniTask<bool>                 RestorePurchasesAsync(CancellationToken ct = default) => UniTask.FromResult(true);
			public bool                          IsProductOwned(string productId)                 => false;
			public bool                          IsSubscribed(string productId)                   => false;
			public DateTime?                     GetSubscriptionExpirationDate(string productId)  => null;
		}
	}
}
