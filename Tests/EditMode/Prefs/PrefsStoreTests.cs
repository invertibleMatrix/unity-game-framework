using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AK.Core;
using AK.Kernel.Persistence;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests
{
	public class PrefsStoreTests
	{
		private const string SetAside = "K.corrupt.20261004T120000000Z";

		private static readonly DateTime Noon = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

		private InMemoryKeyValueStore _backend;
		private PrefsStore            _store;

		[Serializable]
		public class Sample
		{
			public int       Number = 7;
			public string    Text   = "seven";
			public List<int> Values = new();
		}

		[SetUp]
		public void SetUp()
		{
			_backend = new InMemoryKeyValueStore();
			_store   = new PrefsStore(_backend, () => Noon);
		}

		[Test]
		public void Values_RoundTrip_ForEveryKindOfType()
		{
			_store.Set("int", 42);
			_store.Set("long", long.MaxValue);
			_store.Set("float", 1.5f);
			_store.Set("nan", float.NaN);
			_store.Set("bool", true);
			_store.Set("string", "héllo \"quoted\"\n");
			_store.Set("enum", DayOfWeek.Friday);
			_store.Set("list", new List<int> { 1, 2, 3 });
			_store.Set("class", new Sample { Number = 3, Text = "three", Values = { 9 } });

			Assert.AreEqual(42, _store.Get<int>("int"));
			Assert.AreEqual(long.MaxValue, _store.Get<long>("long"));
			Assert.AreEqual(1.5f, _store.Get<float>("float"));
			Assert.IsNaN(_store.Get<float>("nan"));
			Assert.IsTrue(_store.Get<bool>("bool"));
			Assert.AreEqual("héllo \"quoted\"\n", _store.Get<string>("string"));
			Assert.AreEqual(DayOfWeek.Friday, _store.Get<DayOfWeek>("enum"));
			CollectionAssert.AreEqual(new[] { 1, 2, 3 }, _store.Get<List<int>>("list"));

			Sample sample = _store.Get<Sample>("class");
			Assert.AreEqual(3, sample.Number);
			Assert.AreEqual("three", sample.Text);
			CollectionAssert.AreEqual(new[] { 9 }, sample.Values);
		}

		[Test]
		public void AMissingKey_ReadsAsTheDefault()
		{
			Assert.IsFalse(_store.TryGet("missing", out int value));
			Assert.AreEqual(0, value);
			Assert.AreEqual(5, _store.Get("missing", 5));
			Assert.IsFalse(_store.Has("missing"));
		}

		[Test]
		public void AnEmptyValue_ReadsAsNothingStored()
		{
			// PlayerPrefs answers a missing string with an empty one, so there is nothing to keep.
			_backend.Set("K", "");

			Assert.IsFalse(_store.TryGet("K", out int _));
			Assert.IsFalse(_backend.Contains(SetAside));
		}

		// The ways a stored value goes bad. Each is set aside under a dated key, an error names
		// both keys, and the read starts fresh, so the next save can't destroy it.
		[TestCase("not json", "it isn't well-formed JSON", Description = "corrupt")]
		[TestCase("{\"Data\":{\"Number\":3,\"Text\":\"thr", "it isn't well-formed JSON", Description = "truncated")]
		[TestCase("[1,2]", "it isn't a JSON object")]
		[TestCase("{}", "it has no \"Data\" member", Description = "wrong schema: no envelope")]
		[TestCase("{\"Value\":{\"Number\":3}}", "it has no \"Data\" member", Description = "wrong schema: another envelope")]
		[TestCase("{\"Data\":5}", "its \"Data\" member is a number, but Sample is saved as an object", Description = "wrong schema: another type's value")]
		[TestCase("{\"Data\":null}", "its \"Data\" member is null, but Sample is saved as an object", Description = "wrong schema: null")]
		public void AnUnreadableValue_IsSetAside_NotOverwritten(string stored, string reason)
		{
			_backend.Set("K", stored);

			Sample value;
			using (ExpectedLog.Error(Regex.Escape($"'K' can't be used: {reason}. Its value is kept under '{SetAside}', and 'K' starts fresh.")))
			{
				Assert.IsFalse(_store.TryGet("K", out value));
			}

			Assert.IsNull(value);
			Assert.IsFalse(_backend.Contains("K"), "the key starts fresh");
			Assert.IsTrue(_backend.TryGet(SetAside, out string kept));
			Assert.AreEqual(stored, kept, "kept exactly as it was");

			_store.Set("K", new Sample());

			Assert.IsTrue(_backend.TryGet(SetAside, out kept));
			Assert.AreEqual(stored, kept, "the next save leaves it alone");
		}

		[TestCase("{\"Data\":\"seven\"}", "is a string, but Int32 is saved as a number")]
		[TestCase("{\"Data\":true}", "is a boolean, but Int32 is saved as a number")]
		[TestCase("{\"Data\":{}}", "is an object, but Int32 is saved as a number")]
		public void APrimitiveValueOfTheWrongKind_IsSetAside(string stored, string reason)
		{
			_backend.Set("K", stored);

			using (ExpectedLog.Error(Regex.Escape(reason)))
			{
				Assert.AreEqual(-1, _store.Get("K", -1));
			}

			Assert.IsTrue(_backend.Contains(SetAside));
		}

		[Test]
		public void Quarantine_SetsAValueAside_ForAReasonOfTheCallers()
		{
			_store.Set("K", 1);

			using (ExpectedLog.Error(Regex.Escape("'K' can't be used: it came from a newer build.")))
			{
				Assert.AreEqual(SetAside, _store.Quarantine("K", "it came from a newer build"));
			}

			Assert.IsFalse(_store.Has("K"));
			Assert.IsNull(_store.Quarantine("K", "nothing there"), "nothing to set aside, nothing logged");
		}

		[Test]
		public void DeleteAll_DeletesOnlyTheStoresKeys()
		{
			_backend.Set("auth_token", "someone else's");
			_store.Set("a", 1);
			_store.Set("b", "two");

			_store.DeleteAll();

			Assert.IsFalse(_store.Has("a"));
			Assert.IsFalse(_store.Has("b"));
			Assert.IsTrue(_backend.Contains("auth_token"));
			Assert.AreEqual(1, _store.Generation);
		}
	}
}
