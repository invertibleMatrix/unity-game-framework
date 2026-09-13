using System;
using System.Collections.Generic;
using AK.Core.Collections;
using AK.Utilities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using AK.Tests.Support;
using Object = UnityEngine.Object;

namespace AK.Tests
{
	public class ObjectPoolServiceTests
	{
		private sealed class Marker : PoolableObject
		{
			public int Gets;
			public int Returns;

			public override void OnGetFromPool()   => Gets++;
			public override void OnReturnToPool() => Returns++;
		}

		private readonly List<Object> _created = new();
		private GameObject _prefab;

		[SetUp]
		public void SetUp()
		{
			_prefab = new GameObject("PoolPrefab");
			_prefab.SetActive(false);
			_prefab.AddComponent<Marker>();
			_created.Add(_prefab);
		}

		[TearDown]
		public void TearDown()
		{
			foreach (var root in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (root != null && root.name == "[ObjectPools]") Object.DestroyImmediate(root);
			}

			foreach (var o in _created)
			{
				if (o != null) Object.DestroyImmediate(o);
			}

			_created.Clear();
		}

		private PoolableObjectDefinition MakeDefinition(int initial = 2, int max = 4, bool prewarm = false)
		{
			var def = ScriptableObject.CreateInstance<PoolableObjectDefinition>();
			def.name = "TestPool";

			var so = new SerializedObject(def);
			so.FindProperty("_prefab").objectReferenceValue = _prefab;
			so.FindProperty("_initialPoolSize").intValue    = initial;
			so.FindProperty("_maxPoolSize").intValue        = max;
			so.FindProperty("_prewarmOnRegister").boolValue = prewarm;
			so.ApplyModifiedPropertiesWithoutUndo();

			_created.Add(def);
			return def;
		}

		// ---------------------------------------------------------------- leases

		[Test]
		public void Lease_ReturnsValidHandle_ThatResolvesToActiveInstance()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition();

			Handle<PooledInstance> lease = svc.Lease(def, new Vector3(1, 2, 3));

			Assert.IsTrue(lease.IsSet);
			Assert.IsTrue(svc.IsLeased(lease));
			Assert.IsTrue(svc.TryGet(lease, out GameObject go));
			Assert.IsTrue(go.activeSelf);
			Assert.AreEqual(new Vector3(1, 2, 3), go.transform.position);
			Assert.IsTrue(svc.TryGet(lease, out Marker marker));
			Assert.AreEqual(1, marker.Gets);
			Assert.IsFalse(marker.IsInPool);
			Assert.AreEqual(1, svc.ActiveCount(def));
			Assert.AreEqual(1, svc.LeasedCount);
		}

		[Test]
		public void Release_InvalidatesLease_AndReturnsFalseOnSecondRelease()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition();

			Handle<PooledInstance> lease = svc.Lease(def);
			svc.TryGet(lease, out Marker marker);

			Assert.IsTrue(svc.Release(lease));
			Assert.IsFalse(svc.IsLeased(lease));
			Assert.IsFalse(svc.TryGet(lease, out GameObject _));
			Assert.IsTrue(marker.IsInPool);
			Assert.AreEqual(1, marker.Returns);
			Assert.AreEqual(0, svc.ActiveCount(def));
			Assert.AreEqual(1, svc.InactiveCount(def));

