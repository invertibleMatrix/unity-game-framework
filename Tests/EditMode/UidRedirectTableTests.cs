using AK.Core;
using NUnit.Framework;
using UnityEngine;
using AK.Tests.Support;

namespace AK.Tests
{
	public class UidRedirectTableTests
	{
		private UidRedirectTable _table;

		[SetUp]
		public void SetUp()
		{
			_table = ScriptableObject.CreateInstance<UidRedirectTable>();
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_table);
		}

		[Test]
		public void Follow_NoEntry_ReturnsInput()
		{
			Uid id = Uid.NewRandom();
			Assert.AreEqual(id, _table.Follow(id));
			Assert.IsFalse(_table.TryFollow(id, out Uid target));
			Assert.AreEqual(id, target);
			Assert.IsFalse(_table.Contains(id));
		}

		[Test]
		public void Follow_SingleHop()
		{
			Uid from = Uid.NewRandom();
			Uid to = Uid.NewRandom();
			_table.Editor_Add(from, to, "replaced");

			Assert.AreEqual(to, _table.Follow(from));
			Assert.IsTrue(_table.TryFollow(from, out Uid target));
			Assert.AreEqual(to, target);
			Assert.IsTrue(_table.Contains(from));
			Assert.IsFalse(_table.Contains(to));
		}

		[Test]
		public void Follow_IsTransitive()
		{
			Uid a = Uid.NewRandom(), b = Uid.NewRandom(), c = Uid.NewRandom();
			_table.Editor_Add(a, b, "1");
			_table.Editor_Add(b, c, "2");

			Assert.AreEqual(c, _table.Follow(a));
			Assert.AreEqual(c, _table.Follow(b));
			Assert.AreEqual(c, _table.Follow(c));
		}

		[Test]
		public void Follow_Cycle_TerminatesWithError()
		{
			Uid a = Uid.NewRandom(), b = Uid.NewRandom();
			_table.Editor_Add(a, b, "1");
			_table.Editor_Add(b, a, "2");

			Uid result;
			using (ExpectedLog.Error("exceeded"))
			{
				result = _table.Follow(a);
			}

			Assert.IsTrue(result == a || result == b);
		}

		[Test]
		public void Add_ReplacesExistingFrom()
		{
			Uid from = Uid.NewRandom(), first = Uid.NewRandom(), second = Uid.NewRandom();
			_table.Editor_Add(from, first, "1");
			_table.Editor_Add(from, second, "2");

			Assert.AreEqual(1, _table.Entries.Count);
			Assert.AreEqual(second, _table.Follow(from));
		}

		[Test]
		public void Remove_DropsEntry()
		{
			Uid from = Uid.NewRandom(), to = Uid.NewRandom();
			_table.Editor_Add(from, to, "1");

			Assert.IsTrue(_table.Editor_Remove(from));
			Assert.IsFalse(_table.Editor_Remove(from));
			Assert.AreEqual(from, _table.Follow(from));
		}

		[Test]
		public void SelfRedirect_AndNone_AreIgnored()
		{
			Uid id = Uid.NewRandom();
			_table.Editor_Add(id, id, "self");
			_table.Editor_Add(Uid.None, id, "from none");
			_table.Editor_Add(id, Uid.None, "to none");

			Assert.AreEqual(id, _table.Follow(id));
			Assert.IsFalse(_table.Contains(Uid.None));
		}
	}
}
