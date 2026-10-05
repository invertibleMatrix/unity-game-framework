using System;
using System.Collections.Generic;
using AK.CoreDomain.RemoteConfig;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.RemoteConfig
{
	/// <summary>Remote variables on their own: reading text as their type, and the default when no remote value is known.</summary>
	public class RemoteVariableTests
	{
		private RemoteConfigFixture _fixture;
		private LogRecorder _log;

		[SetUp]
		public void SetUp()
		{
			_fixture = new RemoteConfigFixture();
			_log     = new LogRecorder();
		}

		[TearDown]
		public void TearDown()
		{
			_fixture.Dispose();
			_log.Dispose();
		}

		[Test]
		public void WithoutARemoteValue_TheDefaultIsRead()
		{
			RemoteInt lives = _fixture.Int("lives", 3);

			Assert.AreEqual(3, lives.Value);
			Assert.AreEqual(3, (int)lives);
			Assert.IsFalse(lives.HasRemoteValue);
			Assert.AreEqual(RemoteValueOrigin.Default, lives.Origin);
			Assert.IsNull(lives.RemoteText);
			Assert.AreEqual("3", lives.GetValueText());
		}

		[Test]
		public void RemoteText_IsReadAsTheType_AndKeptAsGiven()
		{
			RemoteInt lives = _fixture.Int("lives", 3);

			Assert.IsTrue(lives.TrySetRemoteText(" 7 ", RemoteValueOrigin.Fetched));

			Assert.AreEqual(7, lives.Value);
			Assert.AreEqual(3, lives.DefaultValue);
			Assert.AreEqual(RemoteValueOrigin.Fetched, lives.Origin);
			Assert.AreEqual(" 7 ", lives.RemoteText);
			Assert.AreEqual(" 7 ", lives.GetValueText());
		}

		[TestCase(""), TestCase((string)null), TestCase("seven"), TestCase("7.5"), TestCase("1,000")]
		public void TextThatIsNotAnInt_IsNoValue_AndChangesNothing(string text)
		{
			RemoteInt lives = _fixture.Int("lives", 3);
			lives.TrySetRemoteText("5", RemoteValueOrigin.Cached);

			Assert.IsFalse(lives.TrySetRemoteText(text, RemoteValueOrigin.Fetched));

			Assert.AreEqual(5, lives.Value);
			Assert.AreEqual(RemoteValueOrigin.Cached, lives.Origin);
		}

		[Test]
		public void TheDefaultOrigin_IsRefused()
		{
			RemoteInt lives = _fixture.Int("lives", 3);

			Assert.Throws<ArgumentOutOfRangeException>(() => lives.TrySetRemoteText("4", RemoteValueOrigin.Default));
			Assert.IsFalse(lives.HasRemoteValue);
		}

		[Test]
		public void ClearRemoteValue_GoesBackToTheDefault()
		{
			RemoteBool flag = _fixture.Bool("flag");
			flag.TrySetRemoteText("on", RemoteValueOrigin.Fetched);
			Assert.IsTrue(flag.Value);

			flag.ClearRemoteValue();

			Assert.IsFalse(flag.Value);
			Assert.IsFalse(flag.HasRemoteValue);
			Assert.IsNull(flag.RemoteText);
		}

		[Test]
		public void SetRemoteValue_ActsAsAFetch_WithTextThatReadsBack()
		{
			RemoteFloat speed = _fixture.Float("speed", 1f);

			speed.SetRemoteValue(0.1f);

			Assert.AreEqual(0.1f, speed.Value);
			Assert.AreEqual(RemoteValueOrigin.Fetched, speed.Origin);
			Assert.AreEqual("0.1", speed.RemoteText);
		}

		[Test]
		public void AnEmptyString_IsNoValue_FetchedOrSet()
		{
			RemoteString greeting = _fixture.Text("greeting", "hello");

			Assert.IsFalse(greeting.TrySetRemoteText("", RemoteValueOrigin.Fetched));
			greeting.SetRemoteValue("hi");
			Assert.AreEqual("hi", greeting.Value);

			greeting.SetRemoteValue("");
			Assert.AreEqual("hello", greeting.Value);
			Assert.IsFalse(greeting.HasRemoteValue);

			Assert.IsTrue(greeting.TrySetRemoteText("  spaced  ", RemoteValueOrigin.Fetched));
			Assert.AreEqual("  spaced  ", greeting.Value, "a string is taken as it is");
		}

		[Test]
		public void Defaults_AreWrittenAsInvariantText_OnADeviceInAnotherCulture()
		{
			using (new CultureScope("de-DE"))
			{
				Assert.AreEqual("1.5", _fixture.Float("speed", 1.5f).GetDefaultValueText());
				Assert.AreEqual("true", _fixture.Bool("flag", true).GetDefaultValueText());
				Assert.AreEqual("-4", _fixture.Int("lives", -4).GetDefaultValueText());
				Assert.AreEqual("", _fixture.Text("greeting").GetDefaultValueText());
			}
		}

		// ------------------------------------------------------------------ JSON

		[Test]
		public void Json_ReadsOneObject_LeavingMembersItDoesNotMentionAtTheirInitializers()
		{
			RemoteSettings settings = Json(new Settings { Lives = 3, Mode = "hard" });

			Assert.IsTrue(settings.TrySetRemoteText("{\"Lives\":9,\"Levels\":[1,2]}", RemoteValueOrigin.Fetched));

			Assert.AreEqual(9, settings.Value.Lives);
			Assert.AreEqual("easy", settings.Value.Mode, "the initializer, not the default's value");
			CollectionAssert.AreEqual(new[] { 1, 2 }, settings.Value.Levels);
		}

		[TestCase("[1,2]"), TestCase("42"), TestCase("\"text\""), TestCase("{\"Lives\":"), TestCase("not json"), TestCase("{} {}")]
		public void Json_RefusesAnythingButOneObject(string text)
		{
			RemoteSettings settings = Json(new Settings { Lives = 3 });

			Assert.IsFalse(settings.TrySetRemoteText(text, RemoteValueOrigin.Fetched));

			Assert.AreEqual(3, settings.Value.Lives);
		}

		[Test]
		public void Json_InsideAJsonString_IsDecodedFirst()
		{
			RemoteSettings settings = Json(new Settings());

			Assert.IsTrue(settings.TrySetRemoteText("\"{\\\"Lives\\\":4}\"", RemoteValueOrigin.Fetched));

			Assert.AreEqual(4, settings.Value.Lives);
		}

		[Test]
		public void Json_InABrokenJsonString_IsStillRead_WithAWarning()
		{
			RemoteSettings settings = Json(new Settings());

			Assert.IsTrue(settings.TrySetRemoteText("\"{\"Lives\":5}\"", RemoteValueOrigin.Fetched));

			Assert.AreEqual(5, settings.Value.Lives);
			Assert.AreEqual(1, _log.Count(UnityEngine.LogType.Warning, "'settings' \\(settings\\) holds a JSON string that isn't valid JSON"));
		}

		[Test]
		public void Json_TheDefaultIsACopy_MadeOnce_SoCallersCannotChangeTheAsset()
		{
			var defaults = new Settings { Lives = 3 };
			RemoteSettings settings = Json(defaults);

			Settings value = settings.Value;
			Assert.AreNotSame(defaults, value);
			Assert.AreEqual(3, value.Lives);
			Assert.AreSame(value, settings.Value, "made once");

			value.Lives = 99;

			Assert.AreEqual(3, defaults.Lives);
			Assert.AreEqual("{\"Lives\":3,\"Mode\":\"easy\",\"Levels\":[]}", settings.GetDefaultValueText());
		}

		private RemoteSettings Json(Settings defaults)
		{
			RemoteSettings settings = _fixture.Variable<RemoteSettings>("settings");
			settings.SetDefault(defaults);
			return settings;
		}

		[Serializable]
		public sealed class Settings
		{
			public int Lives = 1;
			public string Mode = "easy";
			public List<int> Levels = new();
		}

		private sealed class RemoteSettings : RemoteJson<Settings>
		{
			public void SetDefault(Settings defaults) => _defaultValue = defaults;
		}
	}
}
