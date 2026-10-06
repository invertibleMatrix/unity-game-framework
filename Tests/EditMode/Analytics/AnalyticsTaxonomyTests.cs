using System;
using AK.Services.Analytics;
using NUnit.Framework;

namespace AK.Tests.Analytics
{
	/// <summary>A game's analytics vocabulary: dimension names and values by slot.</summary>
	public class AnalyticsTaxonomyTests
	{
		[Test]
		public void Empty_NamesSlotsByNumber_AndHasNoValuesOrHooks()
		{
			AnalyticsTaxonomy empty = AnalyticsTaxonomy.Empty;

			Assert.AreEqual("custom_01", empty.DimensionName(1));
			Assert.AreEqual("custom_12", empty.DimensionName(12));
			CollectionAssert.IsEmpty(empty.Dimensions);
			CollectionAssert.IsEmpty(empty.AllowedValues(1));
			Assert.IsFalse(empty.TryGetDimensionSlot("custom_01", out _));
			Assert.IsNull(empty.FlatNamer);
			Assert.IsNull(empty.AppsFlyerSelector);
		}

		[Test]
		public void Dimensions_NameTheirSlots_InOrder()
		{
			var taxonomy = new AnalyticsTaxonomy(new[]
			{
				new AnalyticsDimension("age", "age_1", "age_2"),
				new AnalyticsDimension("life", "life_1")
			});

			Assert.AreEqual("age", taxonomy.DimensionName(1));
			Assert.AreEqual("life", taxonomy.DimensionName(2));
			Assert.AreEqual("custom_03", taxonomy.DimensionName(3));
			Assert.AreEqual("custom_00", taxonomy.DimensionName(0));
			CollectionAssert.AreEqual(new[] { "age_1", "age_2" }, taxonomy.AllowedValues(1));
			CollectionAssert.IsEmpty(taxonomy.AllowedValues(3));

			Assert.IsTrue(taxonomy.TryGetDimensionSlot("life", out int slot));
			Assert.AreEqual(2, slot);
			Assert.IsFalse(taxonomy.TryGetDimensionSlot("Life", out _), "names match exactly");
			Assert.IsFalse(taxonomy.TryGetDimensionSlot(null, out _));
		}

		[Test]
		public void Hooks_AreKept()
		{
			var namer = new FlatEventNameRules();
			var taxonomy = new AnalyticsTaxonomy(flatNamer: namer);

			Assert.AreSame(namer, taxonomy.FlatNamer);
			Assert.IsNull(taxonomy.AppsFlyerSelector);
		}

		[Test]
		public void AllowedValues_AreACopy()
		{
			var values = new[] { "a", "b" };
			var dimension = new AnalyticsDimension("letters", values);

			values[0] = "z";

			CollectionAssert.AreEqual(new[] { "a", "b" }, dimension.AllowedValues);
		}

		[Test]
		public void BadDimensions_AreRejected()
		{
			Assert.Throws<ArgumentException>(() => new AnalyticsDimension(""));
			Assert.Throws<ArgumentException>(() => new AnalyticsDimension("age", "age_1", null));
			Assert.Throws<ArgumentException>(() => new AnalyticsDimension("age", ""));
			Assert.Throws<ArgumentException>(() => new AnalyticsTaxonomy(new AnalyticsDimension[] { null }));
			Assert.Throws<ArgumentException>(() => new AnalyticsTaxonomy(new[]
			{
				new AnalyticsDimension("age"),
				new AnalyticsDimension("age")
			}));
		}
	}
}
