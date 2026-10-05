using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Transactions;
using AK.Kernel.Persistence;
using AK.Kernel.Results;
using AK.Services.Rewards;
using AK.Services.Transactions;
using AK.Tests.Support;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace AK.Tests
{
	public class TransactionServiceTests
	{
		private readonly List<Object> _created = new();

		private InMemoryKeyValueStore _backend;
		private PrefsStore            _store;

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

		// Every service in a test shares one in-memory store, so a new service reads what an
		// earlier one saved, the way a later launch reads the device's PlayerPrefs.
		private TransactionService NewService(IRewardService rewards = null, IUidResolver resolver = null, UidRedirectTable redirects = null)
		{
			return new TransactionService(rewards, resolver, redirects, _store);
		}

		// The service credits synchronously, so its tasks have finished by the time they return.
		private static T Completed<T>(UniTask<T> task)
		{
			Assert.IsTrue(task.Status.IsCompleted(), "the credit finished before returning");
			return task.GetAwaiter().GetResult();
		}

		[SetUp]
		public void SetUp()
		{
			_backend = new InMemoryKeyValueStore();
			_store   = new PrefsStore(_backend);
		}

		[TearDown]
		public void TearDown()
		{
			foreach (var o in _created) Object.DestroyImmediate(o);
			_created.Clear();
		}

		[Test]
		public void Record_MintsRuntimeIdentity_AndCounts()
		{
			var levelComplete = Type("LevelComplete");
			var service = NewService();

			Transaction first = service.Record(levelComplete, 1, "test").Value;
			Transaction second = service.Record(levelComplete.IdAs<TransactionType>(), 2, "test").Value;

			Assert.IsTrue(first.Id.IsSet);
			Assert.IsTrue(second.Id.IsSet);
			Assert.AreNotEqual(first.Id, second.Id);
			Assert.AreEqual(levelComplete.IdAs<TransactionType>(), first.Type);
			Assert.AreEqual(TransactionStatus.Credited, first.Status);
			Assert.AreEqual(2, service.Count(levelComplete));
			Assert.IsTrue(service.HasOccurred(levelComplete.IdAs<TransactionType>()));
		}

		[Test]
		public void Counts_SurviveReload()
		{
			var t = Type("T");
			NewService().Record(t);
			NewService().Record(t);

			Assert.AreEqual(2, NewService().Count(t));
		}

		[Test]
		public void Reverse_OnlyCredited_AndDecrementsCount()
		{
			var t = Type("T");
			var service = NewService();
			Transaction credited = service.Record(t).Value;
			Transaction pending = service.RecordPending(t).Value;

			Assert.IsTrue(service.Reverse(credited.Id).IsOk);
			Assert.AreEqual(ErrorCode.TransactionNotCredited, service.Reverse(credited.Id).Code, "already reversed");
			Assert.AreEqual(ErrorCode.TransactionNotCredited, service.Reverse(pending.Id).Code, "pending cannot be reversed");
			Assert.AreEqual(ErrorCode.NotFound, service.Reverse(Uid.NewRandom()).Code, "unknown");

			Assert.AreEqual(0, service.Count(t));
			Assert.AreEqual(TransactionStatus.Reversed, credited.Status);
		}

		[Test]
		public void Reverse_OfATransactionFromAnEarlierSession_RaisesReversed()
		{
			var t = Type("T");
			Transaction credited = NewService().Record(t).Value;

			var restarted = NewService();
			Transaction reversed = null;
			restarted.Reversed += transaction => reversed = transaction;

			Assert.IsTrue(restarted.Reverse(credited.Id).IsOk);

			Assert.IsNotNull(reversed, "listeners hear of it, as they do for this session's transactions");
			Assert.AreEqual(credited.Id, reversed.Id);
			Assert.AreEqual(TransactionStatus.Reversed, reversed.Status);
			Assert.AreEqual(0, NewService().Count(t));
		}

		[Test]
		public void Query_FiltersByTypeAndStatus()
		{
			var a = Type("A");
			var b = Type("B");
			var service = NewService();
			service.Record(a);
			service.RecordPending(a);
			service.Record(b);

			Assert.AreEqual(2, service.Query(a.IdAs<TransactionType>()).Count);
			Assert.AreEqual(1, service.Query(a.IdAs<TransactionType>(), TransactionStatus.Pending).Count);
			Assert.AreEqual(1, service.Query(b.IdAs<TransactionType>(), TransactionStatus.Credited).Count);
			Assert.AreEqual(1, service.GetPendingTransactions().Count);
		}

		[Test]
		public void TheLedgerAndTheSession_KeepTheNewestEntriesOfEachType_AndEveryPendingOne()
		{
			var t = Type("T");
			var service = NewService();
			Transaction pending = service.RecordPending(t).Value;

			using (service.BeginBatch())
			{
				for (int i = 0; i < TransactionService.MaxEntriesPerType + 10; i++) service.Record(t);
			}

			Assert.AreEqual(TransactionService.MaxEntriesPerType + 1, service.Query(t.IdAs<TransactionType>()).Count);
			Assert.AreEqual(TransactionService.MaxEntriesPerType + 1, TransactionLedgerState.Load(_store).Entries.Count);

			var restarted = NewService();
			IReadOnlyList<Transaction> stillPending = restarted.GetPendingTransactions();

			Assert.AreEqual(TransactionService.MaxEntriesPerType + 10, restarted.Count(t), "counts are never trimmed");
			Assert.AreEqual(1, stillPending.Count, "the oldest entry is pending, so it outlived the trim");
			Assert.AreEqual(pending.Id, stillPending[0].Id);
		}

		[Test]
		public void Pending_SurvivesReload_WithRewardIdentities()
		{
			var t = Type("Purchase");
			var reward = Reward("Coins");

			var seed = NewService();
			Transaction pending = seed.RecordPending(t, new List<IReward> { reward }).Value;

			var recovered = NewService(resolver: new AssetResolver(reward));
			IReadOnlyList<Transaction> pendingAfter = recovered.GetPendingTransactions();

			Assert.AreEqual(1, pendingAfter.Count);
			Assert.AreEqual(pending.Id, pendingAfter[0].Id);
			Assert.AreEqual(t.IdAs<TransactionType>(), pendingAfter[0].Type);
			Assert.IsNotNull(pendingAfter[0].Rewards);
			Assert.AreEqual(1, pendingAfter[0].Rewards.Count);
			Assert.AreSame(reward, pendingAfter[0].Rewards[0]);
		}

		[Test]
		public void Pending_WithUnresolvableReward_IsRecovered_WithoutTheReward()
		{
			var t = Type("Purchase");
			NewService().RecordPending(t, new List<IReward> { Reward("Coins") });

			var recovered = NewService(resolver: new AssetResolver());

			IReadOnlyList<Transaction> pendingAfter;
			using (ExpectedLog.Warning("could not be resolved"))
			{
				pendingAfter = recovered.GetPendingTransactions();
			}

			Assert.AreEqual(1, pendingAfter.Count, "the transaction itself is never lost");
			Assert.IsTrue(pendingAfter[0].Rewards == null || pendingAfter[0].Rewards.Count == 0);
		}

		[Test]
		public void Redirects_RewriteTypeAtLoad()
		{
			var live = Type("Live");
			Uid retired = Uid.NewRandom();

			var seed = NewService();
			seed.Record(new Uid<TransactionType>(retired));
			seed.Record(live);

			var table = ScriptableObject.CreateInstance<UidRedirectTable>();
			_created.Add(table);
			table.Editor_Add(retired, live.Id, "test");

			var migrated = NewService(redirects: table);
			Assert.AreEqual(2, migrated.Count(live));
			Assert.AreEqual(0, migrated.Count(new Uid<TransactionType>(retired)));

			Assert.AreEqual(2, NewService().Count(live), "rewritten ledger persisted");
		}

		[Test]
		public void Record_NullOrIdentityless_FailsWithCode_WithoutLogging()
		{
			var service = NewService();

			Result<Transaction> nullType = service.Record((TransactionType)null);
			Assert.IsTrue(nullType.IsFailed);
			Assert.AreEqual(ErrorCode.NullArgument, nullType.Code);
			Assert.IsNull(nullType.Value);

			Result<Transaction> noId = service.RecordPending(default(Uid<TransactionType>));
			Assert.AreEqual(ErrorCode.NoIdentity, noId.Code);
			Assert.AreEqual(0, service.Query(default).Count, "nothing was recorded");
		}

		[Test]
		public void RecordPending_RejectsANullReward_AndRecordsNothing()
		{
			var service = NewService();

			Result<Transaction> result = service.RecordPending(Type("Purchase"), new List<IReward> { Reward("Coins"), null });

			Assert.AreEqual(ErrorCode.NullArgument, result.Code);
			StringAssert.Contains("rewards[1]", result.Detail);
			Assert.AreEqual(0, service.Query(default).Count);
			Assert.IsFalse(TransactionLedgerState.HasSave(_store));
		}

		// ---------------------------------------------------------------- credit

		[Test]
		public void Credit_GrantsEachReward_InOrder_ThenCountsTheTransaction()
		{
			var t = Type("Purchase");
			RewardStub coins = Reward("Coins"), gems = Reward("Gems");
			var rewards = new RecordingRewardService();
			var service = NewService(rewards);
			int credited = 0;
			service.Credited += _ => credited++;

			Result<Transaction> result = Completed(service.CreditAsync(t.IdAs<TransactionType>(), new List<IReward> { coins, gems }));

			Assert.IsTrue(result.IsOk);
			Assert.AreEqual(TransactionStatus.Credited, result.Value.Status);
			CollectionAssert.AreEqual(new IReward[] { coins, gems }, rewards.Granted);
			Assert.AreEqual(1, service.Count(t));
			Assert.AreEqual(1, credited);
		}

		[Test]
		public void ARewardThatIsntGranted_LeavesTheTransactionPending_AndARetryGrantsOnlyTheRest()
		{
			var t = Type("Purchase");
			RewardStub coins = Reward("Coins"), gems = Reward("Gems"), hat = Reward("Hat");
			var rewards = new RecordingRewardService { Decline = gems };
			var service = NewService(rewards);
			Transaction pending = service.RecordPending(t, new List<IReward> { coins, gems, hat }).Value;

			Result first = Completed(service.CreditAsync(pending));

			Assert.AreEqual(ErrorCode.RewardDeclined, first.Code, "the grant's failure, passed on");
			StringAssert.Contains("reward 1 of transaction", first.Detail);
			Assert.AreEqual(TransactionStatus.Pending, pending.Status);
			Assert.AreEqual(0, service.Count(t), "not counted until every reward is granted");
			CollectionAssert.AreEqual(new IReward[] { coins }, rewards.Granted);

			rewards.Decline = null;
			Result retry = Completed(service.CreditAsync(pending));

			Assert.IsTrue(retry.IsOk);
			Assert.AreEqual(TransactionStatus.Credited, pending.Status);
			CollectionAssert.AreEqual(new IReward[] { coins, gems, hat }, rewards.Granted, "the coins weren't granted twice");
			Assert.AreEqual(1, service.Count(t));
		}

		[Test]
		public void AfterARestart_ACreditResumes_FromTheFirstRewardNotGranted()
		{
			var t = Type("Purchase");
			RewardStub coins = Reward("Coins"), gems = Reward("Gems");
			Completed(NewService(new RecordingRewardService { Decline = gems })
				.CreditAsync(t.IdAs<TransactionType>(), new List<IReward> { coins, gems }));

			var rewards = new RecordingRewardService();
			var restarted = NewService(rewards, new AssetResolver(coins, gems));
			IReadOnlyList<Transaction> pending = restarted.GetPendingTransactions();

			Assert.AreEqual(1, pending.Count);
			Assert.IsTrue(Completed(restarted.CreditAsync(pending[0])).IsOk);
			CollectionAssert.AreEqual(new IReward[] { gems }, rewards.Granted);
			Assert.AreEqual(1, NewService().Count(t));
		}

		[Test]
		public void ARewardWithoutIdentity_HoldsItsPlace_SoACreditAfterARestartResumesCorrectly()
		{
			var t = Type("Purchase");
			var anonymous = ScriptableObject.CreateInstance<RewardStub>();
			_created.Add(anonymous);
			RewardStub gems = Reward("Gems");

			using (ExpectedLog.Warning("has no identity"))
			{
				Completed(NewService(new RecordingRewardService { Decline = gems })
					.CreditAsync(t.IdAs<TransactionType>(), new List<IReward> { anonymous, gems }));
			}

			var rewards = new RecordingRewardService();
			var restarted = NewService(rewards, new AssetResolver(gems));
			IReadOnlyList<Transaction> pending;
			using (ExpectedLog.Warning("Reward 0 of transaction .* had no identity"))
			{
				pending = restarted.GetPendingTransactions();
			}

			Assert.IsTrue(Completed(restarted.CreditAsync(pending[0])).IsOk);
			CollectionAssert.AreEqual(new IReward[] { gems }, rewards.Granted, "the anonymous reward was granted before the restart");
		}

		[Test]
		public void WithoutARewardService_ATransactionIsCredited_WithAWarningThatNothingWasGranted()
		{
			var service = NewService();
			Transaction pending = service.RecordPending(Type("Purchase"), new List<IReward> { Reward("Coins") }).Value;

			Result result;
			using (ExpectedLog.Warning("no IRewardService was provided"))
			{
				result = Completed(service.CreditAsync(pending));
			}

			Assert.IsTrue(result.IsOk);
			Assert.AreEqual(TransactionStatus.Credited, pending.Status);
		}

		[Test]
		public void RewardsRecoveredWithoutAResolver_KeepTheTransactionPending()
		{
			var t = Type("Purchase");
			NewService().RecordPending(t, new List<IReward> { Reward("Coins") });

			var restarted = NewService(new RecordingRewardService());
			IReadOnlyList<Transaction> pending;
			using (ExpectedLog.Warning("no IUidResolver was provided"))
			{
				pending = restarted.GetPendingTransactions();
			}

			Result result = Completed(restarted.CreditAsync(pending[0]));

			Assert.AreEqual(ErrorCode.RewardUnresolved, result.Code, "crediting would lose the rewards on record");
			Assert.AreEqual(TransactionStatus.Pending, pending[0].Status);
			Assert.AreEqual(0, restarted.Count(t));
		}

		// ---------------------------------------------------------------- accounts

		[Test]
		public void EachAccount_HasItsOwnLedger_AndItsOwnSession()
		{
			var t = Type("T");
			var service = NewService();
			service.BindAccount("ada");
			service.Record(t);
			service.RecordPending(t);

			service.BindAccount("ben");

			Assert.AreEqual(0, service.Count(t));
			Assert.AreEqual(0, service.Query(default).Count, "the session's transactions were the other account's");
			Assert.AreEqual(0, service.GetPendingTransactions().Count);

			service.Record(t);
			service.BindAccount("ada");

			Assert.AreEqual(1, service.Count(t));
			Assert.AreEqual(1, service.GetPendingTransactions().Count, "the pending transaction came back with its account's ledger");
		}

		[Test]
		public void TheFirstAccountBound_TakesOverTheDevicesLedger()
		{
			var t = Type("T");
			NewService().Record(t);

			var service = NewService();
			using (ExpectedLog.Info("device's transaction ledger moved"))
			{
				service.BindAccount("ada");
			}

			Assert.AreEqual(1, service.Count(t));
			Assert.IsFalse(_store.Has("UGFW_TRANSACTION_LEDGER"), "moved, not copied");
			Assert.IsTrue(_store.Has("UGFW_TRANSACTION_LEDGER@ada"));
		}

		[Test]
		public void ATransactionOfAnotherAccount_CantBeCredited()
		{
			var t = Type("Purchase");
			var service = NewService(new RecordingRewardService());
			service.BindAccount("ada");
			Transaction pending = service.RecordPending(t, new List<IReward> { Reward("Coins") }).Value;

			service.BindAccount("ben");
			Result result = Completed(service.CreditAsync(pending));

			Assert.AreEqual(ErrorCode.NotFound, result.Code);
			Assert.AreEqual(TransactionStatus.Pending, pending.Status);

			service.BindAccount("ada");

			Assert.IsTrue(Completed(service.CreditAsync(pending)).IsOk, "back on its own account, it credits");
		}

		[Test]
		public void BindingInsideABatch_Throws()
		{
			var service = NewService();

			Assert.Throws<System.InvalidOperationException>(() =>
			{
				using (service.BeginBatch())
				{
					service.BindAccount("ada");
				}
			});

			Assert.IsNull(service.AccountId);
		}

		// ---------------------------------------------------------------- store

		[Test]
		public void EachChange_IsFlushedAtOnce_AndABatchFlushesOnce()
		{
			var t = Type("T");
			var service = NewService();
			int flushes = _backend.FlushCount;

			service.Record(t);

			Assert.AreEqual(flushes + 1, _backend.FlushCount, "a transaction records an exchange, so it doesn't wait for the end of the frame");

			using (service.BeginBatch())
			{
				service.Record(t);
				service.Record(t);
			}

			Assert.AreEqual(flushes + 2, _backend.FlushCount);
		}

		[Test]
		public void WhenTheStoreDeletesEverything_TheLedgerAndTheSessionStartOver()
		{
			var t = Type("T");
			var service = NewService();
			service.BindAccount("ada");
			service.Record(t);
			service.RecordPending(t);

			_store.DeleteAll();

			Assert.AreEqual(0, service.Count(t));
			Assert.AreEqual(0, service.Query(default).Count);
			Assert.AreEqual(0, service.GetPendingTransactions().Count);
			Assert.AreEqual("ada", service.AccountId, "still bound to the same account");

			service.Record(Type("Other"));

			var restarted = NewService();
			restarted.BindAccount("ada");

			Assert.AreEqual(0, restarted.Count(t), "the next save didn't bring the deleted ledger back");
		}

		[Test]
		public void Batch_DefersDiskWrite_UntilScopeEnds()
		{
			var t = Type("T");
			var service = NewService();

			using (service.BeginBatch())
			{
				service.Record(t);
				service.Record(t);
				service.Record(t);

				Assert.AreEqual(3, service.Count(t), "in-memory count is live inside the batch");
				Assert.AreEqual(0, NewService().Count(t), "nothing reached disk yet");
			}

			Assert.AreEqual(3, NewService().Count(t), "one flush at scope end");
		}

		[Test]
		public void Batch_Nested_FlushesOnce_AtOutermost()
		{
			var t = Type("T");
			var service = NewService();

			using (service.BeginBatch())
			{
				service.Record(t);
				using (service.BeginBatch())
				{
					service.Record(t);
				}

				Assert.AreEqual(0, NewService().Count(t), "inner dispose must not flush");
			}

			Assert.AreEqual(2, NewService().Count(t));
		}

		[Test]
		public void Batch_WithNoWrites_DoesNotTouchDisk()
		{
			var service = NewService();

			using (service.BeginBatch()) { }

			Assert.IsFalse(TransactionLedgerState.HasSave(_store));
		}

		// ---------------------------------------------------------------- purchases

		[Test]
		public void AnUnpaidPurchase_IsCreditedOnlyOnceMarkedPaid()
		{
			var t = Type("Purchase");
			RewardStub coins = Reward("Coins");
			var rewards = new RecordingRewardService();
			var service = NewService(rewards);
			Transaction purchase = service.RecordUnpaid(t, new List<IReward> { coins }).Value;

			Assert.IsTrue(purchase.AwaitingPayment);
			Assert.AreEqual(TransactionStatus.Pending, purchase.Status);
			Assert.AreEqual(ErrorCode.TransactionNotPaid, Completed(service.CreditAsync(purchase)).Code);
			CollectionAssert.IsEmpty(rewards.Granted);
			CollectionAssert.IsEmpty(service.GetPendingTransactions(), "under way in this session, not owed");

			Assert.IsTrue(service.MarkPaid(purchase).IsOk);
			Assert.IsFalse(purchase.AwaitingPayment);
			Assert.IsTrue(service.MarkPaid(purchase).IsOk, "marking it paid again changes nothing");
			Assert.AreEqual(1, service.GetPendingTransactions().Count, "owed once paid");

			Assert.IsTrue(Completed(service.CreditAsync(purchase)).IsOk);
			CollectionAssert.AreEqual(new IReward[] { coins }, rewards.Granted);
			Assert.IsTrue(service.MarkPaid(purchase).IsOk, "a credited purchase was paid for");
			Assert.AreEqual(1, service.Count(t));
		}

		[Test]
		public void AnUnpaidPurchase_CanBeAbandoned_ButAPaidOneCant()
		{
			var t = Type("Purchase");
			var service = NewService();
			Transaction declined = service.RecordUnpaid(t).Value;
			Transaction paid = service.RecordUnpaid(t).Value;
			service.MarkPaid(paid);

			Assert.IsTrue(service.Abandon(declined).IsOk);
			Assert.AreEqual(TransactionStatus.Failed, declined.Status);
			Assert.IsTrue(service.Abandon(declined).IsOk, "abandoning it again changes nothing");
			Assert.AreEqual(ErrorCode.TransactionNotPending, service.MarkPaid(declined).Code, "an abandoned purchase can't be paid for after all");

			Assert.AreEqual(ErrorCode.TransactionPaid, service.Abandon(paid).Code);
			Assert.AreEqual(TransactionStatus.Pending, paid.Status, "still owed");

			Assert.AreEqual(ErrorCode.NullArgument, service.Abandon(null).Code);
			Assert.AreEqual(ErrorCode.NullArgument, service.MarkPaid(null).Code);
			Assert.AreEqual(0, service.Count(t));
		}

		[Test]
		public void AfterARestart_AnUnpaidPurchaseIsAbandoned_AndAPaidOneIsOwed()
		{
			var t = Type("Purchase");
			var seed = NewService();
			Transaction unpaid = seed.RecordUnpaid(t).Value;
			Transaction paid = seed.RecordUnpaid(t).Value;
			seed.MarkPaid(paid);

			IReadOnlyList<Transaction> owed;
			using (ExpectedLog.Warning("was never marked paid"))
			{
				owed = NewService().GetPendingTransactions();
			}

			Assert.AreEqual(1, owed.Count);
			Assert.AreEqual(paid.Id, owed[0].Id);
			Assert.IsFalse(owed[0].AwaitingPayment);
			Assert.AreEqual((int)TransactionStatus.Failed, Entry(unpaid.Id).Status, "abandoned on disk");
			Assert.IsFalse(Entry(unpaid.Id).AwaitingPayment);
			Assert.AreEqual(1, NewService().GetPendingTransactions().Count, "the paid one stays owed at every launch");
		}

		[Test]
		public void AnExternalId_IsRecordedOnce_AndFoundAfterARestart()
		{
			var t = Type("Purchase");
			var service = NewService();
			Transaction order = service.RecordPending(t, externalId: "GPA.1234").Value;

			Assert.AreEqual(ErrorCode.DuplicateTransaction, service.RecordPending(t, externalId: "GPA.1234").Code);
			Assert.AreEqual(1, service.Query(t.IdAs<TransactionType>()).Count, "nothing recorded the second time");

			Assert.IsTrue(service.TryFindByExternalId("GPA.1234", out Transaction found));
			Assert.AreSame(order, found, "this session's transaction");

			Completed(service.CreditAsync(order));

			var restarted = NewService();
			Assert.IsTrue(restarted.TryFindByExternalId("GPA.1234", out Transaction recovered));
			Assert.AreEqual(order.Id, recovered.Id);
			Assert.AreEqual(TransactionStatus.Credited, recovered.Status);
			Assert.AreEqual("GPA.1234", recovered.ExternalId);
			Assert.IsFalse(restarted.TryFindByExternalId("gpa.1234", out _), "matched exactly");
			Assert.IsFalse(restarted.TryFindByExternalId(null, out _));
			Assert.IsFalse(restarted.TryFindByExternalId(string.Empty, out _));
			Assert.AreEqual(ErrorCode.DuplicateTransaction, restarted.RecordPending(t, externalId: "GPA.1234").Code);
		}

		[Test]
		public void TransactionsWithoutAnExternalId_AreNeverDuplicates()
		{
			var t = Type("Purchase");
			var service = NewService();

			Assert.IsTrue(service.RecordPending(t).IsOk);
			Assert.IsTrue(service.RecordPending(t, externalId: string.Empty).IsOk);
			Assert.IsTrue(NewService().RecordPending(t).IsOk, "after a restart too");
		}

		[Test]
		public void Amounts_AreWholeNumbers_KeptExactly()
		{
			const long amount = 9_007_199_254_740_993L; // 2^53 + 1: neither a float nor a double holds it

			Transaction recorded = NewService().Record(Type("Coins"), amount).Value;

			Assert.AreEqual(amount, recorded.Amount);
			Assert.AreEqual(amount, TransactionLedgerState.Load(_store).Entries[0].Amount);
		}

		[Test]
		public void ALedgerSavedBeforeThisVersion_LoadsItsFloatAmounts_AndItsPendingEntriesAsOwed()
		{
			var t = Type("Purchase");
			var seed = NewService();
			seed.Record(t);
			Transaction pending = seed.RecordPending(t).Value;

			// As saved before amounts were whole and purchases could be unpaid: float amounts, and
			// no external ids or payment flags.
			string key = LedgerKey();
			Assert.IsTrue(_backend.TryGet(key, out string json));
			json = Replace(json, "\"Amount\":1,", "\"Amount\":1.0,", 2);
			json = Replace(json, "\"ExternalId\":\"\",", string.Empty, 2);
			json = Replace(json, "\"AwaitingPayment\":false,", string.Empty, 2);
			_backend.Set(key, json);

			var restarted = new TransactionService(null, null, null, new PrefsStore(_backend));

			Assert.AreEqual(1, restarted.Count(t));
			IReadOnlyList<Transaction> owed = restarted.GetPendingTransactions();
			Assert.AreEqual(1, owed.Count, "a pending entry from then was paid for, so it is owed");
			Assert.AreEqual(pending.Id, owed[0].Id);
			Assert.AreEqual(1L, owed[0].Amount);
			Assert.IsTrue(Completed(restarted.CreditAsync(owed[0])).IsOk);
		}

		private PersistedTransactionEntry Entry(Uid id) => TransactionLedgerState.Load(_store).Entries.Find(e => e.Id == id);

		private string LedgerKey()
		{
			string found = null;
			foreach (string key in _backend.Keys)
			{
				if (!key.Contains("UGFW_TRANSACTION_LEDGER")) continue;

				Assert.IsNull(found, "one ledger");
				found = key;
			}

			Assert.IsNotNull(found, "the ledger was saved");
			return found;
		}

		private static string Replace(string text, string old, string replacement, int count)
		{
			int found = 0;
			for (int at = text.IndexOf(old, System.StringComparison.Ordinal); at >= 0; at = text.IndexOf(old, at + old.Length, System.StringComparison.Ordinal))
			{
				found++;
			}

			Assert.AreEqual(count, found, $"{old} in the saved ledger: {text}");
			return text.Replace(old, replacement);
		}

		private sealed class RewardStub : MetaDataAsset, IReward
		{
			public Uid RewardType => Uid.None;
			public void CollectRewards(List<IReward> rewards) => rewards.Add(this);
		}

		private sealed class RecordingRewardService : IRewardService
		{
			public readonly List<IReward> Granted = new();

			// Refused the way a provider that can't provide a reward refuses it.
			public IReward Decline;

			public Result Grant(IReward reward)
			{
				if (ReferenceEquals(reward, Decline)) return Result.Fail(ErrorCode.RewardDeclined, "declined by the test");

				Granted.Add(reward);
				return Result.Ok;
			}

			public void RegisterProvider(IRewardProvider provider) { }
			public bool UnregisterProvider(IRewardProvider provider) => false;
			public IRewardProvider GetProvider(Uid rewardType) => null;
		}

		private sealed class AssetResolver : IUidResolver
		{
			private readonly UID[] _assets;
			public AssetResolver(params UID[] assets) => _assets = assets;

			public bool TryResolve(Uid id, out UID asset)
			{
				foreach (UID candidate in _assets)
				{
					if (candidate.HasIdentity && candidate.Id == id)
					{
						asset = candidate;
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
	}
}
