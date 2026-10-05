using System;
using AK.Kernel.Persistence;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class JsonEnvelopeTests
	{
		[TestCase("{\"Data\":{}}", JsonKind.Object)]
		[TestCase("{\"Data\":{\"SaveVersion\":1,\"Counts\":[{\"FactId\":{\"_value\":5},\"Count\":2}]}}", JsonKind.Object)]
		[TestCase("{\"Data\":[1,2]}", JsonKind.Array)]
		[TestCase("{\"Data\":[]}", JsonKind.Array)]
		[TestCase("{\"Data\":\"a\\\"b\\\\c\\n\\u00e9 é\"}", JsonKind.String)]
		[TestCase("{\"Data\":\"\"}", JsonKind.String)]
		[TestCase("{\"Data\":5}", JsonKind.Number)]
		[TestCase("{\"Data\":0}", JsonKind.Number)]
		[TestCase("{\"Data\":-1e-7}", JsonKind.Number)]
		[TestCase("{\"Data\":0.5E+10}", JsonKind.Number)]
		[TestCase("{\"Data\":9223372036854775807}", JsonKind.Number)]
		[TestCase("{\"Data\":NaN}", JsonKind.Number, Description = "JsonUtility writes non-finite floats as bare tokens")]
		[TestCase("{\"Data\":Infinity}", JsonKind.Number)]
		[TestCase("{\"Data\":-Infinity}", JsonKind.Number)]
		[TestCase("{\"Data\":true}", JsonKind.Boolean)]
		[TestCase("{\"Data\":false}", JsonKind.Boolean)]
		[TestCase("{\"Data\":null}", JsonKind.Null)]
		[TestCase(" \t\r\n{ \"Data\" : 1 } \n", JsonKind.Number, Description = "whitespace anywhere between tokens")]
		[TestCase("{\"Other\":[{\"Data\":1}],\"Data\":\"x\"}", JsonKind.String, Description = "only the top level counts")]
		[TestCase("{\"D\\u0061ta\":true}", JsonKind.Boolean, Description = "names compare after unescaping")]
		[TestCase("{\"Data\":1,\"Data\":\"last\"}", JsonKind.String, Description = "the last of a repeated name wins")]
		public void AnObject_ReportsItsMembersKind(string json, JsonKind expected)
		{
			Assert.AreEqual(JsonShape.Object, JsonEnvelope.Inspect(json, "Data", out JsonKind kind));
			Assert.AreEqual(expected, kind);
		}

		[TestCase("{}")]
		[TestCase("{\"data\":1}", Description = "names are case-sensitive")]
		[TestCase("{\"Foo\":{\"Data\":1}}", Description = "a nested member doesn't count")]
		[TestCase("{\"Dat\":1}")]
		[TestCase("{\"Datum\":1}")]
		[TestCase("{\"D\\u0061t\":1}")]
		public void AnObjectWithoutTheMember_ReportsNone(string json)
		{
			Assert.AreEqual(JsonShape.Object, JsonEnvelope.Inspect(json, "Data", out JsonKind kind));
			Assert.AreEqual(JsonKind.None, kind);
		}

		[TestCase("[1,2]")]
		[TestCase("5")]
		[TestCase("\"str\"")]
		[TestCase("null")]
		[TestCase("true")]
		public void AValueThatIsntAnObject_ReportsNotAnObject(string json)
		{
			Assert.AreEqual(JsonShape.NotAnObject, JsonEnvelope.Inspect(json, "Data", out JsonKind kind));
			Assert.AreEqual(JsonKind.None, kind);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		[TestCase("not json")]
		[TestCase("{\"Data\":{", Description = "cut short")]
		[TestCase("{\"Data\":{\"Counts\":[1,2")]
		[TestCase("{\"Data\":\"unterminated}")]
		[TestCase("{\"Data\":1}x", Description = "text after the value")]
		[TestCase("{\"Data\":1}{}")]
		[TestCase("{\"Data\":1,}", Description = "trailing comma")]
		[TestCase("{\"Data\":[1,]}")]
		[TestCase("{\"Data\":[1 2]}")]
		[TestCase("{\"Data\":1 \"More\":2}")]
		[TestCase("{Data:1}", Description = "unquoted name")]
		[TestCase("{'Data':1}")]
		[TestCase("{\"Data\" 1}")]
		[TestCase("{\"Data\":}")]
		[TestCase("{\"Data\":01}", Description = "leading zero")]
		[TestCase("{\"Data\":1.}")]
		[TestCase("{\"Data\":.5}")]
		[TestCase("{\"Data\":+1}")]
		[TestCase("{\"Data\":1e}")]
		[TestCase("{\"Data\":1e+}")]
		[TestCase("{\"Data\":-}")]
		[TestCase("{\"Data\":nan}")]
		[TestCase("{\"Data\":-NaN}")]
		[TestCase("{\"Data\":Infinit}")]
		[TestCase("{\"Data\":tru}")]
		[TestCase("{\"Data\":nul}")]
		[TestCase("{\"Data\":\"a\\qb\"}", Description = "unknown escape")]
		[TestCase("{\"Data\":\"\\u12\"}", Description = "short \\u escape")]
		[TestCase("{\"Data\":\"\\u12G4\"}")]
		[TestCase("{\"Data\":\"line\nbreak\"}", Description = "a raw control character in a string")]
		[TestCase("\uFEFF{\"Data\":1}", Description = "a byte-order mark")]
		public void TextThatIsntOneWellFormedValue_ReportsMalformed(string json)
		{
			Assert.AreEqual(JsonShape.Malformed, JsonEnvelope.Inspect(json, "Data", out JsonKind kind));
			Assert.AreEqual(JsonKind.None, kind);
		}

		[Test]
		public void Nesting_IsAcceptedUpToMaxDepth_AndNoDeeper()
		{
			// The root object is level 1, so its member can hold MaxDepth - 1 nested arrays.
			static string Nested(int arrays) => "{\"Data\":" + new string('[', arrays) + new string(']', arrays) + "}";

			Assert.AreEqual(JsonShape.Object, JsonEnvelope.Inspect(Nested(JsonEnvelope.MaxDepth - 1), "Data", out JsonKind kind));
			Assert.AreEqual(JsonKind.Array, kind);
			Assert.AreEqual(JsonShape.Malformed, JsonEnvelope.Inspect(Nested(JsonEnvelope.MaxDepth), "Data", out _));
			Assert.AreEqual(JsonShape.Malformed, JsonEnvelope.Inspect(Nested(100_000), "Data", out _), "far too deep is rejected, not a stack overflow");
		}

		[Test]
		public void Inspect_RejectsANullMemberName()
		{
			Assert.Throws<ArgumentNullException>(() => JsonEnvelope.Inspect("{}", null, out _));
		}

		[Test]
		public void Inspect_DoesNotAllocate()
		{
			const string json = "{\"Data\":{\"SaveVersion\":1,\"Name\":\"a\\\"b\",\"Counts\":[1,2.5,-3e4,NaN],\"On\":true,\"Off\":null}}";
			const string escaped = "{\"D\\u0061ta\":[1]}";

			int Window() => GcAllocations.Count(() =>
			{
				JsonEnvelope.Inspect(json, "Data", out _);
				JsonEnvelope.Inspect(escaped, "Data", out _);
			});

			// The first window compiles the code it runs, and the runtime allocates its string literals then.
			Window();
			Assert.AreEqual(0, Window());
		}
	}
}
