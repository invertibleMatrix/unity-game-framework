using System.Threading;
using AK.Kernel.Purchasing;
using Cysharp.Threading.Tasks;

namespace AK.Services
{
	/// <summary>
	/// Fulfils the paid orders a store delivers. See <see cref="IIAPService.SetOrderHandler"/>.
	/// <see cref="PurchaseService"/> is the usual one.
	/// </summary>
	public interface IStoreOrderHandler
	{
		/// <summary>
		/// Validates, records and grants a paid order, and says whether the store may finish it.
		/// The same order can arrive more than once: again after a restart, or when retried.
		/// <see cref="StoreOrder.AnswersPurchase"/> tells the order of the purchase under way from
		/// the others. Called on the main thread.
		/// </summary>
		UniTask<OrderFulfilment> FulfilAsync(StoreOrder order, CancellationToken ct);
	}

	/// <summary>What became of a paid order. See <see cref="IStoreOrderHandler.FulfilAsync"/>.</summary>
	public enum OrderFulfilment : byte
	{
		/// <summary>Granted, now or before. The store finishes the order.</summary>
		Fulfilled = 0,

		/// <summary>
		/// Not granted yet. The order stays unfinished: retried by
		/// <see cref="IIAPService.RetryUnfinishedOrdersAsync"/>, and delivered again at the next launch.
		/// </summary>
		Unfinished = 1,

		/// <summary>
		/// Refused: its receipt is invalid. Nothing is granted, and the order isn't finished or
		/// retried in this session. A store that refunds unfinished purchases refunds it.
		/// </summary>
		Rejected = 2,
	}

	/// <summary>A paid order from a store, waiting to be fulfilled.</summary>
	public readonly struct StoreOrder
	{
		public readonly string ProductId;

		/// <summary>The store's id for the purchase. The same each time the store delivers the order.</summary>
		public readonly string TransactionId;

		/// <summary>The store's receipt, for validation.</summary>
		public readonly string Receipt;

		/// <summary>The product's price as the store lists it; None when unknown.</summary>
		public readonly Money Price;

		/// <summary>
		/// True when the order answers the <see cref="IIAPService.PurchaseAsync"/> call under way.
		/// False for every other delivery: restored, approved after a deferral, unfinished from an
		/// earlier session, bought outside the game, or retried after its purchase stopped waiting.
		/// </summary>
		public readonly bool AnswersPurchase;

		public StoreOrder(string productId, string transactionId, string receipt, Money price, bool answersPurchase = false)
		{
			ProductId       = productId;
			TransactionId   = transactionId;
			Receipt         = receipt;
			Price           = price;
			AnswersPurchase = answersPurchase;
		}

		public override string ToString() => $"{ProductId} ({TransactionId})";
	}
}
