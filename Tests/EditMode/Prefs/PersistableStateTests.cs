using System;
using System.Globalization;
using AK.Core;
using AK.Kernel.Persistence;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests
{
	public class PersistableStateTests
	{
		private const string Key = "TEST_MODEL";

		private static readonly DateTime Noon = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

		private InMemoryKeyValueStore _backend;
		private PrefsStore            _store;

		[Serializable]
		public class Model : PersistableState<Model>
		{
			// What this build's code calls the current save version; tests move it to stand in
			// for older and newer builds.
			public static int Version = 1;
			public static int Migrations;
			public static int MigratedFrom;

			public int Stars;

			[NonSerialized] public int Resets;

			protected override string SaveKey => Key;
			protected override int CurrentSaveVersion => Version;

			protected override void OnMigrate()
			{
				Migrations++;
				MigratedFrom = SaveVersion;
				Stars *= 10;
			}

			protected override void OnReset() => Resets++;
		}

		[SetUp]
		public void SetUp()
		{
			_backend = new InMemoryKeyValueStore();
			_store   = new PrefsStore(_backend, () => Noon);

			Model.Version      = 1;
			Model.Migrations   = 0;
			Model.MigratedFrom = 0;
		}

		private Model SavedModel(int stars)
		{
			Model model = Model.Load(_store);
			model.Stars = stars;
			model.Commit();
			return model;
		}

		// ---------------------------------------------------------------- versions

		[Test]
		public void AFreshState_IsBornAtTheCurrentVersion_AndNeverMigrates()
		{
			Model.Version = 3;

			Model model = Model.Load(_store);
			model.Commit();
			Model.Load(_store);

			Assert.IsFalse(model.IsLoadedFromSave);
			Assert.AreEqual(3, model.SaveVersion);
			Assert.AreEqual(0, Model.Migrations);
		}

		[Test]
		public void AnOlderSave_MigratesOnce_AndTheMigrationIsSaved()
		{
			SavedModel(stars: 2);
			Model.Version = 2;

			Model migrated;
			using (ExpectedLog.Info("Migrating Model from version 1 to 2"))
			{
				migrated = Model.Load(_store);
			}

			Assert.IsTrue(migrated.IsLoadedFromSave);
			Assert.AreEqual(1, Model.MigratedFrom, "OnMigrate sees the version it migrates from");
			Assert.AreEqual(2, migrated.SaveVersion);
			Assert.AreEqual(20, migrated.Stars);

			Model reloaded = Model.Load(_store);

			Assert.AreEqual(1, Model.Migrations, "written back, so it isn't migrated again");
			Assert.AreEqual(20, reloaded.Stars);
		}

		[Test]
		public void ASaveFromANewerBuild_IsSetAside_AndTheStateStartsFresh()
		{
			Model.Version = 5;
			SavedModel(stars: 9);
			Model.Version = 4;

			Model model;
			using (ExpectedLog.Error("save version 5 is newer than this build's 4"))
			{
				model = Model.Load(_store);
			}

			Assert.IsFalse(model.IsLoadedFromSave);
			Assert.AreEqual(0, model.Stars);
			Assert.AreEqual(4, model.SaveVersion);
			Assert.IsTrue(_backend.Contains(Key + ".corrupt.20261004T120000000Z"), "the newer save is kept");
		}

		[Test]
		public void AnUnreadableSave_IsSetAside_AndTheStateStartsFresh()
		{
			_backend.Set(Key, "{\"Data\":{\"Stars\":");

			Model model;
			using (ExpectedLog.Error($"'{Key}' can't be used"))
			{
				model = Model.Load(_store);
			}

			model.Stars = 1;
			model.Commit();

			Assert.IsFalse(model.IsLoadedFromSave);
			Assert.IsTrue(_backend.TryGet(Key + ".corrupt.20261004T120000000Z", out string kept));
			Assert.AreEqual("{\"Data\":{\"Stars\":", kept);
		}

		// ---------------------------------------------------------------- sessions

		[Test]
		public void Initialize_NumbersTheFirstSessionOne()
		{
			Model first = Model.Load(_store);
			first.Initialize(out bool isFirstLaunch);

			Assert.IsTrue(isFirstLaunch);
			Assert.AreEqual(1, first.CurrentSession);

			Model second = Model.Load(_store);
			second.Initialize(out bool isFirstLaunchAgain);

			Assert.IsFalse(isFirstLaunchAgain);
			Assert.AreEqual(2, second.CurrentSession);
		}

		[Test]
		public void Initialize_CountsANewUtcDay_SinceTheLastCommit()
		{
			Model model = Model.Load(_store);
			model.SessionEndTime = "2020-01-01T23:59:59.0000000Z";
			int day = model.CurrentDay;

			model.Initialize(out _);

			Assert.AreEqual(day + 1, model.CurrentDay);
		}

		[TestCase("th-TH")]
		[TestCase("ar-SA")]
		public void SessionTimes_AreWrittenAndReadTheSame_UnderAnyCulture(string culture)
		{
			using (new CultureScope(culture))
			{
				Model model = Model.Load(_store);
				model.Initialize(out _);

				string year = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
				StringAssert.StartsWith(year + "-", model.SessionStartTime, "a Gregorian year, not the culture's");
				StringAssert.EndsWith("Z", model.SessionEndTime);

				Assert.IsTrue(model.TryGetSessionEndTime(out DateTime end));
				Assert.AreEqual(DateTimeKind.Utc, end.Kind);
				Assert.Less(Math.Abs((DateTime.UtcNow - end).TotalMinutes), 1);
			}
		}

		[Test]
		public void TryGetSessionEndTime_IsFalse_ForTextThatIsntATime()
		{
			Model model = Model.Load(_store);
			model.SessionEndTime = "yesterday";

			Assert.IsFalse(model.TryGetSessionEndTime(out DateTime end));
			Assert.AreEqual(default(DateTime), end, "not the local time now, as before");
		}

		// ---------------------------------------------------------------- reset and scopes

		[Test]
		public void DeleteAll_ResetsALiveModel_SoItsNextCommitCantBringTheDataBack()
		{
			Model model = SavedModel(stars: 5);

			_store.DeleteAll();

			Assert.AreEqual(0, model.Stars, "back to fresh values at once");
			Assert.AreEqual(1, model.Resets);
			Assert.IsFalse(model.IsLoadedFromSave);

			model.Commit();

			Assert.AreEqual(0, Model.Load(_store).Stars);
		}

		[Test]
		public void AScopedLoad_HasItsOwnSave()
		{
			SavedModel(stars: 1);

			Model account = Model.Load(_store, "player-1");
			account.Stars = 2;
			account.Commit();

			Assert.AreEqual(Key + "@player-1", account.StorageKey);
			Assert.AreEqual(1, Model.Load(_store).Stars);
			Assert.AreEqual(2, Model.Load(_store, "player-1").Stars);
		}

		[Test]
		public void AdoptDeviceSave_GivesTheDevicesSave_OnlyToAScopeWithoutOne()
		{
			SavedModel(stars: 3);

			Assert.IsTrue(Model.AdoptDeviceSave(_store, "player-1"));
			Assert.IsFalse(Model.HasSave(_store), "moved, not copied");
			Assert.AreEqual(3, Model.Load(_store, "player-1").Stars);

			Assert.IsFalse(Model.AdoptDeviceSave(_store, "player-2"), "the device has nothing left to give");

			SavedModel(stars: 4);

			Assert.IsFalse(Model.AdoptDeviceSave(_store, "player-1"), "a scope with a save keeps its own");
			Assert.AreEqual(3, Model.Load(_store, "player-1").Stars);
			Assert.IsTrue(Model.HasSave(_store), "and the device's save stays where it is");
		}
	}
}
