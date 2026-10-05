using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Facts;
using AK.Kernel.Persistence;
using AK.Services.Facts;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests
{
	public class FactServiceTests
	{
		private readonly List<Object> _created = new();

		private InMemoryKeyValueStore _backend;
		private PrefsStore            _store;

		private FactType Fact(string name)
		{
			var asset = ScriptableObject.CreateInstance<FactType>();
			asset.name = name;
			asset.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);
			_created.Add(asset);
			return asset;
		}

		// Every service in a test shares one in-memory store, so a new service reads what an
		// earlier one saved, the way a later launch reads the device's PlayerPrefs.
		private FactService NewService(UidRedirectTable redirects = null) => new(redirects, _store);

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
		public void Record_IncrementsCount_ByIdentity()
		{
			var go = Fact("GoPressed");
			var service = NewService();

			Assert.AreEqual(0, service.Count(go));
			Assert.IsFalse(service.HasOccurred(go));

			service.Record(go);
			service.Record(go.IdAs<FactType>());

			Assert.AreEqual(2, service.Count(go));
			Assert.AreEqual(2, service.Count(go.IdAs<FactType>()));
			Assert.IsTrue(service.HasOccurred(go.IdAs<FactType>()));
		}

		[Test]
		public void Counts_SurviveReload()
		{
			var go = Fact("GoPressed");
			NewService().Record(go);
			NewService().Record(go);

			Assert.AreEqual(2, NewService().Count(go));
		}

		[Test]
		public void Counts_KeyedByIdentity_NotByAssetInstance()
		{
			var original = Fact("GoPressed");
			NewService().Record(original);

			var reloaded = ScriptableObject.CreateInstance<FactType>();
			reloaded.name = "RenamedLater";
			reloaded.Editor_AssignIdentity(original.Id, UidProvenance.Minted, string.Empty);
			_created.Add(reloaded);

			Assert.AreEqual(1, NewService().Count(reloaded));
		}

		[Test]
		public void SetCount_Zero_RemovesRow()
		{
			var go = Fact("GoPressed");
			var service = NewService();
			service.Record(go);
			service.SetCount(go, 0);

			Assert.AreEqual(0, service.Count(go));
			Assert.AreEqual(0, NewService().Count(go));
		}

		[Test]
		public void Changed_FiresWithTypedIdentity()
		{
			var go = Fact("GoPressed");
			var service = NewService();
			Uid<FactType> received = default;
			service.Changed += id => received = id;

			service.Record(go);
			Assert.AreEqual(go.IdAs<FactType>(), received);
		}

		[Test]
		public void AreMet_RequiresEveryCondition()
		{
			var a = Fact("A");
			var b = Fact("B");
			var service = NewService();
			service.Record(a);
			service.Record(a);

			var conditions = new List<FactCondition>
			{
				new() { Type = a, MinCount = 2 },
				new() { Type = b, MinCount = 1 },
			};

			Assert.IsFalse(service.AreMet(conditions));
			service.Record(b);
			Assert.IsTrue(service.AreMet(conditions));
			Assert.IsTrue(service.AreMet(null));
		}

		[Test]
		public void AreMet_ConditionWithoutIdentity_IsNeverMet()
		{
			var empty = ScriptableObject.CreateInstance<FactType>();
			_created.Add(empty);
			var service = NewService();

			Assert.IsFalse(service.AreMet(new List<FactCondition> { new() { Type = empty, MinCount = 0 } }));
			Assert.IsFalse(service.AreMet(new List<FactCondition> { new() { Type = null, MinCount = 0 } }));
		}

		[Test]
		public void Redirects_MergeCountsAtLoad_AndRewriteFile()
		{
			var live = Fact("Live");
			Uid retired = Uid.NewRandom();

			var seed = NewService();
			seed.Record(live);
			seed.Record(new Uid<FactType>(retired));
			seed.Record(new Uid<FactType>(retired));

			var table = ScriptableObject.CreateInstance<UidRedirectTable>();
			_created.Add(table);
			table.Editor_Add(retired, live.Id, "test");

			var migrated = NewService(table);
			Assert.AreEqual(3, migrated.Count(live));
			Assert.AreEqual(0, migrated.Count(new Uid<FactType>(retired)), "retired identity is folded into the live one");

			var afterRewrite = NewService();
			Assert.AreEqual(3, afterRewrite.Count(live), "the merged ledger was persisted; no table needed on later loads");
		}

		[Test]
		public void FindOrphans_ReportsUnresolvableIdentities()
		{
			var live = Fact("Live");
			Uid ghost = Uid.NewRandom();

			var service = NewService();
			service.Record(live);
			service.Record(new Uid<FactType>(ghost));

			var resolver = new SingleAssetResolver(live);
			IReadOnlyList<Uid> orphans = service.FindOrphans(resolver);

			Assert.AreEqual(1, orphans.Count);
			Assert.AreEqual(ghost, orphans[0]);
			Assert.AreEqual(1, service.Count(new Uid<FactType>(ghost)), "orphan rows are kept, not healed or dropped");
		}

		[Test]
		public void ResetAll_ClearsEverything()
		{
			var go = Fact("GoPressed");
			var service = NewService();
			service.Record(go);
			service.ResetAll();

			Assert.AreEqual(0, service.Count(go));
			Assert.AreEqual(0, NewService().Count(go));
		}

		[Test]
		public void Batch_DefersDiskWrite_ButCountsAndEventsAreLive()
		{
			var go = Fact("GoPressed");
			var service = NewService();
			int changedEvents = 0;
			service.Changed += _ => changedEvents++;

			using (service.BeginBatch())
			{
				service.Record(go);
				service.Record(go);
				service.Record(go);

				Assert.AreEqual(3, service.Count(go));
				Assert.AreEqual(3, changedEvents, "Changed fires per record, batching only defers the disk write");
				Assert.AreEqual(0, NewService().Count(go), "nothing reached disk inside the batch");
			}

			Assert.AreEqual(3, NewService().Count(go), "one flush at scope end");
		}

		[Test]
		public void Batch_FlushesOnException_Too()
		{
			var go = Fact("GoPressed");
			var service = NewService();

			Assert.Throws<InvalidOperationException>(() =>
			{
				using (service.BeginBatch())
				{
					service.Record(go);
					throw new InvalidOperationException("boom");
				}
			});

			Assert.AreEqual(1, NewService().Count(go), "the record made before the throw still reached disk");
		}

		// ---------------------------------------------------------------- accounts

		[Test]
		public void EachAccount_HasItsOwnCounts()
		{
			var go = Fact("GoPressed");
			var service = NewService();
			service.BindAccount("ada");
			service.Record(go);
			service.Record(go);

			service.BindAccount("ben");

			Assert.AreEqual("ben", service.AccountId);
			Assert.AreEqual(0, service.Count(go), "a second account starts with no counts");

			service.Record(go);
			service.BindAccount("ada");

			Assert.AreEqual(2, service.Count(go), "switching back restores the first account's counts");

			var restarted = NewService();
			restarted.BindAccount("ben");

			Assert.AreEqual(1, restarted.Count(go), "and each survives a restart");
		}

		[Test]
		public void TheFirstAccountBound_TakesOverTheDevicesLedger()
		{
			var go = Fact("GoPressed");
			NewService().Record(go);

			var service = NewService();
			using (ExpectedLog.Info("device's fact ledger moved"))
			{
				service.BindAccount("ada");
			}

			Assert.AreEqual(1, service.Count(go));
			Assert.IsFalse(_store.Has("UGFW_FACT_LEDGER"), "moved, not copied");
			Assert.IsTrue(_store.Has("UGFW_FACT_LEDGER@ada"));

			service.BindAccount("ben");

			Assert.AreEqual(0, service.Count(go), "the device's ledger went to one account only");
		}

		[Test]
		public void AnAccountWithALedger_KeepsIt_AndTheDevicesStaysPut()
		{
			var go = Fact("GoPressed");
			var service = NewService();
			service.BindAccount("ada");
			service.Record(go);

			service.BindAccount(null);
			Assert.IsNull(service.AccountId, "null binds the device's ledger again");
			service.Record(go);
			service.Record(go);

			var restarted = NewService();
			restarted.BindAccount("ada");

			Assert.AreEqual(1, restarted.Count(go));
			Assert.IsTrue(_store.Has("UGFW_FACT_LEDGER"));
		}

		[Test]
		public void SwitchingAccounts_RaisesChanged_ForEachFactWhoseCountDiffers()
		{
			var same    = Fact("Same");
			var onlyAda = Fact("OnlyAda");
			var onlyBen = Fact("OnlyBen");
			var differs = Fact("Differs");
			var service = NewService();

			service.BindAccount("ben");
			service.Record(same);
			service.Record(onlyBen);
			service.Record(differs);

			service.BindAccount("ada");
			service.Record(same);
			service.Record(onlyAda);
			service.Record(differs);
			service.Record(differs);

			var changed = new List<Uid<FactType>>();
			service.Changed += changed.Add;

			service.BindAccount("ben");

			CollectionAssert.AreEquivalent(new[] { onlyAda.IdAs<FactType>(), onlyBen.IdAs<FactType>(), differs.IdAs<FactType>() }, changed);
		}

		[Test]
		public void BindingTheBoundAccount_DoesNothing_AndEmptyMeansTheDevice()
		{
			var a = Fact("A");
			var service = NewService();
			service.BindAccount("ada");
			service.Record(a);
			int changes = 0;
			service.Changed += _ => changes++;

			service.BindAccount("ada");

			Assert.AreEqual(0, changes);
			Assert.AreEqual(1, service.Count(a));

			service.BindAccount("");

			Assert.IsNull(service.AccountId);
			Assert.AreEqual(0, service.Count(a), "the device's ledger, which is empty");
		}

		[Test]
		public void BindingInsideABatch_Throws()
		{
			var service = NewService();

			Assert.Throws<InvalidOperationException>(() =>
			{
				using (service.BeginBatch())
				{
					service.BindAccount("ada");
				}
			});

			Assert.IsNull(service.AccountId);
		}

		[Test]
		public void BindAccount_RejectsAnIdWithAControlCharacter()
		{
			Assert.Throws<ArgumentException>(() => NewService().BindAccount("a\nb"));
		}

		// ---------------------------------------------------------------- store

		[Test]
		public void Records_AreLeftForTheStoresFlush()
		{
			var service = NewService();
			int flushes = _backend.FlushCount;

			service.Record(Fact("A"));

			Assert.AreEqual(flushes, _backend.FlushCount, "facts don't flush on their own; the store flushes at the end of the frame");
			Assert.IsTrue(_store.Has("UGFW_FACT_LEDGER"));
		}

		[Test]
		public void WhenTheStoreDeletesEverything_CountsGoBackToZero_AndChangedFires()
		{
			var go = Fact("GoPressed");
			var service = NewService();
			service.BindAccount("ada");
			service.Record(go);
			var changed = new List<Uid<FactType>>();
			service.Changed += changed.Add;

			_store.DeleteAll();

			Assert.AreEqual(0, service.Count(go));
			CollectionAssert.AreEqual(new[] { go.IdAs<FactType>() }, changed);
			Assert.AreEqual("ada", service.AccountId, "still bound to the same account");

			service.Record(Fact("Other"));

			var restarted = NewService();
			restarted.BindAccount("ada");
			Assert.AreEqual(0, restarted.Count(go), "the next save didn't bring the deleted count back");
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
