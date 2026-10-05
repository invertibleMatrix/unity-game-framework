using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AK.Core.Threading;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Threading
{
	public class MainThreadInboxTests
	{
		[Test]
		public void APostFromTheMainThread_RunsBeforePostReturns()
		{
			var inbox = new MainThreadInbox();
			bool ran = false;

			inbox.Post(() => ran = true);

			Assert.IsTrue(ran);
			Assert.AreEqual(0, inbox.Pending);
		}

		[Test]
		public void PostsFromAnotherThread_WaitForTheMainThread_AndKeepTheirOrder()
		{
			var inbox = new MainThreadInbox();
			var order = new List<int>();

			Task.Run(() =>
			{
				for (int i = 0; i < 3; i++)
				{
					int n = i;
					inbox.Post(() => order.Add(n));
				}
			}).Wait();

			Assert.AreEqual(3, inbox.Pending, "nothing runs off the main thread");
			Assert.IsEmpty(order);

			inbox.Post(() => order.Add(3));

			CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, order, "a main-thread post runs after what was queued before it");
		}

		[Test]
		public void Drain_RunsWhatOtherThreadsPosted()
		{
			var inbox = new MainThreadInbox();
			int ran = 0;
			Task.Run(() => inbox.Post(() => ran++)).Wait();

			inbox.Drain();

			Assert.AreEqual(1, ran);
			Assert.AreEqual(0, inbox.Pending);
		}

		[Test]
		public void ACallbackThatThrows_IsLogged_AndTheRestStillRun()
		{
			var inbox = new MainThreadInbox();
			var order = new List<string>();
			Task.Run(() =>
			{
				inbox.Post(() => order.Add("a"));
				inbox.Post(() => throw new InvalidOperationException("callback failed"));
				inbox.Post(() => order.Add("c"));
			}).Wait();

			using (ExpectedLog.Exception("callback failed"))
			{
				inbox.Drain();
			}

			CollectionAssert.AreEqual(new[] { "a", "c" }, order);
		}

		[Test]
		public void ACallbackThatPosts_RunsThatPostAfterItself()
		{
			var inbox = new MainThreadInbox();
			var order = new List<string>();

			inbox.Post(() =>
			{
				inbox.Post(() => order.Add("inner"));
				order.Add("outer");
			});

			CollectionAssert.AreEqual(new[] { "outer", "inner" }, order);
		}

		[Test]
		public void Post_RejectsNull()
		{
			Assert.Throws<ArgumentNullException>(() => new MainThreadInbox().Post(null));
		}
	}
}
