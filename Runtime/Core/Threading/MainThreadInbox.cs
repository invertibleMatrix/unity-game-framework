using System;
using System.Collections.Concurrent;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Core.Threading
{
	/// <summary>
	/// Runs callbacks on the main thread, in the order they arrived, whichever thread they
	/// arrived on.
	///
	/// SDKs often raise some callbacks off the main thread (AppLovin MAX raises its ad revenue
	/// callbacks on a background thread by default). Posting every callback of such an SDK
	/// here keeps its state single-threaded, and keeps the callbacks in arrival order: a post
	/// from the main thread runs at once, after anything still queued, and a post from another
	/// thread runs at the next player-loop update.
	///
	/// A callback that throws is logged, and the ones after it still run.
	/// </summary>
	public sealed class MainThreadInbox
	{
		private readonly ConcurrentQueue<Action> _queue = new();
		private readonly Action _drain;
		private int _drainScheduled;
		private bool _draining;

		public MainThreadInbox()
		{
			_drain = Drain;
		}

		/// <summary>Callbacks posted and not run yet.</summary>
		public int Pending => _queue.Count;

		/// <summary>Queues <paramref name="callback"/>. On the main thread it runs before this returns.</summary>
		public void Post(Action callback)
		{
			if (callback == null)
			{
				throw new ArgumentNullException(nameof(callback));
			}

			_queue.Enqueue(callback);

			if (PlayerLoopHelper.IsMainThread)
			{
				Drain();
			}
			else if (Interlocked.CompareExchange(ref _drainScheduled, 1, 0) == 0)
			{
				PlayerLoopHelper.AddContinuation(PlayerLoopTiming.Update, _drain);
			}
		}

		/// <summary>
		/// Runs everything queued, on the calling thread, which must be the main thread. The
		/// player loop calls this for posts from other threads; call it directly to flush early.
		/// </summary>
		public void Drain()
		{
			// A callback that posts again lands in the queue; the loop below picks it up.
			if (_draining)
			{
				return;
			}

			_draining = true;

			// Cleared before the queue is read, so a post racing this drain schedules another
			// one rather than being stranded.
			Volatile.Write(ref _drainScheduled, 0);

			try
			{
				while (_queue.TryDequeue(out Action callback))
				{
					try
					{
						callback();
					}
					catch (Exception e)
					{
						Debug.LogException(e);
					}
				}
			}
			finally
			{
				_draining = false;
			}
		}
	}
}
