using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Facts;
using AK.Services.Facts;
using NUnit.Framework;
using UnityEngine;

namespace AK.Tests
{
	public class FactServiceTests
	{
		private readonly List<Object> _created = new();

		private FactType Fact(string name)
		{
			var asset = ScriptableObject.CreateInstance<FactType>();
			asset.name = name;
			asset.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);
			_created.Add(asset);
			return asset;
		}

		[SetUp]
		public void SetUp()
		{
			FactLedgerState.DeleteSave();
		}

		[TearDown]
		public void TearDown()
		{
			FactLedgerState.DeleteSave();
			foreach (var o in _created) Object.DestroyImmediate(o);
			_created.Clear();
		}

		[Test]
		public void Record_IncrementsCount_ByIdentity()
		{
			var go = Fact("GoPressed");
			var service = new FactService();

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
			new FactService().Record(go);
			new FactService().Record(go);

			Assert.AreEqual(2, new FactService().Count(go));
		}

		[Test]
		public void Counts_KeyedByIdentity_NotByAssetInstance()
		{
			var original = Fact("GoPressed");
			new FactService().Record(original);

			var reloaded = ScriptableObject.CreateInstance<FactType>();
			reloaded.name = "RenamedLater";
			reloaded.Editor_AssignIdentity(original.Id, UidProvenance.Minted, string.Empty);
			_created.Add(reloaded);

			Assert.AreEqual(1, new FactService().Count(reloaded));
		}

		[Test]
		public void SetCount_Zero_RemovesRow()
		{
			var go = Fact("GoPressed");
			var service = new FactService();
			service.Record(go);
			service.SetCount(go, 0);

			Assert.AreEqual(0, service.Count(go));
			Assert.AreEqual(0, new FactService().Count(go));
		}

		[Test]
		public void Changed_FiresWithTypedIdentity()
		{
			var go = Fact("GoPressed");
			var service = new FactService();
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
			var service = new FactService();
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
			var service = new FactService();

			Assert.IsFalse(service.AreMet(new List<FactCondition> { new() { Type = empty, MinCount = 0 } }));
			Assert.IsFalse(service.AreMet(new List<FactCondition> { new() { Type = null, MinCount = 0 } }));
		}

		[Test]
		public void Redirects_MergeCountsAtLoad_AndRewriteFile()
		{
			var live = Fact("Live");
			Uid retired = Uid.NewRandom();

			var seed = new FactService();
			seed.Record(live);
			seed.Record(new Uid<FactType>(retired));
			seed.Record(new Uid<FactType>(retired));

			var table = ScriptableObject.CreateInstance<UidRedirectTable>();
			_created.Add(table);
			table.Editor_Add(retired, live.Id, "test");

			var migrated = new FactService(table);
			Assert.AreEqual(3, migrated.Count(live));
			Assert.AreEqual(0, migrated.Count(new Uid<FactType>(retired)), "retired identity is folded into the live one");

			var afterRewrite = new FactService();
			Assert.AreEqual(3, afterRewrite.Count(live), "the merged ledger was persisted; no table needed on later loads");
		}

		[Test]
		public void FindOrphans_ReportsUnresolvableIdentities()
		{
			var live = Fact("Live");
			Uid ghost = Uid.NewRandom();

			var service = new FactService();
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
			var service = new FactService();
			service.Record(go);
			service.ResetAll();

			Assert.AreEqual(0, service.Count(go));
			Assert.AreEqual(0, new FactService().Count(go));
		}

		[Test]
		public void Batch_DefersDiskWrite_ButCountsAndEventsAreLive()
		{
			var go = Fact("GoPressed");
			var service = new FactService();
			int changedEvents = 0;
			service.Changed += _ => changedEvents++;

			using (service.BeginBatch())
			{
				service.Record(go);
				service.Record(go);
				service.Record(go);

				Assert.AreEqual(3, service.Count(go));
				Assert.AreEqual(3, changedEvents, "Changed fires per record, batching only defers the disk write");
				Assert.AreEqual(0, new FactService().Count(go), "nothing reached disk inside the batch");
			}

			Assert.AreEqual(3, new FactService().Count(go), "one flush at scope end");
		}

		[Test]
		public void Batch_FlushesOnException_Too()
		{
			var go = Fact("GoPressed");
			var service = new FactService();

			Assert.Throws<System.InvalidOperationException>(() =>
			{
				using (service.BeginBatch())
				{
					service.Record(go);
					throw new System.InvalidOperationException("boom");
				}
			});

			Assert.AreEqual(1, new FactService().Count(go), "the record made before the throw still reached disk");
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
