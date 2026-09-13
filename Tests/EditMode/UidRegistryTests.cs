using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Facts;
using NUnit.Framework;
using UnityEngine;
using AK.Tests.Support;

namespace AK.Tests
{
	public class UidRegistryTests
	{
		private readonly List<Object> _created = new();

		private FactType Make(string name, Uid? id = null)
		{
			var asset = ScriptableObject.CreateInstance<FactType>();
			asset.name = name;
			asset.Editor_AssignIdentity(id ?? Uid.NewRandom(), UidProvenance.Minted, string.Empty);
			_created.Add(asset);
			return asset;
		}

		private FactType MakeWithoutIdentity(string name)
		{
			var asset = ScriptableObject.CreateInstance<FactType>();
			asset.name = name;
			_created.Add(asset);
			return asset;
		}

		[TearDown]
		public void TearDown()
		{
			foreach (var o in _created) Object.DestroyImmediate(o);
			_created.Clear();
		}

		[Test]
		public void Resolve_ByIdentity_ReturnsTrackedAsset()
		{
			var a = Make("A");
			var b = Make("B");
			var registry = new UidRegistry<FactType>();
			registry.Add(a);
			registry.Add(b);

			Assert.IsTrue(registry.TryResolve(a.Id, out FactType ra));
			Assert.AreSame(a, ra);
			Assert.IsTrue(registry.TryResolve(b.IdAs<FactType>(), out FactType rb));
			Assert.AreSame(b, rb);
			Assert.AreSame(a, registry.Resolve(a.Id));
			Assert.AreEqual(2, registry.Count);
		}

		[Test]
		public void Resolve_Unknown_ReturnsFalse_WithoutLogging()
		{
			var registry = new UidRegistry<FactType>();
			registry.Add(Make("A"));

			Assert.IsFalse(registry.TryResolve(Uid.NewRandom(), out FactType missing));
			Assert.IsNull(missing);
			Assert.IsNull(registry.Resolve(Uid.NewRandom()));
			Assert.IsFalse(registry.Contains(Uid.NewRandom()));
		}

		[Test]
		public void Resolve_None_ReturnsFalse()
		{
			var registry = new UidRegistry<FactType>();
			registry.Add(Make("A"));

			Assert.IsFalse(registry.TryResolve(Uid.None, out _));
			Assert.IsFalse(registry.Contains(Uid.None));
			Assert.IsFalse(registry.GetHandle(Uid<FactType>.None).IsValid);
		}

		[Test]
		public void Handle_ResolvesInOneIndex_AndSurvivesRebuild()
		{
			var a = Make("A");
			var b = Make("B");
			var registry = new UidRegistry<FactType>();
			registry.Add(a);
			registry.Add(b);

			UidHandle<FactType> handle = registry.GetHandle(b.IdAs<FactType>());
			Assert.IsTrue(handle.IsValid);
			Assert.IsTrue(registry.TryGet(ref handle, out FactType first));
			Assert.AreSame(b, first);

			int versionBefore = registry.Version;
			registry.Remove(a);
			Assert.AreNotEqual(versionBefore, registry.Version);

			Assert.IsTrue(registry.TryGet(ref handle, out FactType afterRebuild));
			Assert.AreSame(b, afterRebuild);
			Assert.AreEqual(registry.Version, handle.Version);
			Assert.AreEqual(0, handle.Slot);
		}

		[Test]
		public void Handle_ToRemovedAsset_BecomesInvalid()
		{
			var a = Make("A");
			var registry = new UidRegistry<FactType>();
			registry.Add(a);

			UidHandle<FactType> handle = registry.GetHandle(a.IdAs<FactType>());
			registry.Remove(a);

			Assert.IsFalse(registry.TryGet(ref handle, out FactType resolved));
			Assert.IsNull(resolved);
			Assert.IsFalse(handle.IsValid);
		}

		[Test]
		public void Duplicate_Identity_SkipsSecond_WithDiagnostic()
		{
			Uid shared = Uid.NewRandom();
			var first = Make("First", shared);
			var second = Make("Second", shared);

			var registry = new UidRegistry<FactType>();
			registry.Add(first);
			using (ExpectedLog.Error("shares identity"))
			{
				registry.Add(second);
			}

			Assert.IsTrue(registry.TryResolve(shared, out FactType resolved));
			Assert.AreSame(first, resolved);
			Assert.AreEqual(2, registry.Objects.Count, "the list keeps both; only the lookup skips the duplicate");
		}

		[Test]
		public void MissingIdentity_IsSkipped_WithDiagnostic()
		{
			var registry = new UidRegistry<FactType>();
			registry.Add(Make("A"));
			using (ExpectedLog.Error("no identity"))
			{
				registry.Add(MakeWithoutIdentity("Empty"));
			}

			Assert.AreEqual(2, registry.Count);
			Assert.IsFalse(registry.Contains(Uid.None));
		}

		[Test]
		public void RemoveNullEntries_DropsDestroyedAssets()
		{
			var a = Make("A");
			var b = Make("B");
			var registry = new UidRegistry<FactType>();
			registry.Add(a);
			registry.Add(b);

			Object.DestroyImmediate(b);
			_created.Remove(b);

			Assert.AreEqual(1, registry.RemoveNullEntries());
			Assert.AreEqual(1, registry.Count);
			Assert.IsTrue(registry.TryResolve(a.Id, out _));
		}

		[Test]
		public void ReplaceAll_Deduplicates_AndDropsNulls()
		{
			var a = Make("A");
			var registry = new UidRegistry<FactType>();
			registry.ReplaceAll(new FactType[] { a, a, null });

			Assert.AreEqual(1, registry.Count);
			Assert.IsTrue(registry.TryResolve(a.Id, out _));
		}

		[Test]
		public void Redirect_FollowedOnMiss()
		{
			var live = Make("Live");
			Uid retired = Uid.NewRandom();

			var table = ScriptableObject.CreateInstance<UidRedirectTable>();
			_created.Add(table);
			table.Editor_Add(retired, live.Id, "test");

			var registry = new UidRegistry<FactType>();
			registry.Add(live);
			registry.SetRedirects(table);

			Assert.IsTrue(registry.TryResolve(retired, out FactType resolved));
			Assert.AreSame(live, resolved);

			UidHandle<FactType> handle = registry.GetHandle(new Uid<FactType>(retired));
			Assert.IsTrue(handle.IsValid);
			Assert.AreEqual(live.Id, handle.Id.Value, "handle carries the live identity, not the retired one");
		}

		[Test]
		public void RegistryAsset_ExposesUntypedResolve()
		{
			var a = Make("A");
			var asset = ScriptableObject.CreateInstance<FactTypeRegistry>();
			_created.Add(asset);
			asset.Registry.Add(a);

			Assert.AreEqual(typeof(FactType), asset.ElementType);
			Assert.AreEqual(1, asset.ObjectCount);
			Assert.IsTrue(asset.TryResolveUntyped(a.Id, out UID untyped));
			Assert.AreSame(a, untyped);
			Assert.IsTrue(asset.TryResolve(a.IdAs<FactType>(), out FactType typed));
			Assert.AreSame(a, typed);
		}
	}
}
