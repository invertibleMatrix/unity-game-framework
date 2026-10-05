#if UGFW_FIREBASE_REMOTE_CONFIG
using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain.RemoteConfig;
using AK.Kernel.Persistence;
using AK.Services;
using AK.Tests.Support;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace AK.Tests.RemoteConfig
{
	/// <summary>
	/// The Firebase service where Firebase can't be used, as in the editor and on devices
	/// without Google Play services: the cached values must still reach the game.
	/// </summary>
	public class FirebaseRemoteConfigServiceTests
	{
		private RemoteConfigFixture _fixture;
		private LogRecorder _log;
		private PrefsStore _cache;

		[SetUp]
		public void SetUp()
		{
			_fixture = new RemoteConfigFixture();
			_log     = new LogRecorder();
			_cache   = new PrefsStore(new InMemoryKeyValueStore());
		}

		[TearDown]
		public void TearDown()
		{
			_fixture.Dispose();
			_log.Dispose();
		}

		[Test]
		public void WithoutFirebase_TheCachedValuesHold_AndInitializationFinishes()
		{
			_fixture.Meta(_fixture.Int("lives", 3)).ApplyFetchedValues(new Dictionary<string, string> { { "lives", "5" } }, _cache);
			RemoteInt lives = _fixture.Int("lives", 3);
			var service = new FirebaseRemoteConfigService(_fixture.Meta(lives), new UnavailableFirebase(), cache: _cache);

			Assert.AreEqual(UniTaskStatus.Succeeded, service.InitializeAsync().Status);

			Assert.IsTrue(service.IsInitialized);
			Assert.AreEqual(5, lives.Value);
			Assert.AreEqual(RemoteValueOrigin.Cached, lives.Origin);
			Assert.AreEqual(1, _log.Count(LogType.Warning, "Firebase isn't available \\(not in this test\\); using cached and default values"));

			Assert.AreEqual(UniTaskStatus.Succeeded, service.FetchAndActivateAsync().Status);
			Assert.AreEqual(5, lives.Value, "nothing fetched, nothing changed");
		}

		[Test]
		public void ACancelledInitialization_LeavesItUninitialized_AndCanRunAgain()
		{
			RemoteInt lives = _fixture.Int("lives", 3);
			var service = new FirebaseRemoteConfigService(_fixture.Meta(lives), new UnavailableFirebase(), cache: _cache);

			using (var cancellation = new CancellationTokenSource())
			{
				cancellation.Cancel();
				Assert.AreEqual(UniTaskStatus.Canceled, service.InitializeAsync(cancellation.Token).Status);
			}

			Assert.IsFalse(service.IsInitialized);
			Assert.AreEqual(UniTaskStatus.Succeeded, service.InitializeAsync().Status);
			Assert.IsTrue(service.IsInitialized);
		}

		[Test]
		public void WithoutAMeta_InitializationFinishes_WithAnError()
		{
			var service = new FirebaseRemoteConfigService(null, new UnavailableFirebase(), cache: _cache);

			using (ExpectedLog.Error("No RemoteConfigMeta"))
			{
				Assert.AreEqual(UniTaskStatus.Succeeded, service.InitializeAsync().Status);
			}

			Assert.IsTrue(service.IsInitialized);
		}

		[Test]
		public void TheConstructor_RefusesNoFirebase_AndANegativeExpiration()
		{
			RemoteConfigMeta meta = _fixture.Meta();

			Assert.Throws<ArgumentNullException>(() => new FirebaseRemoteConfigService(meta, null, cache: _cache));
			Assert.Throws<ArgumentOutOfRangeException>(() => new FirebaseRemoteConfigService(meta, new UnavailableFirebase(), TimeSpan.FromSeconds(-1), _cache));
			Assert.AreEqual(TimeSpan.FromHours(12), FirebaseRemoteConfigService.DefaultCacheExpiration, "the SDK's own default");
		}

		private sealed class UnavailableFirebase : IFirebaseInitializationService
		{
			public bool IsInitialized => true;
			public bool IsAvailable => false;
			public string UnavailableReason => "not in this test";

			public UniTask<bool> InitializeAsync(CancellationToken cancellationToken = default) => UniTask.FromResult(false);

			public bool CheckAvailable() => false;

			public void EnsureAvailable() => throw new InvalidOperationException(UnavailableReason);
		}
	}
}
#endif
