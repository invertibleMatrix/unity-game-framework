#if UGFW_FIREBASE
using System.Threading;
using AK.Services;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace AK.Tests
{
	/// <summary>Firebase's initialization runs once, however many callers wait for it.</summary>
	public class FirebaseInitializationServiceTests
	{
		[Test]
		public void Calls_ShareOneInitialization()
		{
			int runs = 0;
			var check = new UniTaskCompletionSource<bool>();
			var service = new FirebaseInitializationService(() =>
			{
				runs++;
				return check.Task;
			});

			UniTask<bool> first = service.InitializeAsync();
			UniTask<bool> second = service.InitializeAsync();

			Assert.AreEqual(1, runs);
			Assert.AreEqual(UniTaskStatus.Pending, first.Status);

			check.TrySetResult(true);

			Assert.IsTrue(first.GetAwaiter().GetResult());
			Assert.IsTrue(second.GetAwaiter().GetResult());
		}

		[Test]
		public void CancellingAWait_EndsOnlyThatWait()
		{
			int runs = 0;
			var check = new UniTaskCompletionSource<bool>();
			var service = new FirebaseInitializationService(() =>
			{
				runs++;
				return check.Task;
			});

			using var boot = new CancellationTokenSource();
			UniTask<bool> abandoned = service.InitializeAsync(boot.Token);
			boot.Cancel();

			Assert.AreEqual(UniTaskStatus.Canceled, abandoned.Status);

			UniTask<bool> retry = service.InitializeAsync();
			check.TrySetResult(true);

			Assert.AreEqual(1, runs, "the retry waits for the initialization the cancelled call started");
			Assert.IsTrue(retry.GetAwaiter().GetResult());
		}

		[Test]
		public void ACallAfterTheInitialization_GetsItsResult()
		{
			int runs = 0;
			var service = new FirebaseInitializationService(() =>
			{
				runs++;
				return UniTask.FromResult(false);
			});

			Assert.IsFalse(service.InitializeAsync().GetAwaiter().GetResult());

			using var token = new CancellationTokenSource();
			Assert.IsFalse(service.InitializeAsync(token.Token).GetAwaiter().GetResult());
			Assert.AreEqual(1, runs);
		}
	}
}
#endif
