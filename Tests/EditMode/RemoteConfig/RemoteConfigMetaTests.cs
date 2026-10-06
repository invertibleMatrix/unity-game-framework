using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.RemoteConfig;
using AK.Kernel.Persistence;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace AK.Tests.RemoteConfig
{
	/// <summary>
	/// The meta as the one place values reach the variables: whole fetches, the cache across
	/// sessions, and lookups by key. Each test's cache lives in memory.
	/// </summary>
	public class RemoteConfigMetaTests
	{
		private RemoteConfigFixture _fixture;
		private LogRecorder _log;
		private InMemoryKeyValueStore _backend;
		private PrefsStore _cache;

		[SetUp]
		public void SetUp()
		{
			_fixture = new RemoteConfigFixture();
			_log     = new LogRecorder();
			_backend = new InMemoryKeyValueStore();
			_cache   = new PrefsStore(_backend);
		}

		[TearDown]
		public void TearDown()
		{
			_fixture.Dispose();
			_log.Dispose();
		}

		// ------------------------------------------------------------------ fetches

		[Test]
		public void AFetch_IsApplied_AndCachedForTheNextSession()
		{
			RemoteInt lives = _fixture.Int("lives", 3);
			RemoteBool hard = _fixture.Bool("hard_mode");
			RemoteConfigMeta meta = _fixture.Meta(lives, hard);

			RemoteConfigUpdate update = meta.ApplyFetchedValues(Fetched(("lives", "5"), ("hard_mode", "true"), ("unused", "x")), _cache);

			Assert.AreEqual(new RemoteConfigUpdate(2, 0, 0), update);
			Assert.AreEqual(5, lives.Value);
			Assert.IsTrue(hard.Value);

			// The next session: fresh variables over the same cache.
			RemoteInt nextLives = _fixture.Int("lives", 3);
			RemoteBool nextHard = _fixture.Bool("hard_mode");
			Assert.AreEqual(2, _fixture.Meta(nextLives, nextHard).LoadCachedValues(_cache));
			Assert.AreEqual(5, nextLives.Value);
			Assert.AreEqual(RemoteValueOrigin.Cached, nextLives.Origin);
			Assert.IsTrue(nextHard.Value);
		}

		[Test]
		public void AVariableTheFetchLeavesOut_LosesItsRemoteValue_AndItsCache()
		{
			RemoteInt lives = _fixture.Int("lives", 3);
			RemoteConfigMeta meta = _fixture.Meta(lives);
			meta.ApplyFetchedValues(Fetched(("lives", "5")), _cache);

			RemoteConfigUpdate update = meta.ApplyFetchedValues(Fetched(), _cache);

			Assert.AreEqual(new RemoteConfigUpdate(0, 0, 1), update);
			Assert.AreEqual(3, lives.Value);
			Assert.IsFalse(_cache.Has(RemoteConfigMeta.CacheKey), "nothing left to cache");
		}

		[Test]
		public void AFetchedValueThatCannotBeRead_IsRejected_AndTheDefaultHolds()
		{
			RemoteInt lives = _fixture.Int("lives", 3);
			RemoteConfigMeta meta = _fixture.Meta(lives);
			meta.ApplyFetchedValues(Fetched(("lives", "5")), _cache);

			RemoteConfigUpdate update;
			using (ExpectedLog.Error("The fetched value of 'lives' can't be read as Int32, so it reads as its default: 'five'"))
			{
				update = meta.ApplyFetchedValues(Fetched(("lives", "five")), _cache);
			}

			Assert.AreEqual(new RemoteConfigUpdate(0, 1, 1), update);
			Assert.AreEqual(3, lives.Value);
		}

		[Test]
		public void AnEmptyFetchedValue_IsNoValue()
		{
			RemoteInt lives = _fixture.Int("lives", 3);
			RemoteConfigMeta meta = _fixture.Meta(lives);

			Assert.AreEqual(new RemoteConfigUpdate(0, 0, 0), meta.ApplyFetchedValues(Fetched(("lives", "")), _cache));
			Assert.IsFalse(lives.HasRemoteValue);
		}

		[Test]
		public void ADisabledVariable_TakesNoValue_AndLosesOneItHad()
		{
			RemoteInt lives = _fixture.Int("lives", 3, enabled: false);
			RemoteConfigMeta meta = _fixture.Meta(lives);
			lives.SetRemoteValue(8);

			RemoteConfigUpdate update = meta.ApplyFetchedValues(Fetched(("lives", "5")), _cache);

			Assert.AreEqual(new RemoteConfigUpdate(0, 0, 1), update);
			Assert.AreEqual(3, lives.Value);
			CollectionAssert.IsEmpty(meta.GetEnabledVariableKeys());
			CollectionAssert.IsEmpty(meta.GetDefaultValueTexts());
		}

		[Test]
		public void TheDefaults_AreText_ForTheEnabledVariables_OncePerKey()
		{
			RemoteConfigMeta meta = _fixture.Meta(
				_fixture.Int("lives", 3),
				_fixture.Float("speed", 1.5f),
				_fixture.Bool("hard_mode", true),
				_fixture.Int("lives", 4),
				_fixture.Int("retired", 1, enabled: false));

			CollectionAssert.AreEquivalent(new Dictionary<string, string>
			{
				{ "lives", "3" },
				{ "speed", "1.5" },
				{ "hard_mode", "true" },
			}, meta.GetDefaultValueTexts());
			CollectionAssert.AreEqual(new[] { "lives", "speed", "hard_mode" }, meta.GetEnabledVariableKeys());
		}

		// ------------------------------------------------------------------ the cache

		[Test]
		public void AVariableThatDoesNotCache_IsLeftOutOfTheCache()
		{
			RemoteConfigMeta meta = _fixture.Meta(_fixture.Int("lives", 3, cache: false), _fixture.Int("coins"));
			meta.ApplyFetchedValues(Fetched(("lives", "5"), ("coins", "10")), _cache);

			RemoteInt nextLives = _fixture.Int("lives", 3, cache: false);
			RemoteInt nextCoins = _fixture.Int("coins");

			Assert.AreEqual(1, _fixture.Meta(nextLives, nextCoins).LoadCachedValues(_cache));
			Assert.AreEqual(3, nextLives.Value);
			Assert.AreEqual(10, nextCoins.Value);
		}

		[Test]
		public void TheCache_NeverOverridesAValueFetchedThisSession()
		{
			RemoteInt lives = _fixture.Int("lives", 3);
			RemoteConfigMeta meta = _fixture.Meta(lives);
			meta.ApplyFetchedValues(Fetched(("lives", "5")), _cache);
			lives.SetRemoteValue(9);

			Assert.AreEqual(0, meta.LoadCachedValues(_cache));
			Assert.AreEqual(9, lives.Value);
		}

		[Test]
		public void ACachedValueTheVariableCanNoLongerRead_IsDropped_WithAWarning()
		{
			_fixture.Meta(_fixture.Text("mode", "easy")).ApplyFetchedValues(Fetched(("mode", "hard")), _cache);

			// The next version made the variable a number.
			RemoteInt nextMode = _fixture.Int("mode", 1);

			Assert.AreEqual(0, _fixture.Meta(nextMode).LoadCachedValues(_cache));
			Assert.AreEqual(1, nextMode.Value);
			Assert.AreEqual(1, _log.Count(LogType.Warning, "The cached value of 'mode' can't be read as Int32 and was dropped: 'hard'"));
		}

		[Test]
		public void TheEarlierPerVariableCache_IsDeleted_NotRead()
		{
			RemoteInt lives = _fixture.Int("lives", 3);
			RemoteConfigMeta meta = _fixture.Meta(lives);
			_backend.Set("remote_config_lives", "{\"Data\":7}");

			Assert.AreEqual(0, meta.LoadCachedValues(_cache));

			Assert.AreEqual(3, lives.Value, "it may hold a default posing as a remote value");
			Assert.IsFalse(_backend.Contains("remote_config_lives"));
		}

		[Test]
		public void ClearingTheCache_KeepsTheValuesHeld_AndClearingTheValues_KeepsTheCache()
		{
			RemoteInt lives = _fixture.Int("lives", 3);
			RemoteConfigMeta meta = _fixture.Meta(lives);
			meta.ApplyFetchedValues(Fetched(("lives", "5")), _cache);

			meta.ClearAllRemoteValues();
			Assert.AreEqual(3, lives.Value);
			Assert.AreEqual(1, meta.LoadCachedValues(_cache));
			Assert.AreEqual(5, lives.Value);

			meta.ClearCachedValues(_cache);
			Assert.AreEqual(5, lives.Value);
			Assert.IsFalse(_cache.Has(RemoteConfigMeta.CacheKey));
		}

		[Test]
		public void WithoutARegistry_NothingIsLoadedOrApplied()
		{
			RemoteConfigMeta meta = _fixture.Track(ScriptableObject.CreateInstance<RemoteConfigMeta>());

			Assert.AreEqual(0, meta.LoadCachedValues(_cache));
			Assert.AreEqual(default(RemoteConfigUpdate), meta.ApplyFetchedValues(Fetched(("lives", "5")), _cache));
			CollectionAssert.IsEmpty(meta.GetAllVariables());
			Assert.IsNull(meta.GetVariableByKey("lives"));
		}

		// ------------------------------------------------------------------ lookups

		[Test]
		public void LookupsByKey_FollowTheRegistry_AsItChanges()
		{
			RemoteInt lives = _fixture.Int("lives", 3);
			RemoteConfigMeta meta = _fixture.Meta(lives);
			Assert.AreSame(lives, meta.GetVariableByKey("lives"));
			Assert.IsNull(meta.GetVariableByKey("coins"));

			RemoteInt coins = _fixture.Int("coins", 7);
			meta.Registry.Registry.Add(coins);

			Assert.AreSame(coins, meta.GetVariableByKey("coins"));
			Assert.AreSame(coins, meta.GetVariableByKey<int>("coins"));
			Assert.IsNull(meta.GetVariableByKey<bool>("coins"), "another type");
			Assert.AreEqual(7, meta.GetValue<int>("coins"));
			Assert.IsNull(meta.GetVariableByKey(null));
		}

		[Test]
		public void TwoVariablesWithOneKey_TheFirstIsFound()
		{
			RemoteInt first = _fixture.Int("lives", 3);
			RemoteConfigMeta meta = _fixture.Meta(first, _fixture.Int("lives", 4));

			Assert.AreSame(first, meta.GetVariableByKey("lives"));
		}

		[Test]
		public void AVariableIsFound_ByTheIdentityOfAHeldReference()
		{
			RemoteInt lives = _fixture.Int("lives", 3);
			RemoteConfigMeta meta = _fixture.Meta(lives);

			Assert.AreSame(lives, meta.GetVariable(lives.Id));
			Assert.AreSame(lives, meta.GetVariable<int>(lives));
			Assert.IsNull(meta.GetVariable(Uid.NewRandom()));
		}

		private static Dictionary<string, string> Fetched(params (string Key, string Value)[] values)
		{
			var fetched = new Dictionary<string, string>();
			foreach ((string key, string value) in values)
			{
				fetched.Add(key, value);
			}

			return fetched;
		}
	}
}
