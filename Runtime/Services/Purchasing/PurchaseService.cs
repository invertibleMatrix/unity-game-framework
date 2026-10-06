using System;
using System.Collections.Generic;
using System.Threading;
using AK.CoreDomain;
using AK.CoreDomain.Transactions;
using AK.Kernel.Results;
using AK.Services.Costs;
using AK.Services.Transactions;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// Sells items for the game's currencies or through a store. Every purchase is a saga on the
	/// transaction ledger (<see cref="AK.Kernel.Purchasing.PurchaseSaga"/>), written to disk step by
	/// step, so a purchase cut off at any point resumes or unwinds: a payment isn't lost, and
	/// nothing is granted twice or without one.
	///
	/// For currency: the cost is checked, the purchase is recorded unpaid, the cost is deducted,
	/// the purchase is marked paid, its rewards are granted one by one, and it is credited. A
	/// declined deduction abandons the record, and so does a restart before it was marked paid.
	/// Once paid, a purchase only moves forward: a reward that isn't granted keeps it pending,
	/// and <see cref="GrantPendingCredits"/> resumes it.
	///
	/// Through a store (a non-empty ProductID): the store takes the payment and delivers a paid
	/// order. Every order goes the same way, whether bought here, restored, approved after a
	/// deferral, bought from the store's own page, or left unfinished by an earlier session: its
	/// receipt is validated when a validator is given, the order is recorded once by its store
	/// transaction id, its rewards are granted, and only then is the order finished at the store.
	/// An order not finished comes back at the next launch, so a crash can't lose it.
	///
	/// Every expected failure is a <see cref="Result"/> code the UI can map to a message:
	/// <see cref="ErrorCode.CannotAfford"/>, <see cref="ErrorCode.Cancelled"/>, the 6xx store
	/// codes, and so on. Nothing here throws for a declined purchase.
	///
	/// The ledger writes every step to disk at once. Save the balances and items the providers
	/// change at least as soon, when the purchase is credited, or a crash can undo a deduction or
	/// a grant the ledger already holds.
	/// </summary>
	public class PurchaseService : IPurchaseService
	{
		private readonly ICostService        _costs;
		private readonly ITransactionService _ledger;
		private readonly IIAPService         _store;
		private readonly IPurchasableCatalog _catalog;
		private readonly IReceiptValidator   _validator;

		// What each product bought in this session sells, so an order that arrives after its
		// purchase stopped waiting (a timeout, a deferral, a cancelled wait) finds its item.
		private readonly Dictionary<string, IPurchasable> _bought = new();

		// The store purchase PurchaseFromStore is waiting on, if any.
		private StorePurchase _inFlight;

		// Store orders credited by the order handler, so GrantPendingCredits counts the ones its
		// retry credits.
		private int _ordersCredited;

		public IIAPService IAPService => _store;

		/// <param name="costs">Checks and deducts currency costs.</param>
		/// <param name="ledger">Records every purchase. Its IRewardService grants the rewards.</param>
		/// <param name="store">The store, for items with a ProductID; null for a game without one. The service becomes its order handler.</param>
		/// <param name="catalog">What each store product sells, for orders that arrive without a purchase under way.</param>
		/// <param name="validator">Checks store receipts before anything is granted; null to trust the store.</param>
		public PurchaseService(ICostService costs, ITransactionService ledger, IIAPService store = null,
		                       IPurchasableCatalog catalog = null, IReceiptValidator validator = null)
		{
			_costs     = costs  ?? throw new ArgumentNullException(nameof(costs));
			_ledger    = ledger ?? throw new ArgumentNullException(nameof(ledger));
			_store     = store;
			_catalog   = catalog;
			_validator = validator;

			_store?.SetOrderHandler(new OrderHandler(this));
		}

		public UniTask<Result> Purchase(IPurchasable item, bool immediateCredit = true, CancellationToken ct = default)
		{
			if (item == null) return UniTask.FromResult(Result.Fail(ErrorCode.NullArgument, "item"));

			return string.IsNullOrEmpty(item.ProductID)
				? PurchaseWithCurrency(item, immediateCredit, ct)
				: PurchaseFromStore(item, immediateCredit, ct);
		}

		public async UniTask<int> GrantPendingCredits(CancellationToken ct = default)
		{
			int credited = 0;

			IReadOnlyList<Transaction> owed = _ledger.GetPendingTransactions();
			for (int i = 0; i < owed.Count; i++)
			{
				Result result = await _ledger.CreditAsync(owed[i], ct);
				if (result.IsOk) credited++;
			}

			// Finishes the store orders credited just now, and tries the others again.
			if (_store != null && _store.IsInitialized)
			{
				int before = _ordersCredited;
				await _store.RetryUnfinishedOrdersAsync(ct);
				credited += _ordersCredited - before;
			}

			return credited;
		}

		// ---------------------------------------------------------------- currency

		private async UniTask<Result> PurchaseWithCurrency(IPurchasable item, bool immediateCredit, CancellationToken ct)
		{
			ICostInfo cost = item.Cost;
			if (cost == null || cost.CostType.IsNone)
			{
				return Result.Fail(ErrorCode.NoIdentity, $"'{item.DisplayName}' has no cost type; an item sold for currency without one would be given away");
			}

			Result affordable = _costs.CanAfford(cost);
			if (affordable.IsFailed) return affordable;

			// Recorded before the deduction: one found unpaid after a restart is abandoned, and
			// one marked paid is owed until granted.
			Result<Transaction> recorded = _ledger.RecordUnpaid(item.TransactionType, CollectRewards(item));
			if (recorded.IsFailed) return recorded.Untyped;

			Transaction purchase = recorded.Value;

			Result paid = _costs.Deduct(cost);
			if (paid.IsFailed)
			{
				Result abandoned = _ledger.Abandon(purchase);
				if (abandoned.IsFailed)
				{
					Debug.LogError($"[PurchaseService] '{item.DisplayName}' couldn't be abandoned after its payment was declined: {abandoned}. It will be at the next launch.");
				}

				return paid;
			}

			Result marked = _ledger.MarkPaid(purchase);
			if (marked.IsFailed)
			{
				// It was recorded a moment ago in this account's ledger, so this is a bug.
				throw new InvalidOperationException($"[PurchaseService] '{item.DisplayName}' was paid for, but the ledger wouldn't record the payment: {marked}");
			}

			return immediateCredit ? await _ledger.CreditAsync(purchase, ct) : Result.Ok;
		}

		// ---------------------------------------------------------------- store

		private async UniTask<Result> PurchaseFromStore(IPurchasable item, bool immediateCredit, CancellationToken ct)
		{
			if (_store == null)
			{
				// Never silently charge currency for a store product.
				return Result.Fail(ErrorCode.StoreNotInitialized, $"'{item.DisplayName}' has a ProductID but no IIAPService was provided");
			}

			if (!_store.IsInitialized) return Result.Fail(ErrorCode.StoreNotInitialized);

			if (_inFlight != null)
			{
				return Result.Fail(ErrorCode.PurchaseInProgress, $"'{_inFlight.Item.DisplayName}' is being bought");
			}

			var purchase = new StorePurchase(item, immediateCredit);
			_inFlight = purchase;
			_bought[item.ProductID] = item;

			try
			{
				IAPPurchaseResult result = await _store.PurchaseAsync(item.ProductID, ct);
				if (!result.Success) return Result.Fail(MapIAPFailure(result.FailureType), result.FailureReason);

				return purchase.HasOutcome
					? purchase.Outcome
					: Result.Fail(ErrorCode.StoreUnknown, "the store reported the purchase, but its order didn't reach the order handler");
			}
			finally
			{
				_inFlight = null;
			}
		}

		private async UniTask<OrderFulfilment> Fulfil(StoreOrder order, CancellationToken ct)
		{
			// The purchase under way, when the order answers it.
			StorePurchase purchase = order.AnswersPurchase && _inFlight != null && _inFlight.Item.ProductID == order.ProductId ? _inFlight : null;

			if (string.IsNullOrEmpty(order.TransactionId))
			{
				purchase?.Report(Result.Fail(ErrorCode.StoreError, "the store delivered an order without a transaction id"));
				return OrderFulfilment.Unfinished;
			}

			// An order on record, delivered again: finish it, or grant what it still owes.
			if (_ledger.TryFindByExternalId(order.TransactionId, out Transaction recorded))
			{
				return await Settle(recorded, purchase, ct);
			}

			IPurchasable item = FindItem(purchase, order.ProductId);
			if (item == null)
			{
				Debug.LogWarning($"[PurchaseService] Nothing is sold as store product '{order.ProductId}', so order {order.TransactionId} stays unfinished. Pass an IPurchasableCatalog that knows the product.");
				return OrderFulfilment.Unfinished;
			}

			if (_validator != null)
			{
				ReceiptVerdict verdict = await _validator.ValidateAsync(order, ct);
				if (verdict != ReceiptVerdict.Valid)
				{
					bool invalid = verdict == ReceiptVerdict.Invalid;
					purchase?.Report(Result.Fail(invalid ? ErrorCode.ReceiptRejected : ErrorCode.ReceiptUnverified, order.ToString()));
					return invalid ? OrderFulfilment.Rejected : OrderFulfilment.Unfinished;
				}

				// Another delivery of the order may have recorded it while this one waited.
				if (_ledger.TryFindByExternalId(order.TransactionId, out recorded))
				{
					return await Settle(recorded, purchase, ct);
				}
			}

			Result<Transaction> pending = _ledger.RecordPending(item.TransactionType, CollectRewards(item), order.ProductId, order.TransactionId);
			if (pending.IsFailed)
			{
				Debug.LogWarning($"[PurchaseService] Order {order} couldn't be recorded, so it stays unfinished: {pending}");
				purchase?.Report(pending.Untyped);
				return OrderFulfilment.Unfinished;
			}

			// Bought to be credited later: the order is finished once GrantPendingCredits credits it.
			if (purchase != null && !purchase.ImmediateCredit)
			{
				purchase.Report(Result.Ok);
				return OrderFulfilment.Unfinished;
			}

			return await Settle(pending.Value, purchase, ct);
		}

		/// <summary>Credits an order's transaction if it is owed. The order is finished once it is credited.</summary>
		private async UniTask<OrderFulfilment> Settle(Transaction transaction, StorePurchase purchase, CancellationToken ct)
		{
			switch (transaction.Status)
			{
				case TransactionStatus.Pending:
					Result credited = await _ledger.CreditAsync(transaction, ct);
					if (credited.IsOk) _ordersCredited++;

					purchase?.Report(credited);
					return credited.IsOk ? OrderFulfilment.Fulfilled : OrderFulfilment.Unfinished;

				case TransactionStatus.Credited:
				case TransactionStatus.Reversed:
					// Granted before. A reversed one was taken back on purpose, not to be granted again.
					purchase?.Report(Result.Ok);
					return OrderFulfilment.Fulfilled;

				default:
					purchase?.Report(Result.Fail(ErrorCode.TransactionNotPending, transaction.Status.ToString()));
					return OrderFulfilment.Rejected;
			}
		}

		// ---------------------------------------------------------------- helpers

		// What the product sells: the purchase under way's item, one bought earlier in the session, or the catalog's.
		private IPurchasable FindItem(StorePurchase purchase, string productId)
		{
			if (purchase != null) return purchase.Item;
			if (productId == null) return null;
			if (_bought.TryGetValue(productId, out IPurchasable bought)) return bought;

			return _catalog != null && _catalog.TryGetByProductId(productId, out IPurchasable item) ? item : null;
		}

		// The transaction keeps the reward list for its lifetime, so it can't come from the pool.
		private static List<IReward> CollectRewards(IPurchasable item)
		{
			var rewards = new List<IReward>();
			item.CollectRewards(rewards);
			return rewards.Count > 0 ? rewards : null;
		}

		private static ErrorCode MapIAPFailure(IAPFailureType failureType)
		{
			return failureType switch
			{
				IAPFailureType.UserCancelled           => ErrorCode.Cancelled,
				IAPFailureType.NotInitialized          => ErrorCode.StoreNotInitialized,
				IAPFailureType.ProductUnavailable      => ErrorCode.ProductUnavailable,
				IAPFailureType.PaymentDeclined         => ErrorCode.PaymentDeclined,
				IAPFailureType.StoreError              => ErrorCode.StoreError,
				IAPFailureType.DuplicateTransaction    => ErrorCode.DuplicateTransaction,
				IAPFailureType.Timeout                 => ErrorCode.Timeout,
				IAPFailureType.ExistingPurchasePending => ErrorCode.PurchaseInProgress,
				IAPFailureType.Deferred                => ErrorCode.PurchaseDeferred,
				_                                      => ErrorCode.StoreUnknown
			};
		}

		/// <summary>A store purchase under way, and what its order's fulfilment reported.</summary>
		private sealed class StorePurchase
		{
			public readonly IPurchasable Item;
			public readonly bool         ImmediateCredit;

			public bool   HasOutcome { get; private set; }
			public Result Outcome    { get; private set; }

			public StorePurchase(IPurchasable item, bool immediateCredit)
			{
				Item            = item;
				ImmediateCredit = immediateCredit;
			}

			// The first fulfilment of its order speaks for the purchase; a retry doesn't change it.
			public void Report(Result outcome)
			{
				if (HasOutcome) return;

				HasOutcome = true;
				Outcome    = outcome;
			}
		}

		// Registered with the store in place of the service, so the service's public type doesn't
		// carry the handler interface.
		private sealed class OrderHandler : IStoreOrderHandler
		{
			private readonly PurchaseService _service;

			public OrderHandler(PurchaseService service) => _service = service;

			public UniTask<OrderFulfilment> FulfilAsync(StoreOrder order, CancellationToken ct) => _service.Fulfil(order, ct);
		}
	}
}
