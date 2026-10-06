using System;
using AK.Kernel.Results;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests
{
	public class ResultTests
	{
		[Test]
		public void Ok_IsDefault_AndTruthy()
		{
			Result r = default;
			Assert.IsTrue(r.IsOk);
			Assert.IsFalse(r.IsFailed);
			Assert.AreEqual(Result.Ok, r);
			Assert.IsTrue(r);
			Assert.AreEqual(ErrorCode.None, r.Code);
			Assert.IsNull(r.Detail);
		}

		[Test]
		public void Fail_CarriesCodeAndDetail()
		{
			Result r = Result.Fail(ErrorCode.CannotAfford, "need 5 more");
			Assert.IsTrue(r.IsFailed);
			Assert.IsFalse(r);
			Assert.AreEqual(ErrorCode.CannotAfford, r.Code);
			Assert.AreEqual("need 5 more", r.Detail);
			StringAssert.Contains("CannotAfford", r.ToString());
		}

		[Test]
		public void Fail_WithNone_Throws()
		{
			Assert.Throws<ArgumentException>(() => Result.Fail(ErrorCode.None));
			Assert.Throws<ArgumentException>(() => Result<int>.Fail(ErrorCode.None));
		}

		[Test]
		public void GameCodes_MustStartAtReservedBlock()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => Result.Fail(42));
			Result r = Result.Fail((int)ErrorCode.GameDefined + 7, "custom");
			Assert.AreEqual((int)ErrorCode.GameDefined + 7, (int)r.Code);
		}

		[Test]
		public void Equality_IsByCode_NotDetail()
		{
			Assert.AreEqual(Result.Fail(ErrorCode.NotFound, "a"), Result.Fail(ErrorCode.NotFound, "b"));
			Assert.AreNotEqual(Result.Fail(ErrorCode.NotFound), Result.Fail(ErrorCode.Internal));
			Assert.IsTrue(Result.Fail(ErrorCode.NotFound) == Result.Fail(ErrorCode.NotFound));
			Assert.IsTrue(Result.Ok != Result.Fail(ErrorCode.NotFound));
		}

		[Test]
		public void Typed_Ok_CarriesValue()
		{
			Result<int> r = Result<int>.Ok(42);
			Assert.IsTrue(r.IsOk);
			Assert.IsTrue(r);
			Assert.AreEqual(42, r.Value);
			Assert.IsTrue(r.TryGet(out int v));
			Assert.AreEqual(42, v);
			Assert.AreEqual(42, r.Or(-1));
		}

		[Test]
		public void Typed_Fail_ValueIsDefault_NotThrow()
		{
			Result<string> r = Result<string>.Fail(ErrorCode.NotFound, "x");
			Assert.IsTrue(r.IsFailed);
			Assert.IsNull(r.Value);
			Assert.IsFalse(r.TryGet(out string v));
			Assert.IsNull(v);
			Assert.AreEqual("fallback", r.Or("fallback"));
			Assert.AreEqual(ErrorCode.NotFound, r.Untyped.Code);
		}

		[Test]
		public void Typed_ImplicitFromValue_ExplicitFromUntypedFailure()
		{
			Result<int> ok = 7;
			Assert.IsTrue(ok.IsOk);
			Assert.AreEqual(7, ok.Value);

			var failed = (Result<int>)Result.Fail(ErrorCode.Timeout, "slow");
			Assert.AreEqual(ErrorCode.Timeout, failed.Code);
			Assert.AreEqual("slow", failed.Detail);
		}

		[Test]
		public void Untyped_As_OnOk_Throws()
		{
			Assert.Throws<InvalidOperationException>(() => Result.Ok.As<int>());
			Assert.Throws<InvalidOperationException>(() => _ = (Result<int>)Result.Ok);
		}

		[Test]
		public void Typed_Equality_ComparesValueOnOk_CodeOnFail()
		{
			Assert.AreEqual(Result<int>.Ok(1), Result<int>.Ok(1));
			Assert.AreNotEqual(Result<int>.Ok(1), Result<int>.Ok(2));
			Assert.AreEqual(Result<int>.Fail(ErrorCode.NotFound, "a"), Result<int>.Fail(ErrorCode.NotFound, "b"));
			Assert.AreNotEqual(Result<int>.Ok(0), Result<int>.Fail(ErrorCode.NotFound));
		}

		[Test]
		public void HappyPath_DoesNotAllocate()
		{
			int Window() => GcAllocations.Count(() =>
			{
				for (int i = 0; i < 10_000; i++)
				{
					Result r = i % 2 == 0 ? Result.Ok : Result.Fail(ErrorCode.NotFound);
					Result<int> t = r.IsOk ? Result<int>.Ok(i) : Result<int>.Fail(ErrorCode.NotFound);
					if (t.TryGet(out int v) && v < 0) throw new Exception();
				}
			});

			Window();
			Assert.AreEqual(0, Window(), "Result construction and inspection allocated");
		}
	}
}
