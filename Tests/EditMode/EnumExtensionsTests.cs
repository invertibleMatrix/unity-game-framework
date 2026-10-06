using System;
using AK.Core.Extensions;
using NUnit.Framework;

namespace AK.Tests
{
	public class EnumExtensionsTests
	{
		private enum Fruit { Apple = 1, Banana = 2, Cherry = 3 }

		[Flags]
		private enum Sides { None = 0, Left = 1, Right = 2 }

		// Names that differ only in case.
		private enum Cased { Value = 1, VALUE = 2 }

		// A value with two names.
		private enum Aliased { First = 1, Second = 1, Third = 2 }

		[TestCase("banana")]
		[TestCase("BANANA")]
		[TestCase("Banana")]
		public void TryGetEnum_MatchesANameInAnyCase(string text)
		{
			Assert.IsTrue(text.TryGetEnum(out Fruit fruit));
			Assert.AreEqual(Fruit.Banana, fruit);
		}

		[Test]
		public void TryGetEnum_ParsesNumbersAndFlags()
		{
			Assert.IsTrue("3".TryGetEnum(out Fruit fruit));
			Assert.AreEqual(Fruit.Cherry, fruit);

			Assert.IsTrue("left, RIGHT".TryGetEnum(out Sides sides));
			Assert.AreEqual(Sides.Left | Sides.Right, sides);
		}

		[Test]
		public void TryGetEnum_RefusesAnUnknownNameAndNull()
		{
			Assert.IsFalse("durian".TryGetEnum(out Fruit _));
			Assert.IsFalse(((string)null).TryGetEnum(out Fruit fruit));
			Assert.AreEqual(default(Fruit), fruit);
		}

		[Test]
		public void GetEnum_ThrowsForAnUnknownNameAndNull()
		{
			Assert.AreEqual(Fruit.Apple, "apple".GetEnum<Fruit>());
			Assert.Throws<ArgumentException>(() => "durian".GetEnum<Fruit>());
			Assert.Throws<ArgumentNullException>(() => ((string)null).GetEnum<Fruit>());
		}

		[Test]
		public void NamesThatDifferOnlyInCase_MatchAsEnumTryParseIgnoringCaseDoes()
		{
			Assert.IsTrue(Enum.TryParse("value", true, out Cased expected));

			Assert.IsTrue("value".TryGetEnum(out Cased cased));
			Assert.AreEqual(expected, cased);
			Assert.AreEqual("VALUE", Cased.VALUE.GetName());
			Assert.AreEqual("value", Cased.VALUE.GetLowerName());
		}

		[Test]
		public void AValueWithTwoNames_GoesByTheSameNameInBothCases()
		{
			string name = Aliased.First.GetName();

			Assert.That(name, Is.EqualTo("First").Or.EqualTo("Second"));
			Assert.AreEqual(name.ToLowerInvariant(), Aliased.Second.GetLowerName());
			CollectionAssert.AreEquivalent(new[] { name, "Third" }, EnumExtensions.GetNames<Aliased>());
		}

		[Test]
		public void AValueWithoutAName_GoesByItsNumberOrItsFlags()
		{
			Assert.AreEqual("7", ((Fruit)7).GetName());
			Assert.AreEqual("Left, Right", (Sides.Left | Sides.Right).GetName());
			Assert.AreEqual("left, right", (Sides.Left | Sides.Right).GetLowerName());
		}

		[Test]
		public void GetNames_ListsTheNames()
		{
			CollectionAssert.AreEquivalent(new[] { "Apple", "Banana", "Cherry" }, EnumExtensions.GetNames<Fruit>());
			Assert.AreEqual("cherry", Fruit.Cherry.GetLowerName());
		}
	}
}
