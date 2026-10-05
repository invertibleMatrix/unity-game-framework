using System;
using System.Collections.Generic;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using AK.Kernel.Analytics;
using AK.Services;
using AK.Services.Analytics;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests.Analytics
{
	/// <summary>
	/// The analytics facade: queueing until init, the definition overlay (fail-open, defaults,
	/// per-user sampling), and handing providers their options and the game's taxonomy.
	/// </summary>
	public class AnalyticsServiceTests
	{
		// user-2's bucket is about 0.20 and user-1's about 0.47.
		private const string InSampleUser = "user-2";
		private const string OutOfSampleUser = "user-1";

		private readonly List<Object> _assets = new();
		private LogRecorder _log;

		[SetUp]
		public void SetUp()
		{
			_log = new LogRecorder();
		}

		[TearDown]
		public void TearDown()
		{
			foreach (Object asset in _assets)
			{
				Object.DestroyImmediate(asset);
			}

			_assets.Clear();
			_log.Dispose();
		}

		[Test]
		public void MissingDefinition_StillDispatches()
		{
			(AnalyticsService service, RecordingAnalyticsProvider provider) = Started();

			service.TrackDesign("dice:roll", 1f);

			Assert.AreEqual(1, provider.Events.Count);
			Assert.AreEqual("dice:roll", provider.Events[0].Id);
		}

		[Test]
		public void QueuesEventsUntilInitialize()
		{
			var provider = new RecordingAnalyticsProvider();
			var service = new AnalyticsService();
			service.RegisterProvider(provider);

			service.TrackDesign("install:first_open");
			Assert.AreEqual(0, provider.Events.Count);

			service.Initialize();
			Assert.AreEqual(1, provider.Events.Count);
			Assert.AreEqual("install:first_open", provider.Events[0].Id);
		}

		[Test]
		public void InactiveDefinition_DropsEvent()
		{
			AnalyticsEventDefinition def = Definition("sampled_out");
			def.IsActive = false;
			(AnalyticsService service, RecordingAnalyticsProvider provider) = Started(Meta(def));

			service.TrackDesign("sampled_out");

			Assert.AreEqual(0, provider.Events.Count);
		}

		[Test]
		public void MissingRequiredParam_FailOpen_StillSends()
		{
			AnalyticsEventDefinition def = Definition("needs_age");
			def.Parameters = new List<AnalyticsParameter> { RequiredInteger("age") };
			(AnalyticsService service, RecordingAnalyticsProvider provider) = Started(Meta(def));

			service.TrackDesign("needs_age");

			Assert.AreEqual(1, provider.Events.Count);
		}

		[Test]
		public void MissingRequiredParam_FailClosed_Drops()
		{
			AnalyticsEventDefinition def = Definition("needs_age");
			def.FailClosed = true;
			def.Parameters = new List<AnalyticsParameter> { RequiredInteger("age") };
			(AnalyticsService service, RecordingAnalyticsProvider provider) = Started(Meta(def));

			service.TrackDesign("needs_age");

			Assert.AreEqual(0, provider.Events.Count);
		}

		[Test]
		public void DefaultValueFillsRequiredParam()
		{
			AnalyticsEventDefinition def = Definition("needs_age");
			def.FailClosed = true;
			AnalyticsParameter age = RequiredInteger("age");
			age.DefaultValue = "0";
			def.Parameters = new List<AnalyticsParameter> { age };
			(AnalyticsService service, RecordingAnalyticsProvider provider) = Started(Meta(def));

			service.TrackDesign("needs_age");

			Assert.AreEqual(1, provider.Events.Count);
			Assert.AreEqual(0, provider.Events[0].Parameters["age"]);
		}

		[Test]
		public void QueuesCustomDimensionsUntilInitialize()
		{
			var provider = new RecordingAnalyticsProvider();
			var service = new AnalyticsService();
			service.RegisterProvider(provider);

			service.SetCustomDimension(1, "age_3");
			Assert.AreEqual(0, provider.Dimensions.Count);

			service.Initialize();
			Assert.AreEqual(1, provider.Dimensions.Count);
			Assert.AreEqual(1, provider.Dimensions[0].Index);
			Assert.AreEqual("age_3", provider.Dimensions[0].Value);
		}

		[Test]
		public void DisabledService_DoesNotDispatch()
		{
			(AnalyticsService service, RecordingAnalyticsProvider provider) = Started();
			service.SetAnalyticsEnabled(false);

			service.TrackDesign("nope");

			Assert.AreEqual(0, provider.Events.Count);
		}

		[Test]
		public void EarlyStart_ReachesProviders_OnceOnly_BeforeInitialize()
		{
			var provider = new RecordingAnalyticsProvider();
			var service = new AnalyticsService();
			service.RegisterProvider(provider);

			service.EarlyStart();
			service.EarlyStart();

			Assert.AreEqual(1, provider.EarlyStarts);
			Assert.IsFalse(service.IsInitialized);
		}

		[Test]
		public void EarlyStart_DoesNotFlushQueuedEvents()
		{
			var provider = new RecordingAnalyticsProvider();
			var service = new AnalyticsService();
			service.RegisterProvider(provider);

			service.TrackDesign("install:first_open");
			service.EarlyStart();
			Assert.AreEqual(0, provider.Events.Count);

			service.Initialize();
			Assert.AreEqual(1, provider.Events.Count);
		}

		[Test]
		public void ProviderConfig_ReachesProvidersAlongsideUserIdAndBuild()
		{
			var provider = new RecordingAnalyticsProvider();
			var service = new AnalyticsService();
			service.RegisterProvider(provider);

			service.Initialize(null, new AnalyticsInitOptions
			{
				UserId = "player-1",
				Build = "0.3.19",
				ProviderConfig = new Dictionary<string, string> { { MetaProviderConfig.InstallsOnlyKey, "true" } }
			});

			Assert.IsTrue(MetaProviderConfig.IsInstallsOnly(provider.Config));
			Assert.AreEqual("player-1", provider.Config["userId"]);
			Assert.AreEqual("0.3.19", provider.Config["build"]);
		}

		// ---------------------------------------------------------------- taxonomy and options

		[Test]
		public void Taxonomy_ReachesProvidersOnce_BeforeTheyStart()
		{
			var taxonomy = new AnalyticsTaxonomy(new[] { new AnalyticsDimension("age", "age_1") });
			var provider = new RecordingAnalyticsProvider();
			var service = new AnalyticsService();
			service.RegisterProvider(provider);
			service.SetTaxonomy(taxonomy);

			service.Initialize();

			Assert.AreSame(taxonomy, provider.Taxonomy);
			Assert.AreEqual(1, provider.Configures);
			Assert.IsTrue(provider.ConfiguredBeforeInitialize);
		}

		[Test]
		public void NoTaxonomy_ProvidersGetTheEmptyOne()
		{
			(_, RecordingAnalyticsProvider provider) = Started();

			Assert.AreSame(AnalyticsTaxonomy.Empty, provider.Taxonomy);
		}

		[Test]
		public void SetTaxonomyOrOptions_AfterInitialize_Throws()
		{
			(AnalyticsService service, _) = Started();

			Assert.Throws<InvalidOperationException>(() => service.SetTaxonomy(AnalyticsTaxonomy.Empty));
			Assert.Throws<InvalidOperationException>(() => service.SetOptions(new AnalyticsInitOptions()));
		}

		[Test]
		public void SetOptions_AreWhatInitializeUses()
		{
			var provider = new RecordingAnalyticsProvider();
			var service = new AnalyticsService();
			service.RegisterProvider(provider);
			var options = new AnalyticsInitOptions { UserId = "player-7", Build = "1.2.3" };

			service.SetOptions(options);
			service.Initialize();

			Assert.AreSame(options, provider.Options);
			CollectionAssert.AreEqual(new[] { "player-7" }, provider.UserIds);
			Assert.AreEqual("1.2.3", provider.Config["build"]);
		}

		[Test]
		public void Builder_AppliesOptionsAndTaxonomy()
		{
			var taxonomy = new AnalyticsTaxonomy(new[] { new AnalyticsDimension("age", "age_1") });
			var options = new AnalyticsInitOptions { UserId = "player-9" };
			var provider = new RecordingAnalyticsProvider();

			AnalyticsService service = new AnalyticsServiceBuilder()
				.AddProvider(provider)
				.WithOptions(options)
				.WithTaxonomy(taxonomy)
				.Build();
			service.Initialize();

			Assert.AreSame(options, provider.Options);
			Assert.AreSame(taxonomy, provider.Taxonomy);
			CollectionAssert.Contains(provider.UserIds, "player-9");
		}

		[Test]
		public void CustomDimensions_AreNamedByTheTaxonomy()
		{
			var taxonomy = new AnalyticsTaxonomy(new[]
			{
				new AnalyticsDimension("age", "age_1"),
				new AnalyticsDimension("life", "life_2")
			});
			var provider = new RecordingAnalyticsProvider();
			var service = new AnalyticsService();
			service.RegisterProvider(provider);
			service.SetTaxonomy(taxonomy);
			service.Initialize();

			service.SetCustomDimension(2, "life_2");
			service.SetCustomDimension(3, "x");

			CollectionAssert.AreEqual(new[] { ("life", "life_2"), ("custom_03", "x") }, provider.UserProperties);
		}

		// ---------------------------------------------------------------- per-user sampling

		[Test]
		public void Sampling_KeepsAUserInTheSample_OnEveryEvent()
		{
			Assert.That(UserSampling.Bucket(InSampleUser), Is.LessThan(0.3));
			AnalyticsEventDefinition def = Definition("sampled");
			def.SamplingRate = 0.3f;
			(AnalyticsService service, RecordingAnalyticsProvider provider) = Started(Meta(def), InSampleUser);

			for (int i = 0; i < 20; i++)
			{
				service.TrackDesign("sampled");
			}

			Assert.AreEqual(20, provider.Events.Count);
		}

		[Test]
		public void Sampling_KeepsAUserOutOfTheSample_OnEveryEvent_AndAcrossRuns()
		{
			Assert.That(UserSampling.Bucket(OutOfSampleUser), Is.GreaterThanOrEqualTo(0.3));
			AnalyticsEventDefinition def = Definition("sampled");
			def.SamplingRate = 0.3f;

			for (int run = 0; run < 3; run++)
			{
				(AnalyticsService service, RecordingAnalyticsProvider provider) = Started(Meta(def), OutOfSampleUser);
				for (int i = 0; i < 20; i++)
				{
					service.TrackDesign("sampled");
				}

				Assert.AreEqual(0, provider.Events.Count, "run " + run);
			}
		}

		[Test]
		public void Sampling_FollowsTheUserId_WhenItChanges()
		{
			AnalyticsEventDefinition def = Definition("sampled");
			def.SamplingRate = 0.3f;
			(AnalyticsService service, RecordingAnalyticsProvider provider) = Started(Meta(def), OutOfSampleUser);

			service.TrackDesign("sampled");
			service.SetUserID(InSampleUser);
			service.TrackDesign("sampled");

			Assert.AreEqual(1, provider.Events.Count);
		}

		[Test]
		public void Sampling_WithoutAUserId_TreatsTheRunAsOneUser()
		{
			AnalyticsEventDefinition def = Definition("sampled");
			def.SamplingRate = 0.5f;
			(AnalyticsService service, RecordingAnalyticsProvider provider) = Started(Meta(def));

			for (int i = 0; i < 40; i++)
			{
				service.TrackDesign("sampled");
			}

			Assert.That(provider.Events.Count, Is.EqualTo(0).Or.EqualTo(40));
		}

		// ---------------------------------------------------------------- helpers

		private (AnalyticsService, RecordingAnalyticsProvider) Started(AnalyticsMeta meta = null, string userId = null)
		{
			var provider = new RecordingAnalyticsProvider();
			var service = new AnalyticsService();
			service.RegisterProvider(provider);
			service.Initialize(meta, new AnalyticsInitOptions { UserId = userId });
			return (service, provider);
		}

		private AnalyticsEventDefinition Definition(string eventId)
		{
			var def = ScriptableObject.CreateInstance<AnalyticsEventDefinition>();
			def.EventID = eventId;
			_assets.Add(def);
			return def;
		}

		private AnalyticsMeta Meta(params AnalyticsEventDefinition[] definitions)
		{
			var meta = ScriptableObject.CreateInstance<AnalyticsMeta>();
			meta.Events = new List<AnalyticsEventDefinition>(definitions);
			_assets.Add(meta);
			return meta;
		}

		private static AnalyticsParameter RequiredInteger(string key) =>
			new() { Key = key, IsRequired = true, Type = AnalyticsParameterType.Integer };
	}
}
