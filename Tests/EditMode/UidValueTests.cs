using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Facts;
using NUnit.Framework;
using UnityEngine;

namespace AK.Tests
{
	public class UidValueTests
	{
		[Test]
		public void Default_IsNone()
		{
			Uid id = default;
			Assert.IsTrue(id.IsNone);
			Assert.IsFalse(id.IsSet);
			Assert.AreEqual(Uid.None, id);
			Assert.AreEqual(new string('0', 32), id.ToString());
		}

		[Test]
		public void NewRandom_IsSet_AndUnique()
		{
			var seen = new HashSet<Uid>();
			for (int i = 0; i < 10_000; i++)
			{
				Uid id = Uid.NewRandom();
				Assert.IsTrue(id.IsSet);
				Assert.IsTrue(seen.Add(id), "random identities collided");
			}
		}

		[Test]
		public void NewRandom_HasVersion4Bits()
		{
			Guid g = Uid.NewRandom().ToGuid();
			byte[] bytes = g.ToByteArray();
			Assert.AreEqual(0x40, bytes[7] & 0xF0, "version nibble");
			Assert.AreEqual(0x80, bytes[8] & 0xC0, "variant bits");
		}

		[Test]
		public void Deterministic_SameInputs_SameIdentity()
		{
			Uid ns = Uid.Parse("6f2a9c4e1b3d4a7f9e8c2b1d5a6f7e8c");
			Uid a = Uid.Deterministic(ns, "SoftCurrency");
			Uid b = Uid.Deterministic(ns, "SoftCurrency");
			Assert.AreEqual(a, b);
			Assert.IsTrue(a.IsSet);
		}

		[Test]
		public void Deterministic_DifferentNameOrNamespace_Differs()
		{
			Uid ns1 = Uid.Parse("6f2a9c4e1b3d4a7f9e8c2b1d5a6f7e8c");
			Uid ns2 = Uid.Parse("7f2a9c4e1b3d4a7f9e8c2b1d5a6f7e8c");
			Assert.AreNotEqual(Uid.Deterministic(ns1, "A"), Uid.Deterministic(ns1, "B"));
			Assert.AreNotEqual(Uid.Deterministic(ns1, "A"), Uid.Deterministic(ns2, "A"));
		}

		[Test]
		public void Deterministic_HasVersion5Bits()
		{
			Uid ns = Uid.NewRandom();
			byte[] bytes = Uid.Deterministic(ns, "x").ToGuid().ToByteArray();
			Assert.AreEqual(0x50, bytes[7] & 0xF0);
			Assert.AreEqual(0x80, bytes[8] & 0xC0);
		}

		[Test]
		public void Deterministic_EmptyName_Throws()
		{
			Assert.Throws<ArgumentException>(() => Uid.Deterministic(Uid.NewRandom(), string.Empty));
			Assert.Throws<ArgumentException>(() => Uid.Deterministic(Uid.NewRandom(), null));
		}

		[TestCase("0123456789abcdef0123456789abcdef")]
		[TestCase("0123456789ABCDEF0123456789ABCDEF")]
		[TestCase("01234567-89ab-cdef-0123-456789abcdef")]
		[TestCase("{01234567-89ab-cdef-0123-456789abcdef}")]
		[TestCase("(0123456789abcdef0123456789abcdef)")]
		[TestCase("  0123456789abcdef0123456789abcdef  ")]
		public void TryParse_AcceptsCanonicalForms(string text)
		{
			Assert.IsTrue(Uid.TryParse(text, out Uid id));
			Assert.AreEqual("0123456789abcdef0123456789abcdef", id.ToString());
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("0123456789abcdef0123456789abcde")]
		[TestCase("0123456789abcdef0123456789abcdef0")]
		[TestCase("0123456789abcdef0123456789abcdeg")]
		[TestCase("not-a-uid")]
		public void TryParse_RejectsInvalid(string text)
		{
			Assert.IsFalse(Uid.TryParse(text, out Uid id));
			Assert.IsTrue(id.IsNone);
		}

		[Test]
		public void Parse_Invalid_Throws()
		{
			Assert.Throws<FormatException>(() => Uid.Parse("nope"));
		}

		[Test]
		public void ToString_RoundTrips()
		{
			for (int i = 0; i < 1000; i++)
			{
				Uid id = Uid.NewRandom();
				Assert.AreEqual(id, Uid.Parse(id.ToString()));
				Assert.AreEqual(id, Uid.Parse(id.ToDashedString()));
			}
		}

		[Test]
		public void Guid_RoundTrips()
		{
			var guid = Guid.NewGuid();
			Uid id = Uid.FromGuid(guid);
			Assert.AreEqual(guid, id.ToGuid());
			Assert.AreEqual(guid.ToString("N"), id.ToString());
		}

		[Test]
		public void Bytes_AreNetworkOrder_AndMatchText()
		{
			Uid id = Uid.Parse("0123456789abcdef0123456789abcdef");
			var bytes = new byte[Uid.ByteLength];
			id.WriteBytes(bytes);

			Assert.AreEqual(0x01, bytes[0]);
			Assert.AreEqual(0x23, bytes[1]);
			Assert.AreEqual(0xef, bytes[7]);
			Assert.AreEqual(0x01, bytes[8]);
			Assert.AreEqual(0xef, bytes[15]);
			Assert.AreEqual(id, Uid.FromBytes(bytes));
			Assert.AreEqual(0x0123456789abcdefUL, id.Hi);
			Assert.AreEqual(0x0123456789abcdefUL, id.Lo);
		}

