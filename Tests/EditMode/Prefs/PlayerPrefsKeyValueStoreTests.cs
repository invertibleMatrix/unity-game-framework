using System;
using AK.Core;
using NUnit.Framework;
using UnityEngine;

namespace AK.Tests
{
	// Runs against the real PlayerPrefs, under a key of its own that each test deletes.
	public class PlayerPrefsKeyValueStoreTests
	{
		private const string Key = "AK.Tests.PlayerPrefsKeyValueStore";

		private PlayerPrefsKeyValueStore _store;

		[SetUp]
		public void SetUp()
		{
			PlayerPrefs.DeleteKey(Key);
			_store = new PlayerPrefsKeyValueStore();
		}

		[TearDown]
		public void TearDown()
		{
			_store.Dispose();
			PlayerPrefs.DeleteKey(Key);
			PlayerPrefs.Save();
		}

		[Test]
		public void OutsidePlayMode_EachWriteIsFlushedAtOnce()
		{
			_store.Set(Key, "v");

			Assert.IsFalse(_store.HasUnflushedWrites, "there are no frames to wait for");

			Assert.IsTrue(_store.Delete(Key));

			Assert.IsFalse(_store.HasUnflushedWrites);
		}

		[Test]
		public void Values_RoundTrip_AndAnEmptyStringIsStillAValue()
		{
			Assert.IsFalse(_store.TryGet(Key, out string value));
			Assert.IsNull(value);

			_store.Set(Key, "héllo\n\"quoted\"");

			Assert.IsTrue(_store.TryGet(Key, out value));
			Assert.AreEqual("héllo\n\"quoted\"", value);

			_store.Set(Key, string.Empty);

			Assert.IsTrue(_store.Contains(Key));
			Assert.IsTrue(_store.TryGet(Key, out value));
			Assert.AreEqual(string.Empty, value);
		}

		[Test]
		public void Delete_IsFalse_WhenThereIsNothingToDelete()
		{
			Assert.IsFalse(_store.Delete(Key));

			_store.Set(Key, "v");

			Assert.IsTrue(_store.Delete(Key));
			Assert.IsFalse(_store.Contains(Key));
		}

		[Test]
		public void AfterDispose_UseThrows_AndDisposeAgainDoesNothing()
		{
			_store.Dispose();

			Assert.Throws<ObjectDisposedException>(() => _store.Set(Key, "v"));
			Assert.Throws<ObjectDisposedException>(() => _store.TryGet(Key, out _));
			Assert.DoesNotThrow(() => _store.Dispose());
			Assert.IsFalse(PlayerPrefs.HasKey(Key));
		}
	}
}
