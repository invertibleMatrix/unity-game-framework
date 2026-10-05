#if UGFW_ADDRESSABLES
using System;
using System.Collections.Generic;
using AK.Core.ResourceManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace AK.Tests.ResourceManagement
{
	/// <summary>
	/// Synchronous loads through the Addressables strategy, on operations the tests start and finish
	/// themselves. Where nothing can block (WebGL), a load that isn't done is refused: the strategy
	/// releases its handle, and the operation is destroyed when it finishes.
	/// </summary>
	public sealed class AddressablesSyncLoadTests
	{
		// Pending until Finish. Waiting on it finishes it, as waiting on a real load does.
		private sealed class PendingOp<T> : AsyncOperationBase<T>
		{
			private readonly T _result;
			private bool       _finished;

			public PendingOp(T result) => _result = result;

			public void Finish()
			{
				if (_finished) return;
				_finished = true;
				Complete(_result, true, null);
			}

			protected override void Execute()
			{
			}

			protected override bool InvokeWaitForCompletion()
			{
				Finish();
				return true;
			}
		}

		private ResourceManager _resources;
		private Texture2D       _asset;

		[SetUp]
		public void SetUp()
		{
			_resources = new ResourceManager();
			_asset     = new Texture2D(1, 1) { name = "SyncLoadAsset" };
		}

		[TearDown]
		public void TearDown()
		{
			_resources.Dispose();
			Object.DestroyImmediate(_asset);
		}

		private AsyncOperationHandle<T> Start<T>(PendingOp<T> op) => _resources.StartOperation(op, default);

		[Test]
		public void CantBlock_PendingLoad_IsRefused_AndDestroyedWhenItFinishes()
		{
			var strategy = new AddressablesLoadingStrategy(canBlock: false);
			var op       = new PendingOp<Texture2D>(_asset);
			var handle   = Start(op);

			Assert.IsFalse(strategy.TryWaitAndTrack(handle, out Texture2D result));
			Assert.IsNull(result);
			Assert.AreEqual(0, strategy.ClaimCount);
			Assert.IsTrue(handle.IsValid(), "the load keeps running");

			op.Finish();
			Assert.IsFalse(handle.IsValid(), "nothing holds it once it finishes");
		}

		[Test]
		public void CantBlock_LoadThatIsDone_IsClaimed_LikeAnyLoad()
		{
			var strategy = new AddressablesLoadingStrategy(canBlock: false);
			var handle   = _resources.CreateCompletedOperation(_asset, null);

			Assert.IsTrue(strategy.TryWaitAndTrack(handle, out Texture2D result));
			Assert.AreSame(_asset, result);
			Assert.AreEqual(1, strategy.ClaimsOf(_asset));

			strategy.DisposeAsset(_asset);
			Assert.AreEqual(0, strategy.ClaimCount);
			Assert.IsFalse(handle.IsValid(), "the dispose released the load");
		}

		[Test]
		public void CantBlock_LoadAsset_ThrowsNotSupported_NamingTheKey()
		{
			var strategy = new AddressablesLoadingStrategy(canBlock: false);
			var op       = new PendingOp<Texture2D>(_asset);
			var handle   = Start(op);

			var error = Assert.Throws<NotSupportedException>(() => strategy.WaitAndTrack(handle, "Art/Hero"));
			StringAssert.Contains("'Art/Hero'", error.Message);
			Assert.AreEqual(0, strategy.ClaimCount);

			op.Finish();
			Assert.IsFalse(handle.IsValid());
		}

		[Test]
		public void CantBlock_PendingListLoad_IsRefused_AndDestroyedWhenItFinishes()
		{
			var strategy = new AddressablesLoadingStrategy(canBlock: false);
			var op       = new PendingOp<IList<Texture2D>>(new[] { _asset });
			var handle   = Start(op);

			Assert.IsFalse(strategy.TryWaitAndTrackList(handle, out IList<Texture2D> result));
			Assert.IsNull(result);
			Assert.AreEqual(0, strategy.ClaimCount);

			op.Finish();
			Assert.IsFalse(handle.IsValid());
		}

		[Test]
		public void CanBlock_PendingLoad_IsWaitedFor_AndClaimed()
		{
			var strategy = new AddressablesLoadingStrategy(canBlock: true);
			var handle   = Start(new PendingOp<Texture2D>(_asset));

			Assert.IsTrue(strategy.TryWaitAndTrack(handle, out Texture2D result));
			Assert.AreSame(_asset, result);
			Assert.AreEqual(1, strategy.ClaimsOf(_asset));

			strategy.DisposeAsset(_asset);
			Assert.IsFalse(handle.IsValid());
		}

		[Test]
		public void CanBlock_PendingListLoad_IsWaitedFor_AndClaimedOnItsFirstElement()
		{
			var strategy = new AddressablesLoadingStrategy(canBlock: true);
			var handle   = Start(new PendingOp<IList<Texture2D>>(new[] { _asset }));

			Assert.IsTrue(strategy.TryWaitAndTrackList(handle, out IList<Texture2D> result));
			Assert.AreEqual(1, result.Count);
			Assert.AreEqual(1, strategy.ClaimsOf(_asset));

			strategy.DisposeAsset(_asset);
			Assert.IsFalse(handle.IsValid());
		}
	}
}
#endif
