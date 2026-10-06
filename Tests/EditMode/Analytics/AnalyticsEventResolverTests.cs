using System.Collections.Generic;
using AK.CoreDomain;
using AK.CoreDomain.Analytics;
using AK.Services.Analytics;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests.Analytics
{
	/// <summary>The definition overlay: sampling by the user's bucket, and typed defaults.</summary>
	public class AnalyticsEventResolverTests
	{
		private readonly List<Object> _assets = new();

		[TearDown]
		public void TearDown()
		{
			foreach (Object asset in _assets)
			{
				Object.DestroyImmediate(asset);
			}

			_assets.Clear();
		}

		[TestCase(0.25f, 0.2, false)]
		[TestCase(0.25f, 0.25, true)]
		[TestCase(0.25f, 0.9, true)]
		[TestCase(1f, 0.9999, false)]
		[TestCase(0f, 0.0, true)]
		public void Sampling_DropsUsersOutsideTheRate(float rate, double userBucket, bool dropped)
		{
			AnalyticsEventDefinition def = Definition("sampled");
			def.SamplingRate = rate;

			AnalyticsEventResolver.Result result = AnalyticsEventResolver.Resolve(AnalyticsEvent.Design("sampled"), Meta(def), userBucket);

			Assert.AreEqual(dropped, result.Dropped);
			Assert.AreEqual(dropped ? "sampled" : null, result.DropReason);
		}

		[Test]
		public void NoDefinition_IsNeverSampled()
		{
			AnalyticsEventResolver.Result result = AnalyticsEventResolver.Resolve(AnalyticsEvent.Design("free"), null, 0.9999);

			Assert.IsFalse(result.Dropped);
			Assert.IsTrue(result.MissingDefinition);
		}

		[Test]
		public void Defaults_ReadTheSameInEveryCulture()
		{
			AnalyticsEventDefinition def = Definition("defaults");
			def.Parameters = new List<AnalyticsParameter>
			{
				Parameter("count", AnalyticsParameterType.Integer, "-4"),
				Parameter("ratio", AnalyticsParameterType.Float, "1.5"),
				Parameter("flag", AnalyticsParameterType.Boolean, "True"),
				Parameter("label", AnalyticsParameterType.String, "1.5"),
				Parameter("broken", AnalyticsParameterType.Float, "fast")
			};

			Dictionary<string, object> parameters;
			using (new CultureScope("de-DE"))
			{
				parameters = AnalyticsEventResolver.Resolve(AnalyticsEvent.Design("defaults"), Meta(def), 0d).Event.Parameters;
			}

			Assert.AreEqual(-4, parameters["count"]);
			Assert.AreEqual(1.5f, parameters["ratio"]);
			Assert.AreEqual(true, parameters["flag"]);
			Assert.AreEqual("1.5", parameters["label"]);
			Assert.AreEqual(0f, parameters["broken"]);
		}

		[Test]
		public void Defaults_NeverReplaceASentValue()
		{
			AnalyticsEventDefinition def = Definition("defaults");
			def.Parameters = new List<AnalyticsParameter> { Parameter("count", AnalyticsParameterType.Integer, "3") };
			AnalyticsEvent evt = AnalyticsEvent.Design("defaults", parameters: new Dictionary<string, object> { { "count", 9 } });

			AnalyticsEventResolver.Result result = AnalyticsEventResolver.Resolve(evt, Meta(def), 0d);

			Assert.AreEqual(9, result.Event.Parameters["count"]);
			Assert.IsFalse(result.UsedDefaults);
		}

		[Test]
		public void ParameterKey_IsTheSnakeCaseName_UnlessAKeyIsSet()
		{
			Assert.AreEqual("device_model", AnalyticsEventResolver.ParameterKey(new AnalyticsParameter { Name = ParameterName.DeviceModel }));
			Assert.AreEqual("level_number", AnalyticsEventResolver.ParameterKey(new AnalyticsParameter { Name = ParameterName.LevelNumber }));
			Assert.AreEqual("custom", AnalyticsEventResolver.ParameterKey(new AnalyticsParameter { Name = ParameterName.ItemId, Key = "custom" }));
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

		private static AnalyticsParameter Parameter(string key, AnalyticsParameterType type, string defaultValue) =>
			new() { Key = key, Type = type, IsRequired = false, DefaultValue = defaultValue };
	}
}
