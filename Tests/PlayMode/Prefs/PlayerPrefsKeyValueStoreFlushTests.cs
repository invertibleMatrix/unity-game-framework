using System.Collections;
using AK.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AK.Tests
{
	// Runs against the real PlayerPrefs, under a key of its own that the test deletes.
	public class PlayerPrefsKeyValueStoreFlushTests
	{
		private const string Key = "AK.Tests.PlayerPrefsKeyValueStore.PlayMode";

		[UnityTest]
		public IEnumerator InPlayMode_AFramesWrites_AreFlushedOnce_AtTheEndOfTheFrame()
		{
			var store = new PlayerPrefsKeyValueStore();
			try
			{
				store.Set(Key, "1");
				store.Set(Key, "2");

				Assert.IsTrue(store.HasUnflushedWrites, "nothing is written to disk before the frame ends");

				yield return null;

				Assert.IsFalse(store.HasUnflushedWrites, "flushed at the end of the frame");

				store.Delete(Key);

				Assert.IsTrue(store.HasUnflushedWrites, "a later frame's writes schedule a flush of their own");

				yield return null;

				Assert.IsFalse(store.HasUnflushedWrites);
			}
			finally
			{
				store.Dispose();
				PlayerPrefs.DeleteKey(Key);
				PlayerPrefs.Save();
			}
		}

		[UnityTest]
		public IEnumerator Flush_WritesAtOnce_AndTheScheduledFlushThenHasNothingToDo()
		{
			var store = new PlayerPrefsKeyValueStore();
			try
			{
				store.Set(Key, "1");
				store.Flush();

				Assert.IsFalse(store.HasUnflushedWrites);

				yield return null;

				Assert.IsFalse(store.HasUnflushedWrites);
				Assert.IsTrue(store.TryGet(Key, out string value));
				Assert.AreEqual("1", value);
			}
			finally
			{
				store.Dispose();
				PlayerPrefs.DeleteKey(Key);
				PlayerPrefs.Save();
			}
		}
	}
}
