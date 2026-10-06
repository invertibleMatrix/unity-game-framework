using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AK.Kernel.Persistence;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class RecordStoreTests
	{
		private static readonly DateTime Noon = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

		private InMemoryKeyValueStore _backend;
		private RecordStore           _store;

		[SetUp]
		public void SetUp()
		{
			_backend = new InMemoryKeyValueStore();
			_store   = new RecordStore(_backend);
		}

		private List<string> Owned(RecordStore store = null)
		{
			var keys = new List<string>();
			(store ?? _store).GetOwnedKeys(keys);
			return keys;
		}

		// ---------------------------------------------------------------- ownership

		[Test]
		public void AWrittenKey_IsOwned_AndListedInTheIndex()
		{
			_store.Write("b", "2");
			_store.Write("a", "1");

			CollectionAssert.AreEqual(new[] { "a", "b" }, Owned());
			Assert.IsTrue(_backend.TryGet(StorageKeys.Index, out string index));
			Assert.AreEqual("a\nb", index, "one key per line, in ordinal order");
		}

		[Test]
		public void TheIndex_IsReadBack_ByANewStoreOverTheSameBackend()
		{
			_store.Write("a", "1");
			_store.Write("b", "2");

			CollectionAssert.AreEqual(new[] { "a", "b" }, Owned(new RecordStore(_backend)));
		}

		[Test]
		public void AKeyWrittenBeforeTheIndexExisted_IsOwnedOnceRead()
		{
			_backend.Set("legacy", "x");
			var store = new RecordStore(_backend);
			Assert.AreEqual(0, store.OwnedCount);

			Assert.IsTrue(store.TryRead("legacy", out string value));

			Assert.AreEqual("x", value);
			CollectionAssert.AreEqual(new[] { "legacy" }, Owned(store));
		}

		[Test]
		public void ReadingAMissingKey_OwnsNothing()
		{
			Assert.IsFalse(_store.TryRead("missing", out string value));

			Assert.IsNull(value);
			Assert.AreEqual(0, _store.OwnedCount);
			Assert.IsFalse(_backend.Contains(StorageKeys.Index));
		}

		[Test]
		public void Delete_DisownsTheKey_AndTheLastOneTakesTheIndexWithIt()
		{
			_store.Write("a", "1");

			Assert.IsTrue(_store.Delete("a"));
			Assert.IsFalse(_store.Delete("a"), "nothing left to delete");

			Assert.AreEqual(0, _store.OwnedCount);
			Assert.IsFalse(_backend.Contains("a"));
			Assert.IsFalse(_backend.Contains(StorageKeys.Index));
		}

		[Test]
		public void AnUnreadableIndex_IsReadLeniently()
		{
			_backend.Set(StorageKeys.Index, "a\n\nb\n\u0001bad\n" + StorageKeys.Index + "\n");

			CollectionAssert.AreEqual(new[] { "a", "b" }, Owned(new RecordStore(_backend)));
		}

		[Test]
		public void BadKeysAndValues_Throw()
		{
			Assert.Throws<ArgumentException>(() => _store.Write("", "v"));
			Assert.Throws<ArgumentException>(() => _store.Write(StorageKeys.Index, "v"));
			Assert.Throws<ArgumentException>(() => _store.TryRead("a\nb", out _));
			Assert.Throws<ArgumentNullException>(() => _store.Write("k", null));
			Assert.Throws<ArgumentNullException>(() => _store.Delete(null));
		}

		[Test]
		public void TheConstructor_RejectsANullBackend_AndAQuarantineLimitBelowOne()
		{
			Assert.Throws<ArgumentNullException>(() => new RecordStore(null));
			Assert.Throws<ArgumentOutOfRangeException>(() => new RecordStore(_backend, quarantineLimit: 0));
		}

		// ---------------------------------------------------------------- delete all

		[Test]
		public void DeleteAll_DeletesEveryOwnedKey_AndNothingElse_ThenFlushes()
		{
			_backend.Set("someone-elses", "keep");
			_backend.Set("legacy", "x");
			_store.Write("a", "1");
			_store.TryRead("legacy", out _);
			int flushes = _backend.FlushCount;

			_store.DeleteAll();

			Assert.IsFalse(_backend.Contains("a"));
			Assert.IsFalse(_backend.Contains("legacy"));
			Assert.IsFalse(_backend.Contains(StorageKeys.Index));
			Assert.IsTrue(_backend.Contains("someone-elses"), "a key the store never owned is left alone");
			Assert.AreEqual(0, _store.OwnedCount);
			Assert.AreEqual(flushes + 1, _backend.FlushCount, "the deletion is made durable at once");
		}

		[Test]
		public void DeleteAll_AdvancesTheGeneration()
		{
			int before = _store.Generation;

			_store.DeleteAll();

			Assert.AreEqual(before + 1, _store.Generation);
		}

		[Test]
		public void DeleteAll_NotifiesEachListenerOnce_InRegistrationOrder()
		{
			var calls  = new List<string>();
			var first  = new Listener("first", calls);
			var second = new Listener("second", calls);
			_store.AddResetListener(first);
			_store.AddResetListener(second);
			_store.AddResetListener(first);

			_store.DeleteAll();

			CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
		}

		[Test]
		public void RemoveResetListener_EndsTheRegistration()
		{
			var calls    = new List<string>();
			var listener = new Listener("listener", calls);
			_store.AddResetListener(listener);

			_store.RemoveResetListener(listener);
			_store.DeleteAll();

			CollectionAssert.IsEmpty(calls);
		}

		[Test]
		public void AListenerThatThrows_DoesntStopTheOthers()
		{
			var calls = new List<string>();
			_store.AddResetListener(new Listener("throws", calls, () => throw new InvalidOperationException("boom")));
			var after = new Listener("after", calls);
			_store.AddResetListener(after);
			_store.Write("a", "1");

			var failure = Assert.Throws<AggregateException>(() => _store.DeleteAll());

			Assert.AreEqual(1, failure.InnerExceptions.Count);
			CollectionAssert.AreEqual(new[] { "throws", "after" }, calls);
			Assert.IsFalse(_backend.Contains("a"), "the data is deleted regardless");
		}

		[Test]
		public void AListener_MayWriteAndUnregister_WhileBeingNotified()
		{
			var calls = new List<string>();
			Listener listener = null;
			listener = new Listener("listener", calls, () =>
			{
				_store.Write("fresh", "default");
				_store.RemoveResetListener(listener);
			});
			_store.AddResetListener(listener);

			_store.DeleteAll();
			_store.DeleteAll();

			CollectionAssert.AreEqual(new[] { "listener" }, calls, "unregistered during the first reset");
			Assert.IsFalse(_backend.Contains("fresh"), "its write was owned, so the second reset deleted it");
		}

		[Test]
		public void Registering_DoesntKeepAListenerAlive()
		{
			WeakReference collected = RegisterUnreferencedListener();

			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			Assert.IsFalse(collected.IsAlive);
			Assert.DoesNotThrow(() => _store.DeleteAll());
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private WeakReference RegisterUnreferencedListener()
		{
			var listener = new Listener("dropped", new List<string>());
			_store.AddResetListener(listener);
			return new WeakReference(listener);
		}

		// ---------------------------------------------------------------- quarantine

		[Test]
		public void Quarantine_MovesTheValueAside_AndLeavesTheKeyEmpty()
		{
			_store.Write("K", "garbage");

			string kept = _store.Quarantine("K", Noon);

			Assert.AreEqual("K.corrupt.20261004T120000000Z", kept);
			Assert.IsFalse(_backend.Contains("K"));
			Assert.IsTrue(_backend.TryGet(kept, out string value));
			Assert.AreEqual("garbage", value);
			CollectionAssert.AreEqual(new[] { kept }, Owned(), "the copy is owned, so DeleteAll removes it too");
		}

		[Test]
		public void Quarantine_OfAnEmptyKey_DoesNothing()
		{
			Assert.IsNull(_store.Quarantine("K", Noon));
			Assert.AreEqual(0, _backend.Count);
		}

		[Test]
		public void TwoCopiesSetAsideInOneMillisecond_AreBothKept()
		{
			_store.Write("K", "1");
			_store.Quarantine("K", Noon);
			_store.Write("K", "2");

			string second = _store.Quarantine("K", Noon);

			Assert.AreEqual("K.corrupt.20261004T120000000Z-2", second);
			Assert.IsTrue(_backend.TryGet("K.corrupt.20261004T120000000Z", out string first));
			Assert.AreEqual("1", first);
		}

		[Test]
		public void Quarantine_KeepsOnlyTheNewestCopiesOfTheKey()
		{
			var store = new RecordStore(_backend, quarantineLimit: 2);
			store.Write("Other", "x");
			store.Quarantine("Other", Noon);

			for (int i = 0; i < 4; i++)
			{
				store.Write("K", i.ToString());
				store.Quarantine("K", Noon.AddSeconds(i));
			}

			CollectionAssert.AreEqual(new[]
			{
				"K.corrupt.20261004T120002000Z",
				"K.corrupt.20261004T120003000Z",
				"Other.corrupt.20261004T120000000Z",
			}, Owned(store), "another key's copies are left alone");
			Assert.IsFalse(_backend.Contains("K.corrupt.20261004T120000000Z"));
			Assert.IsFalse(_backend.Contains("K.corrupt.20261004T120001000Z"));
		}

		// ---------------------------------------------------------------- move

		[Test]
		public void Move_CarriesTheValueAndItsOwnership()
		{
			_store.Write("from", "v");

			Assert.IsTrue(_store.Move("from", "to"));

			Assert.IsFalse(_backend.Contains("from"));
			Assert.IsTrue(_backend.TryGet("to", out string value));
			Assert.AreEqual("v", value);
			CollectionAssert.AreEqual(new[] { "to" }, Owned());
		}

		[Test]
		public void Move_FromAnEmptyKey_ChangesNothing()
		{
			Assert.IsFalse(_store.Move("from", "to"));
			Assert.AreEqual(0, _backend.Count);
		}

		[Test]
		public void Move_OntoAKeyThatHoldsAValue_Throws_AndChangesNothing()
		{
			_store.Write("from", "1");
			_store.Write("to", "2");

			Assert.Throws<InvalidOperationException>(() => _store.Move("from", "to"));
			Assert.Throws<ArgumentException>(() => _store.Move("from", "from"));

			Assert.IsTrue(_backend.TryGet("from", out string from));
			Assert.IsTrue(_backend.TryGet("to", out string to));
			Assert.AreEqual("1", from);
			Assert.AreEqual("2", to);
		}

		private sealed class Listener : IStoreResetListener
		{
			private readonly string       _name;
			private readonly List<string> _calls;
			private readonly Action       _onReset;

			public Listener(string name, List<string> calls, Action onReset = null)
			{
				_name    = name;
				_calls   = calls;
				_onReset = onReset;
			}

			public void OnStoreReset()
			{
				_calls.Add(_name);
				_onReset?.Invoke();
			}
		}
	}
}