			Assert.IsFalse(svc.Release(lease), "double release through a lease is a silent no-op");
			Assert.AreEqual(1, marker.Returns, "OnReturnToPool must not fire twice");
		}

		[Test]
		public void StaleLease_DoesNotResolve_ToTheNextOccupant()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition(initial: 1, max: 1);

			Handle<PooledInstance> first = svc.Lease(def);
			svc.TryGet(first, out GameObject firstGo);
			svc.Release(first);

			Handle<PooledInstance> second = svc.Lease(def);
			svc.TryGet(second, out GameObject secondGo);

			Assert.AreSame(firstGo, secondGo, "with max=1 the same GameObject must be recycled");
			Assert.AreNotEqual(first, second, "but the lease must differ");
			Assert.IsFalse(svc.TryGet(first, out GameObject _), "the stale lease must not see the recycled object");
			Assert.IsFalse(svc.Release(first), "and cannot release it out from under the new holder");
			Assert.IsTrue(svc.IsLeased(second));
		}

		[Test]
		public void Lease_AtCapAndEmpty_ReturnsInvalid()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition(initial: 0, max: 2);

			Handle<PooledInstance> a = svc.Lease(def);
			Handle<PooledInstance> b = svc.Lease(def);
			Assert.IsTrue(a.IsSet && b.IsSet);

			Handle<PooledInstance> c;
			using (ExpectedLog.Warning("MaxPoolSize"))
			{
				c = svc.Lease(def);
			}

			Assert.IsFalse(c.IsSet);
			Assert.AreEqual(2, svc.ActiveCount(def));
		}

		// ---------------------------------------------------------------- reference API parity

		[Test]
		public void Get_And_Release_ByReference_StillWork()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition();

			Marker m = svc.Get<Marker>(def);
			Assert.IsNotNull(m);
			Assert.AreEqual(1, svc.ActiveCount(def));

			svc.Release(m.gameObject);
			Assert.AreEqual(0, svc.ActiveCount(def));
			Assert.AreEqual(1, svc.InactiveCount(def));
			Assert.IsTrue(m.IsInPool);
		}

		[Test]
		public void ReturnToPool_OnPoolableObject_RoutesThroughService()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition();

			Marker m = svc.Get<Marker>(def);
			m.ReturnToPool();

			Assert.AreEqual(0, svc.ActiveCount(def));
			Assert.AreEqual(1, svc.InactiveCount(def));
			Assert.AreEqual(0, svc.LeasedCount);
		}

		[Test]
		public void DoubleRelease_ByReference_WarnsOnce_AndDoesNotCorruptCounts()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition();

			GameObject go = svc.Get(def);
			svc.Release(go);

			using (ExpectedLog.Warning("released twice"))
			{
				svc.Release(go);
			}

			Assert.AreEqual(0, svc.ActiveCount(def));
			Assert.AreEqual(1, svc.InactiveCount(def));
		}

		[Test]
		public void Prewarm_CreatesInitialPoolSize_WithoutLeasing()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition(initial: 3, max: 8);

			svc.Prewarm(def);

			Assert.AreEqual(3, svc.InactiveCount(def));
			Assert.AreEqual(0, svc.ActiveCount(def));
			Assert.AreEqual(0, svc.LeasedCount);
		}

		[Test]
		public void Clear_DestroysInstances_AndInvalidatesLeases()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition();

			Handle<PooledInstance> lease = svc.Lease(def);
			svc.Get(def);

			svc.Clear(def);

			Assert.IsFalse(svc.IsLeased(lease));
			Assert.AreEqual(0, svc.LeasedCount);
			Assert.AreEqual(0, svc.ActiveCount(def));
			Assert.AreEqual(0, svc.InactiveCount(def));
		}

		// ---------------------------------------------------------------- allocation

		[Test]
		public void LeaseRelease_SteadyState_DoesNotAllocate()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition(initial: 8, max: 8);
			svc.Prewarm(def);

			var leases = new Handle<PooledInstance>[8];
			for (int i = 0; i < leases.Length; i++) leases[i] = svc.Lease(def);
			for (int i = 0; i < leases.Length; i++) svc.Release(leases[i]);

			int allocations = GcAllocations.Count(() =>
			{
				for (int round = 0; round < 50; round++)
				{
					for (int i = 0; i < leases.Length; i++) leases[i] = svc.Lease(def);
					for (int i = 0; i < leases.Length; i++) svc.TryGet(leases[i], out GameObject _);
					for (int i = 0; i < leases.Length; i++) svc.Release(leases[i]);
				}
			});

			Assert.AreEqual(0, allocations, "Lease/TryGet/Release allocated managed memory at steady state");
		}
	}
}
