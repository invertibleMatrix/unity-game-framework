using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Transactions;
using AK.Services.Transactions;
using NUnit.Framework;
using UnityEngine;
using AK.Tests.Support;

namespace AK.Tests
{
	public class TransactionServiceTests
	{
		private readonly List<Object> _created = new();

		private TransactionType Type(string name)
		{
			var asset = ScriptableObject.CreateInstance<TransactionType>();
			asset.name = name;
			asset.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);
			_created.Add(asset);
			return asset;
		}

		[SetUp]
		public void SetUp()
		{
			TransactionLedgerState.DeleteSave();
		}

		[TearDown]
		public void TearDown()
		{
			TransactionLedgerState.DeleteSave();
			foreach (var o in _created) Object.DestroyImmediate(o);
			_created.Clear();
		}

		[Test]
		public void Record_MintsRuntimeIdentity_AndCounts()
		{
			var levelComplete = Type("LevelComplete");
			var service = new TransactionService();

			Transaction first = service.Record(levelComplete, 1f, "test").Value;
			Transaction second = service.Record(levelComplete.IdAs<TransactionType>(), 2f, "test").Value;

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
			new TransactionService().Record(t);
			new TransactionService().Record(t);

			Assert.AreEqual(2, new TransactionService().Count(t));
		}

		[Test]
		public void Reverse_OnlyCredited_AndDecrementsCount()
		{
			var t = Type("T");
			var service = new TransactionService();
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
		public void Query_FiltersByTypeAndStatus()
		{
			var a = Type("A");
			var b = Type("B");
			var service = new TransactionService();
			service.Record(a);
			service.RecordPending(a);
			service.Record(b);

			Assert.AreEqual(2, service.Query(a.IdAs<TransactionType>()).Count);
			Assert.AreEqual(1, service.Query(a.IdAs<TransactionType>(), TransactionStatus.Pending).Count);
			Assert.AreEqual(1, service.Query(b.IdAs<TransactionType>(), TransactionStatus.Credited).Count);
			Assert.AreEqual(1, service.GetPendingTransactions().Count);
		}

		[Test]
		public void Pending_SurvivesReload_WithRewardIdentities()
		{
			var t = Type("Purchase");
			var reward = ScriptableObject.CreateInstance<RewardStub>();
			reward.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);
			_created.Add(reward);

			var seed = new TransactionService();
			Transaction pending = seed.RecordPending(t, new List<IReward> { reward }).Value;

			var recovered = new TransactionService(resolver: new SingleAssetResolver(reward));
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
			var reward = ScriptableObject.CreateInstance<RewardStub>();
			reward.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);
			_created.Add(reward);

			new TransactionService().RecordPending(t, new List<IReward> { reward });

			var recovered = new TransactionService(resolver: new SingleAssetResolver(null));

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

			var seed = new TransactionService();
			seed.Record(new Uid<TransactionType>(retired));
			seed.Record(live);

			var table = ScriptableObject.CreateInstance<UidRedirectTable>();
			_created.Add(table);
			table.Editor_Add(retired, live.Id, "test");

			var migrated = new TransactionService(redirects: table);
			Assert.AreEqual(2, migrated.Count(live));
			Assert.AreEqual(0, migrated.Count(new Uid<TransactionType>(retired)));

			Assert.AreEqual(2, new TransactionService().Count(live), "rewritten ledger persisted");
		}

		[Test]
		public void Record_NullOrIdentityless_FailsWithCode_WithoutLogging()
		{
			var service = new TransactionService();

			Result<Transaction> nullType = service.Record((TransactionType)null);
			Assert.IsTrue(nullType.IsFailed);
			Assert.AreEqual(ErrorCode.NullArgument, nullType.Code);
			Assert.IsNull(nullType.Value);

			Result<Transaction> noId = service.RecordPending(default(Uid<TransactionType>));
			Assert.AreEqual(ErrorCode.NoIdentity, noId.Code);
			Assert.AreEqual(0, service.Query(default).Count, "nothing was recorded");
		}

		[Test]
		public void Batch_DefersDiskWrite_UntilScopeEnds()
		{
			var t = Type("T");
			var service = new TransactionService();

			using (service.BeginBatch())
			{
				service.Record(t);
				service.Record(t);
				service.Record(t);

				Assert.AreEqual(3, service.Count(t), "in-memory count is live inside the batch");
				Assert.AreEqual(0, new TransactionService().Count(t), "nothing reached disk yet");
			}

			Assert.AreEqual(3, new TransactionService().Count(t), "one flush at scope end");
		}

		[Test]
		public void Batch_Nested_FlushesOnce_AtOutermost()
		{
			var t = Type("T");
			var service = new TransactionService();

			using (service.BeginBatch())
			{
				service.Record(t);
				using (service.BeginBatch())
				{
					service.Record(t);
				}

				Assert.AreEqual(0, new TransactionService().Count(t), "inner dispose must not flush");
			}

			Assert.AreEqual(2, new TransactionService().Count(t));
		}

		[Test]
		public void Batch_WithNoWrites_DoesNotTouchDisk()
		{
			TransactionLedgerState.DeleteSave();
			var service = new TransactionService();

			using (service.BeginBatch()) { }

			Assert.IsFalse(TransactionLedgerState.HasSave());
		}

		private sealed class RewardStub : MetaDataAsset, IReward
		{
			public Uid RewardType => Uid.None;
			public void CollectRewards(List<IReward> rewards) => rewards.Add(this);
		}

		private sealed class SingleAssetResolver : IUidResolver
		{
			private readonly UID _asset;
			public SingleAssetResolver(UID asset) => _asset = asset;

			public bool TryResolve(Uid id, out UID asset)
			{
				asset = _asset != null && _asset.Id == id ? _asset : null;
				return asset != null;
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