		[Test]
		public void Serialize_Then_Deserialize_PreservesText()
		{
			const string text = "c0d454b46d6640d5b270d19d61817d79";
			var holder = new Holder { Plain = Uid.Parse(text) };

			string json = JsonUtility.ToJson(holder);
			StringAssert.Contains(text, json);

			var back = JsonUtility.FromJson<Holder>(json);
			Assert.AreEqual(text, back.Plain.ToString());
			Assert.AreEqual(text, JsonUtility.FromJson<Holder>(JsonUtility.ToJson(back)).Plain.ToString());
		}

		[Test]
		public void Equality_IsByValue()
		{
			Uid a = Uid.Parse("0123456789abcdef0123456789abcdef");
			Uid b = Uid.Parse("01234567-89AB-CDEF-0123-456789ABCDEF");
			Assert.IsTrue(a == b);
			Assert.IsFalse(a != b);
			Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
			Assert.IsTrue(a.Equals((object)b));
		}

		[Test]
		public void CompareTo_IsTotalOrder()
		{
			var ids = new List<Uid>();
			for (int i = 0; i < 200; i++) ids.Add(Uid.NewRandom());
			ids.Sort();
			for (int i = 1; i < ids.Count; i++)
			{
				Assert.LessOrEqual(ids[i - 1].CompareTo(ids[i]), 0);
			}
		}

		[Test]
		public void ToShortString_IsPrefixOfCanonical()
		{
			Uid id = Uid.NewRandom();
			Assert.AreEqual(8, id.ToShortString().Length);
			Assert.IsTrue(id.ToString().StartsWith(id.ToShortString()));
		}

		[Test]
		public void HashCode_DistributesAcrossBuckets()
		{
			// 5000 uniform draws into 4096 buckets occupy ~2888 of them (4096 · (1 − e^(−5000/4096))).
			var buckets = new HashSet<int>();
			for (int i = 0; i < 5000; i++) buckets.Add(Uid.NewRandom().GetHashCode() & 0xFFF);
			Assert.Greater(buckets.Count, 2700, "hash codes collapsed into too few buckets");
		}

		[Serializable]
		private class Holder
		{
			public Uid Plain;
			public Uid<FactType> Typed;
			public List<Uid> Many = new();
		}

		[Test]
		public void JsonUtility_RoundTrips_AsHexStrings()
		{
			var holder = new Holder { Plain = Uid.NewRandom(), Typed = new Uid<FactType>(Uid.NewRandom()) };
			holder.Many.Add(Uid.NewRandom());
			holder.Many.Add(Uid.None);

			string json = JsonUtility.ToJson(holder);
			StringAssert.Contains(holder.Plain.ToString(), json);
			StringAssert.Contains(holder.Typed.ToString(), json);
			StringAssert.DoesNotContain("_hi", json);
			StringAssert.DoesNotContain("_lo", json);

			var back = JsonUtility.FromJson<Holder>(json);
			Assert.AreEqual(holder.Plain, back.Plain);
			Assert.AreEqual(holder.Typed, back.Typed);
			Assert.AreEqual(2, back.Many.Count);
			Assert.AreEqual(holder.Many[0], back.Many[0]);
			Assert.IsTrue(back.Many[1].IsNone);
		}

		[Test]
		public void JsonUtility_None_SerializesAsEmptyString()
		{
			string json = JsonUtility.ToJson(new Holder());
			StringAssert.Contains("\"Plain\":{\"_value\":\"\"}", json);
		}

		[Test]
		public void JsonUtility_GarbageString_DeserializesToNone()
		{
			var back = JsonUtility.FromJson<Holder>("{\"Plain\":{\"_value\":\"garbage\"},\"Typed\":{\"_value\":{\"_value\":\"\"}},\"Many\":[]}");
			Assert.IsTrue(back.Plain.IsNone);
			Assert.IsTrue(back.Typed.IsNone);
		}

		[Test]
		public void Typed_SharesBytesWithUntyped()
		{
			Uid raw = Uid.NewRandom();
			var typed = new Uid<FactType>(raw);
			Assert.AreEqual(raw, typed.Value);
			Assert.AreEqual(raw, typed.Untyped);
			Assert.AreEqual(raw.GetHashCode(), typed.GetHashCode());
			Assert.AreEqual(raw.ToString(), typed.ToString());
			Assert.AreEqual(typed, Uid<FactType>.From(raw));
			Assert.AreEqual(raw, typed.As<MetaDataAsset>().Value);
		}

		[Test]
		public void Typed_Default_IsNone()
		{
			Uid<FactType> none = default;
			Assert.IsTrue(none.IsNone);
			Assert.AreEqual(Uid<FactType>.None, none);
		}

		[Test]
		public void Asset_IdAs_ReturnsSameIdentity()
		{
			var asset = ScriptableObject.CreateInstance<FactType>();
			try
			{
				Assert.IsFalse(asset.HasIdentity);
				Assert.IsTrue(asset.IdAs<FactType>().IsNone);

				Uid minted = Uid.NewRandom();
				asset.Editor_AssignIdentity(minted, UidProvenance.Minted, string.Empty);

				Assert.IsTrue(asset.HasIdentity);
				Assert.AreEqual(minted, asset.Id);
				Assert.AreEqual(minted, asset.IdAs<FactType>().Value);
				Assert.AreEqual(UidProvenance.Minted, asset.Provenance);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(asset);
			}
		}
	}
}
