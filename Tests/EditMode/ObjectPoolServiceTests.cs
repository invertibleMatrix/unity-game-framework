using System;
using System.Collections.Generic;
using AK.Core;
using AK.Kernel.Collections;
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

			Handle<PooledInstance> lease = svc.Lease(def, new Vector3(1, 2, 3), Quaternion.identity);

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

		// ---------------------------------------------------------------- placement

		[Test]
		public void Lease_UnderAParent_GivesAReusedInstanceItsPrefabsLocalPose()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition(initial: 0, max: 1);
			_prefab.transform.SetLocalPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(0, 90, 0));
			_prefab.transform.localScale = new Vector3(2, 2, 2);

			var parent = new GameObject("Parent").transform;
			_created.Add(parent.gameObject);
			parent.SetPositionAndRotation(new Vector3(10, 0, 0), Quaternion.Euler(0, 0, 45));

			GameObject first = svc.Get(def, parent);
			first.transform.SetPositionAndRotation(new Vector3(-5, -5, -5), Quaternion.Euler(30, 0, 0));
			first.transform.localScale = new Vector3(7, 7, 7);
			svc.Release(first);

			GameObject again = svc.Get(def, parent);

			Assert.AreSame(first, again);
			Assert.AreSame(parent, again.transform.parent);
			Assert.AreEqual(new Vector3(1, 2, 3), again.transform.localPosition, "posed as Instantiate(prefab, parent) would pose it");
			Assert.Less(Quaternion.Angle(Quaternion.Euler(0, 90, 0), again.transform.localRotation), 0.01f);
			Assert.AreEqual(new Vector3(2, 2, 2), again.transform.localScale);
		}

		[Test]
		public void Lease_AtAPositionAndRotation_KeepsThePrefabsScale()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition();
			_prefab.transform.localScale = new Vector3(3, 3, 3);

			var parent = new GameObject("Parent").transform;
			_created.Add(parent.gameObject);
			parent.position = new Vector3(10, 0, 0);

			GameObject go = svc.Get(def, new Vector3(1, 2, 3), Quaternion.Euler(0, 45, 0), parent);

			Assert.AreSame(parent, go.transform.parent);
			Assert.Less(Vector3.Distance(new Vector3(1, 2, 3), go.transform.position), 1e-4f, "a world position, as Instantiate takes it");
			Assert.Less(Quaternion.Angle(Quaternion.Euler(0, 45, 0), go.transform.rotation), 0.01f);
			Assert.AreEqual(new Vector3(3, 3, 3), go.transform.localScale);
		}

		// ---------------------------------------------------------------- instances destroyed outside the pool

		[Test]
		public void AnInstanceDestroyedWhileLeased_EndsItsLease_AndFreesItsPlace()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition(initial: 0, max: 1);

			Handle<PooledInstance> lease = svc.Lease(def);
			svc.TryGet(lease, out Marker marker);
			Object.DestroyImmediate(marker.gameObject);

			Assert.IsFalse(svc.IsLeased(lease));
			Assert.IsFalse(svc.TryGet(lease, out GameObject _));
			Assert.IsFalse(svc.Release(lease), "its lease ended when it was destroyed");
			Assert.AreEqual(0, marker.Returns, "a destroyed instance gets no callbacks");
			Assert.AreEqual(0, svc.ActiveCount(def));
			Assert.AreEqual(0, svc.LeasedCount);
			Assert.IsTrue(svc.Lease(def).IsSet, "its place under the cap is free again");
		}

		[Test]
		public void AFullPool_WritesOffInstancesDestroyedWhileLeased_BeforeRefusing()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition(initial: 0, max: 1);
			Object.DestroyImmediate(svc.Get(def));

			Handle<PooledInstance> lease;
			using (ExpectedLog.Warning("MaxPoolSize", expected: 0))
			{
				lease = svc.Lease(def);
			}

			Assert.IsTrue(lease.IsSet);
			Assert.AreEqual(1, svc.ActiveCount(def));
		}

		[Test]
		public void AnInstanceDestroyedWhileResting_IsSkipped_AndItsPlaceFreed()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition(initial: 0, max: 2);
			GameObject a = svc.Get(def);
			GameObject b = svc.Get(def);
			svc.Release(a);
			svc.Release(b);
			Object.DestroyImmediate(b);

			GameObject first = svc.Get(def);
			GameObject second;
			using (ExpectedLog.Warning("MaxPoolSize", expected: 0))
			{
				second = svc.Get(def);
			}

			Assert.AreSame(a, first, "the destroyed instance, next in line, was skipped");
			Assert.IsNotNull(second, "a new instance took its place");
			Assert.AreEqual(2, svc.ActiveCount(def));
			Assert.AreEqual(0, svc.InactiveCount(def));
		}

		[Test]
		public void Counts_LeaveOutDestroyedInstances_AndPrewarmMakesUpForThem()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition(initial: 3, max: 3);
			svc.Prewarm(def);
			GameObject resting = svc.Get(def);
			svc.Release(resting);
			Object.DestroyImmediate(resting);

			Assert.AreEqual(2, svc.InactiveCount(def));

			svc.Prewarm(def);
			Assert.AreEqual(3, svc.InactiveCount(def));
		}

		[Test]
		public void Release_OfADestroyedInstance_EndsItsLease_Quietly()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition();
			Handle<PooledInstance> lease = svc.Lease(def);
			svc.TryGet(lease, out GameObject go);
			Object.DestroyImmediate(go);

			using (ExpectedLog.Warning("ObjectPoolService", expected: 0))
			{
				svc.Release(go);
			}

			Assert.IsFalse(svc.IsLeased(lease));
			Assert.AreEqual(0, svc.LeasedCount);
		}

		// ---------------------------------------------------------------- identity

		private ObjectPoolService MakeServiceWithRegistry(PoolableObjectDefinition def)
		{
			def.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);

			var registry = ScriptableObject.CreateInstance<ObjectPoolRegistry>();
			_created.Add(registry);
			Assert.IsTrue(registry.Editor_TryTrack(def));

			return new ObjectPoolService(registry);
		}

		[Test]
		public void LeaseById_LeasesFromTheRegisteredDefinition()
		{
			var def = MakeDefinition();
			ObjectPoolService svc = MakeServiceWithRegistry(def);

			Handle<PooledInstance> lease = svc.Lease(def.IdAs<PoolableObjectDefinition>());

			Assert.IsTrue(lease.IsSet);
			Assert.AreEqual(1, svc.ActiveCount(def));
		}

		[Test]
		public void LeaseById_WithNoId_IsRefused_InsteadOfPickingAPool()
		{
			var def = MakeDefinition();
			ObjectPoolService svc = MakeServiceWithRegistry(def);

			Handle<PooledInstance> lease;
			using (ExpectedLog.Error("No pool definition id"))
			{
				lease = svc.Lease(default(Uid<PoolableObjectDefinition>));
			}

			Assert.IsFalse(lease.IsSet);
			Assert.AreEqual(0, svc.LeasedCount);
		}

		// ---------------------------------------------------------------- objects the service didn't make

		[Test]
		public void Release_OfAnObjectTheServiceDidNotMake_LeavesItAlone()
		{
			var svc = new ObjectPoolService();
			var stranger = new GameObject("Stranger");
			_created.Add(stranger);

			using (ExpectedLog.Warning("not made by this service"))
			{
				svc.Release(stranger);
			}

			Assert.IsTrue(stranger != null, "not destroyed");
			Assert.IsTrue(stranger.activeSelf);
		}

		[Test]
		public void ReturnToPool_OnAnInstanceNoPoolMade_LeavesItAlone()
		{
			var stranger = new GameObject("Stranger");
			_created.Add(stranger);
			var marker = stranger.AddComponent<Marker>();

			using (ExpectedLog.Warning("not made by an ObjectPoolService"))
			{
				marker.ReturnToPool();
			}

			Assert.IsTrue(stranger != null, "not destroyed");
			Assert.IsTrue(stranger.activeSelf);
		}

		[Test]
		public void GetT_ForAComponentTheInstanceLacks_GivesTheInstanceBack()
		{
			var svc = new ObjectPoolService();
			var def = MakeDefinition();

			BoxCollider missing;
			using (ExpectedLog.Error("has no BoxCollider"))
			{
				missing = svc.Get<BoxCollider>(def);
			}

			Assert.IsNull(missing);
			Assert.AreEqual(0, svc.ActiveCount(def));
			Assert.AreEqual(1, svc.InactiveCount(def));
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
