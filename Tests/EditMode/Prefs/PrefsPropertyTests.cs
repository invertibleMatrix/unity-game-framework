using System.Collections.Generic;
using AK.Core;
using AK.Kernel.Persistence;
using NUnit.Framework;

namespace AK.Tests
{
	public class PrefsPropertyTests
	{
		private InMemoryKeyValueStore _backend;
		private PrefsStore            _store;

		[SetUp]
		public void SetUp()
		{
			_backend = new InMemoryKeyValueStore();
			_store   = new PrefsStore(_backend);
		}

		[Test]
		public void Read_IsTheDefault_UntilAValueIsSaved()
		{
			var volume = new PrefsProperty<float>("volume", 0.8f, _store);

			Assert.AreEqual(0.8f, volume.Read());

			volume.Save(0.25f);

			Assert.AreEqual(0.25f, volume.Read());
			Assert.AreEqual(0.25f, new PrefsProperty<float>("volume", 0.8f, _store).Read());
		}

		[Test]
		public void SaveWithoutAValue_SavesTheCurrentValue_NotDefaultOfT()
		{
			new PrefsProperty<float>("volume", 1f, _store).Save(0.25f);

			// The old Save(T toSave = default) wrote 0 here for a value type.
			new PrefsProperty<float>("volume", 1f, _store).Save();

			Assert.AreEqual(0.25f, new PrefsProperty<float>("volume", 1f, _store).Read());
		}

		[Test]
		public void SaveWithoutAValue_SavesAValueChangedInPlace()
		{
			var unlocked = new PrefsProperty<List<string>>("unlocked", new List<string>(), _store);
			unlocked.Read().Add("hat");

			unlocked.Save();

			CollectionAssert.AreEqual(new[] { "hat" }, new PrefsProperty<List<string>>("unlocked", null, _store).Read());
		}

		[Test]
		public void Read_IsCached()
		{
			var volume = new PrefsProperty<float>("volume", 1f, _store);
			volume.Save(0.5f);

			_store.Set("volume", 0.75f);

			Assert.AreEqual(0.5f, volume.Read(), "read once, then served from the cache");
		}

		[Test]
		public void Reset_DeletesTheValue_AndRestoresTheDefault()
		{
			var name = new PrefsProperty<string>("name", "guest", _store);
			name.Save("Ada");

			name.Reset();

			Assert.AreEqual("guest", name.Read());
			Assert.IsFalse(_store.Has("name"));
		}

		[Test]
		public void AfterTheStoreDeletesEverything_ItReadsAsTheDefault_AndSaveCantBringTheValueBack()
		{
			var name = new PrefsProperty<string>("name", "guest", _store);
			name.Save("Ada");

			_store.DeleteAll();

			Assert.AreEqual("guest", name.Read());

			name.Save();

			Assert.AreEqual("guest", new PrefsProperty<string>("name", "guest", _store).Read());
		}

		[Test]
		public void ConvertsAndPrintsAsItsValue()
		{
			var count = new PrefsProperty<int>("count", 3, _store);

			int value = count;

			Assert.AreEqual(3, value);
			Assert.AreEqual("3", count.ToString());
			Assert.AreEqual(string.Empty, new PrefsProperty<string>("none", null, _store).ToString());
		}
	}
}
