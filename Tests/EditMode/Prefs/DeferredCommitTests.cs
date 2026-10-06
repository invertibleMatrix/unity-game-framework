using System;
using AK.Core;
using NUnit.Framework;

namespace AK.Tests
{
	public class DeferredCommitTests
	{
		[Test]
		public void OutsideABatch_EachCommitRunsAtOnce()
		{
			int commits = 0;
			var commit = new DeferredCommit(() => commits++);

			commit.Commit();
			commit.Commit();

			Assert.AreEqual(2, commits);
		}

		[Test]
		public void NestedBatches_CommitOnce_AtTheOutermostResume()
		{
			int commits = 0;
			var commit = new DeferredCommit(() => commits++);

			commit.Suspend();
			commit.Commit();
			commit.Suspend();
			commit.Commit();
			commit.Resume();

			Assert.AreEqual(0, commits, "the inner resume doesn't commit");

			commit.Resume();

			Assert.AreEqual(1, commits);
		}

		[Test]
		public void ABatchWithoutWrites_DoesntCommit()
		{
			int commits = 0;
			var commit = new DeferredCommit(() => commits++);

			commit.Suspend();
			commit.Resume();

			Assert.AreEqual(0, commits);
		}

		[Test]
		public void ACommitThatFailsAtResume_StaysPending_AndTheNextBatchRetriesIt()
		{
			int  attempts = 0;
			bool fail     = true;
			var commit = new DeferredCommit(() =>
			{
				attempts++;
				if (fail) throw new InvalidOperationException("disk full");
			});

			commit.Suspend();
			commit.Commit();
			Assert.Throws<InvalidOperationException>(() => commit.Resume());

			fail = false;
			commit.Suspend();
			commit.Resume();

			Assert.AreEqual(2, attempts, "the failed batch's writes were committed by the next one");

			commit.Suspend();
			commit.Resume();

			Assert.AreEqual(2, attempts, "nothing is pending after a success");
		}

		[Test]
		public void ResumeWithoutSuspend_Throws()
		{
			var commit = new DeferredCommit(() => { });

			Assert.Throws<InvalidOperationException>(() => commit.Resume());
		}
	}
}
