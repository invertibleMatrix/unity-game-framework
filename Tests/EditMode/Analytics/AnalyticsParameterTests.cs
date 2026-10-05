using AK.CoreDomain.Analytics;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Analytics
{
	/// <summary>A parameter's default value, read as its type with the invariant culture.</summary>
	public class AnalyticsParameterTests
	{
		[Test]
		public void TypedDefault_ParsesWithTheInvariantCulture()
		{
			using (new CultureScope("de-DE"))
			{
				Assert.AreEqual(1.5f, With("1.5").GetDefaultValue<float>());
				Assert.AreEqual(1000f, With("1,000").GetDefaultValue<float>());
				Assert.AreEqual(-12, With("-12").GetDefaultValue<int>());
				Assert.AreEqual(true, With("true").GetDefaultValue<bool>());
				Assert.AreEqual("1.5", With("1.5").GetDefaultValue<string>());
			}
		}

		[Test]
		public void TypedDefault_IsTheTypesDefault_WhenItCannotBeRead()
		{
			Assert.AreEqual(0, With("twelve").GetDefaultValue<int>());
			Assert.AreEqual(0, With("1,000").GetDefaultValue<int>());
			Assert.AreEqual(0f, With("fast").GetDefaultValue<float>());
			Assert.AreEqual(false, With("yes").GetDefaultValue<bool>());
			Assert.AreEqual(0d, With("1.5").GetDefaultValue<double>(), "an unsupported type");
			Assert.IsNull(With("").GetDefaultValue<string>());
			Assert.AreEqual(0, With(null).GetDefaultValue<int>());
		}

		[Test]
		public void Default_IsBoxedAsTheParameterType()
		{
			using (new CultureScope("de-DE"))
			{
				Assert.That(With("7", AnalyticsParameterType.Integer).GetDefaultValue(), Is.TypeOf<int>().And.EqualTo(7));
				Assert.That(With("2.5", AnalyticsParameterType.Float).GetDefaultValue(), Is.TypeOf<float>().And.EqualTo(2.5f));
				Assert.That(With("True", AnalyticsParameterType.Boolean).GetDefaultValue(), Is.TypeOf<bool>().And.EqualTo(true));
				Assert.That(With("2.5", AnalyticsParameterType.String).GetDefaultValue(), Is.TypeOf<string>().And.EqualTo("2.5"));
				Assert.AreEqual("{\"a\":1}", With("{\"a\":1}", AnalyticsParameterType.JsonObject).GetDefaultValue());
			}
		}

		[Test]
		public void Default_ThatCannotBeRead_IsZeroOrFalse_AndAMissingOneIsNull()
		{
			Assert.That(With("x", AnalyticsParameterType.Integer).GetDefaultValue(), Is.TypeOf<int>().And.EqualTo(0));
			Assert.That(With("x", AnalyticsParameterType.Float).GetDefaultValue(), Is.TypeOf<float>().And.EqualTo(0f));
			Assert.That(With("x", AnalyticsParameterType.Boolean).GetDefaultValue(), Is.TypeOf<bool>().And.EqualTo(false));
			Assert.IsNull(With("", AnalyticsParameterType.Integer).GetDefaultValue());
			Assert.IsNull(With(null, AnalyticsParameterType.String).GetDefaultValue());
		}

		private static AnalyticsParameter With(string defaultValue, AnalyticsParameterType type = AnalyticsParameterType.String) =>
			new() { DefaultValue = defaultValue, Type = type };
	}
}
