using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using AK.Core.Collections;
using Unity.Profiling;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace AK.Jobs
{
	/// <summary>
	/// Lock-free frame-synchronous job system. See <see cref="IJobScheduler"/> for the contract.
	///
	/// Ownership, not locking, keeps the threads apart. Every buffer has one owner at any instant and
	/// ownership changes hands only inside <see cref="Tick"/>, after the main thread has observed that
	/// every worker finished the previous frame. From the kick until that observation, workers read
	/// the frozen executing buffers, the chunk list and the frame context, and write only their own
	/// job data; the main thread appends to the pending buffers and touches nothing the workers read.
	/// The two events are release/acquire pairs, so no other fences are needed. The only atomics are
	/// the chunk cursor, the per-phase chunk counters and the remaining-workers counter.
	///
	/// A frame whose workers are still busy at the next barrier is skipped — no swap, no kick — and
	/// pending work simply waits one more frame. Nothing is ever reset while a worker may be running.
	/// </summary>
	public sealed partial class JobScheduler : IJobScheduler
	{
		private const int JoinTimeoutMs = 2000;

		private static readonly ProfilerMarker TickMarker   = new("AK.Jobs.Tick");
		private static readonly ProfilerMarker WorkerMarker = new("AK.Jobs.Worker");

		private readonly JobSchedulerOptions _options;
		private readonly int                 _mainThreadId;
		private readonly SlotMap<JobSlot>    _slots = new(64);
		private readonly PhaseLane[]         _lanes;

		// Frozen for the duration of a frame: written by main before the kick, read by workers after it.
		private Chunk[]      _chunks = new Chunk[64];
		private int          _chunkCount;
		private FrameContext _ctx;
		private readonly int[] _phaseRemaining;

		// Shared during a frame through atomics only.
		private int           _nextChunk;
		private int           _remainingWorkers;
		private int           _faultedThisFrame;
		private volatile int  _openPhase;
		private volatile bool _stopping;

		private readonly Thread[]               _workers;
		private readonly ManualResetEventSlim[] _kicks;
		private readonly ManualResetEventSlim   _done = new(false, 0);
		private readonly long[]                 _workerTicks;

		// Main-thread state.
		private bool _busy;
		private bool _disposed;
		private int  _handOffGen;
		private int  _inFlightJobs, _inFlightChunks;
		private int  _skippedFrames, _lastFrameJobs, _lastFrameChunks, _lastFrameFaults;
		private long _lastFrameWorkerTicks, _lastFrameCriticalTicks;

		public JobScheduler(JobSchedulerOptions options = null)
		{
			_options = options ?? new JobSchedulerOptions();
			_options.Validate();

			_mainThreadId = Thread.CurrentThread.ManagedThreadId;

			_lanes = new PhaseLane[_options.PhaseCount];
			for (int i = 0; i < _lanes.Length; i++) _lanes[i] = new PhaseLane();
			_phaseRemaining = new int[_options.PhaseCount];

			int n = _options.WorkerCount;
			_workers     = new Thread[n];
			_kicks       = new ManualResetEventSlim[n];
			_workerTicks = new long[n];

			for (int i = 0; i < n; i++)
			{
				_kicks[i]   = new ManualResetEventSlim(false, _options.SpinCount);
				_workers[i] = new Thread(WorkerMain)
				{
					IsBackground = true,
					Name         = $"AK.Jobs Worker {i}",
				};
				_workers[i].Start(i);
			}
		}

		public bool IsIdle => !_busy || _done.IsSet;

		public JobSchedulerStats Stats =>
			new(_workers.Length, _lastFrameJobs, _lastFrameChunks, _lastFrameFaults, _skippedFrames,
			    _lastFrameWorkerTicks, _lastFrameCriticalTicks);

		internal int WorkerCount => _workers.Length;

		/// <summary>The frozen chunk list of the frame in flight (or the last one). Main thread only.</summary>
		internal ReadOnlySpan<Chunk> Chunks => new(_chunks, 0, _chunkCount);

		internal bool AnyWorkerAlive
		{
			get
			{
				for (int i = 0; i < _workers.Length; i++)
				{
					if (_workers[i].IsAlive) return true;
				}

				return false;
			}
		}

		// ---------------------------------------------------------------- scheduling (main thread)

		public JobHandle Schedule(IJob job, int phase = 0)
		{
			ThrowIfNotMainThread();
			ThrowIfDisposed();
			if (job == null) throw new ArgumentNullException(nameof(job));
			ValidatePhase(phase);

			Handle<JobSlot> handle = _slots.Add(new JobSlot
			{
				Job          = job,
				Callback     = job as IJobCallback,
				Kind         = JobKind.OneShot,
				Phase        = (byte)phase,
				ScheduledGen = _handOffGen,
			});

			PhaseLane lane = _lanes[phase];
			lane.PendingJobs.Add(job);
			lane.PendingHandles.Add(handle);

			return new JobHandle(handle);
		}

		public JobHandle ScheduleRepeating(IJob job, int phase = 0)
		{
			ThrowIfNotMainThread();
			ThrowIfDisposed();
			if (job == null) throw new ArgumentNullException(nameof(job));
			ValidatePhase(phase);

			Handle<JobSlot> handle = _slots.Add(new JobSlot
			{
				Job          = job,
				Callback     = job as IJobCallback,
				Kind         = JobKind.Repeating,
				Phase        = (byte)phase,
				ScheduledGen = _handOffGen,
			});

			PhaseLane lane = _lanes[phase];
			lane.StagedRepeatingJobs.Add(job);
			lane.StagedRepeatingHandles.Add(handle);

			return new JobHandle(handle);
		}

		public bool Cancel(JobHandle handle)
		{
			ThrowIfNotMainThread();

			if (!_slots.Contains(handle.Slot)) return false;

			ref JobSlot slot = ref _slots.GetRefUnchecked(handle.Slot.Index);
			if (slot.Cancelled) return false;

			slot.Cancelled = true;

			PhaseLane lane = _lanes[slot.Phase];
			if (slot.Kind == JobKind.OneShot)
			{
				if (slot.ScheduledGen == _handOffGen) lane.CancelledBeforeHandOff++;
			}
			else
			{
				lane.RepeatingCancelled++;
			}

			return true;
		}

		public bool IsPending(JobHandle handle)
		{
			ThrowIfNotMainThread();
			return _slots.Contains(handle.Slot) && !_slots.GetRefUnchecked(handle.Slot.Index).Cancelled;
		}

		public void Register<TJob>(JobBatch<TJob> batch) where TJob : struct, IJob
		{
			ThrowIfNotMainThread();
			ThrowIfDisposed();
			if (batch == null) throw new ArgumentNullException(nameof(batch));

			IJobBatch b = batch;
			if (b.Owner == this) return;
			if (b.Owner != null) throw new InvalidOperationException("batch is registered with another scheduler");
			ValidatePhase(b.Phase);

			b.Owner = this;
			_lanes[b.Phase].Batches.Add(b);
		}

		/// <summary>
		/// Stops rotating the batch. A frame already in flight still finishes executing it, so wait for
		/// idle before discarding a batch that was unregistered mid-frame.
		/// </summary>
		public void Unregister<TJob>(JobBatch<TJob> batch) where TJob : struct, IJob
		{
			ThrowIfNotMainThread();
			if (batch == null) throw new ArgumentNullException(nameof(batch));

			IJobBatch b = batch;
			if (b.Owner != this) return;

			_lanes[b.Phase].Batches.Remove(b);
			b.Owner = null;
		}

		public void WaitForIdle()
		{
			ThrowIfNotMainThread();
			if (_busy) _done.Wait();
		}

		// ---------------------------------------------------------------- barrier (main thread)

		/// <summary>
		/// The barrier. Collects the previous frame if the workers are done with it, hands the pending
		/// work over, and kicks the workers. Called once per frame by the driver; tests call it directly.
		/// </summary>
		internal void Tick(in FrameContext ctx)
		{
			ThrowIfNotMainThread();
			ThrowIfDisposed();

			using ProfilerMarker.AutoScope scope = TickMarker.Auto();

			if (_busy)
			{
				if (!_done.IsSet)
				{
					_skippedFrames++;
					return;
				}

				_busy = false;
				Collect();
			}

			HandOff();
			BuildChunks();

			_inFlightChunks = _chunkCount;
			if (_chunkCount == 0) return;

			_ctx              = ctx;
			_nextChunk        = 0;
			_faultedThisFrame = 0;
			_remainingWorkers = _workers.Length;
			_openPhase        = FirstNonEmptyPhase();

			_done.Reset();
			for (int i = 0; i < _kicks.Length; i++) _kicks[i].Set();
			_busy = true;
		}

		/// <summary>Delivers completions for the frame the workers just finished and frees their slots.</summary>
		private void Collect()
		{
			long sum = 0, max = 0;
			for (int i = 0; i < _workerTicks.Length; i++)
			{
				long t = _workerTicks[i];
				sum += t;
				if (t > max) max = t;
			}

			_lastFrameWorkerTicks   = sum;
			_lastFrameCriticalTicks = max;
			_lastFrameFaults        = _faultedThisFrame;
			_lastFrameJobs          = _inFlightJobs;
			_lastFrameChunks        = _inFlightChunks;

			for (int p = 0; p < _lanes.Length; p++)
			{
				PhaseLane                 lane    = _lanes[p];
				JobArray<Handle<JobSlot>> handles = lane.ExecutingHandles;

				for (int i = 0; i < handles.Count; i++)
				{
					Handle<JobSlot> handle    = handles.Items[i];
					ref JobSlot     slot      = ref _slots.GetRefUnchecked(handle.Index);
					IJobCallback    callback  = slot.Callback;
					bool            cancelled = slot.Cancelled;

					_slots.Remove(handle);
					SafeComplete(callback, cancelled);
				}

				lane.ExecutingJobs.Clear();
				handles.Clear();
			}
		}

		/// <summary>Pending becomes executing. O(1) unless something was cancelled while pending.</summary>
		private void HandOff()
		{
			int jobs = 0;

			for (int p = 0; p < _lanes.Length; p++)
			{
				PhaseLane lane = _lanes[p];
				lane.Swap();

				if (lane.CancelledBeforeHandOff > 0) CompactCancelled(lane);

				jobs += lane.ExecutingJobs.Count;

				MergeStagedRepeating(lane);
				if (lane.RepeatingCancelled > 0) PurgeCancelledRepeating(lane);

				jobs += lane.RepeatingJobs.Count;

				List<IJobBatch> batches = lane.Batches;
				for (int i = 0; i < batches.Count; i++)
				{
					batches[i].Rotate();
					jobs += batches[i].ExecutingCount;
				}
			}

			_handOffGen++;
			_inFlightJobs = jobs;
		}

		private void CompactCancelled(PhaseLane lane)
		{
			JobArray<IJob>            jobs    = lane.ExecutingJobs;
			JobArray<Handle<JobSlot>> handles = lane.ExecutingHandles;
			int                       write   = 0;

			for (int read = 0; read < handles.Count; read++)
			{
				Handle<JobSlot> handle = handles.Items[read];
				ref JobSlot     slot   = ref _slots.GetRefUnchecked(handle.Index);

				if (slot.Cancelled)
				{
					IJobCallback callback = slot.Callback;
					_slots.Remove(handle);
					SafeComplete(callback, cancelled: true);
					continue;
				}

				if (write != read)
				{
					handles.Items[write] = handle;
					jobs.Items[write]    = jobs.Items[read];
				}

				write++;
			}

			jobs.Truncate(write);
			handles.Truncate(write);
			lane.CancelledBeforeHandOff = 0;
		}

		private static void MergeStagedRepeating(PhaseLane lane)
		{
			JobArray<IJob>            stagedJobs    = lane.StagedRepeatingJobs;
			JobArray<Handle<JobSlot>> stagedHandles = lane.StagedRepeatingHandles;
			if (stagedJobs.Count == 0) return;

			for (int i = 0; i < stagedJobs.Count; i++)
			{
				lane.RepeatingJobs.Add(stagedJobs.Items[i]);
				lane.RepeatingHandles.Add(stagedHandles.Items[i]);
			}

			stagedJobs.Clear();
			stagedHandles.Clear();
		}

		private void PurgeCancelledRepeating(PhaseLane lane)
		{
			JobArray<IJob>            jobs    = lane.RepeatingJobs;
			JobArray<Handle<JobSlot>> handles = lane.RepeatingHandles;
			int                       write   = 0;

			for (int read = 0; read < handles.Count; read++)
			{
				Handle<JobSlot> handle = handles.Items[read];
				ref JobSlot     slot   = ref _slots.GetRefUnchecked(handle.Index);

				if (slot.Cancelled)
				{
					IJobCallback callback = slot.Callback;
					_slots.Remove(handle);
					SafeComplete(callback, cancelled: true);
					continue;
				}

				if (write != read)
				{
					handles.Items[write] = handle;
					jobs.Items[write]    = jobs.Items[read];
				}

				write++;
			}

			jobs.Truncate(write);
			handles.Truncate(write);
			lane.RepeatingCancelled = 0;
		}

		private void BuildChunks()
		{
			_chunkCount = 0;
			Array.Clear(_phaseRemaining, 0, _phaseRemaining.Length);

			int targetChunks = _workers.Length * _options.ChunksPerWorker;

			for (int p = 0; p < _lanes.Length; p++)
			{
				PhaseLane lane = _lanes[p];

				int oneShots = lane.ExecutingJobs.Count;
				if (oneShots > 0)
				{
					AddChunks(lane.OneShotRunner, oneShots, Math.Max(1, (oneShots + targetChunks - 1) / targetChunks), p);
				}

				int repeating = lane.RepeatingJobs.Count;
				if (repeating > 0)
				{
					AddChunks(lane.RepeatingRunner, repeating, Math.Max(1, (repeating + targetChunks - 1) / targetChunks), p);
				}

				List<IJobBatch> batches = lane.Batches;
				for (int i = 0; i < batches.Count; i++)
				{
					IJobBatch batch = batches[i];
					int       count = batch.ExecutingCount;
					if (count == 0) continue;

					int perChunk = batch.ItemsPerChunk > 0
						? batch.ItemsPerChunk
						: Math.Max(_options.MinBatchChunkItems, (count + targetChunks - 1) / targetChunks);

					AddChunks(batch, count, perChunk, p);
				}
			}
		}

		private void AddChunks(IChunkRunner runner, int count, int itemsPerChunk, int phase)
		{
			for (int start = 0; start < count; start += itemsPerChunk)
			{
				if (_chunkCount == _chunks.Length) Array.Resize(ref _chunks, _chunks.Length * 2);

				_chunks[_chunkCount++] = new Chunk(runner, start, Math.Min(itemsPerChunk, count - start), phase);
				_phaseRemaining[phase]++;
			}
		}

		private int FirstNonEmptyPhase()
		{
			int p = 0;
			while (p < _phaseRemaining.Length && _phaseRemaining[p] == 0) p++;
			return p;
		}

		// ---------------------------------------------------------------- workers

		private void WorkerMain(object boxedIndex)
		{
			int                  index = (int)boxedIndex;
			ManualResetEventSlim kick  = _kicks[index];

			Profiler.BeginThreadProfiling("AK.Jobs", Thread.CurrentThread.Name);

			while (true)
			{
				kick.Wait();
				kick.Reset();
				if (_stopping) break;

				RunFrame(index);
			}

			Profiler.EndThreadProfiling();
		}

		private void RunFrame(int index)
		{
			long start   = Stopwatch.GetTimestamp();
			int  faulted = 0;

			WorkerMarker.Begin();

			try
			{
				FrameContext ctx    = _ctx;
				Chunk[]      chunks = _chunks;
				int          count  = _chunkCount;

				while (true)
				{
					int c = Interlocked.Increment(ref _nextChunk) - 1;
					if (c >= count) break;

					ref readonly Chunk chunk = ref chunks[c];

					if (chunk.Phase > _openPhase) WaitForPhase(chunk.Phase);

					faulted += chunk.Runner.ExecuteRange(chunk.Start, chunk.Count, in ctx);

					if (Interlocked.Decrement(ref _phaseRemaining[chunk.Phase]) == 0) OpenPhasesAfter(chunk.Phase);
				}
			}
			catch (Exception e)
			{
				// Runners contain their jobs' exceptions, so this is a scheduler bug. Open every phase so
				// no peer spins forever on a gate this worker can no longer close.
				_openPhase = _phaseRemaining.Length;
				Debug.LogException(e);
			}
			finally
			{
				if (faulted > 0) Interlocked.Add(ref _faultedThisFrame, faulted);
				_workerTicks[index] = Stopwatch.GetTimestamp() - start;
				WorkerMarker.End();

				if (Interlocked.Decrement(ref _remainingWorkers) == 0) _done.Set();
			}
		}

		private void WaitForPhase(int phase)
		{
			int spins = 0;
			while (_openPhase < phase && !_stopping)
			{
				if (spins < 24)
				{
					Thread.SpinWait(4 << Math.Min(spins, 8));
					spins++;
				}
				else
				{
					Thread.Yield();
				}
			}
		}

		// Phases finish in order and exactly one worker closes each, so the gate only ever moves up;
		// the compare keeps a bug-path "open everything" write from being lowered afterwards.
		private void OpenPhasesAfter(int finished)
		{
			int next = finished + 1;
			while (next < _phaseRemaining.Length && Volatile.Read(ref _phaseRemaining[next]) == 0) next++;
			if (next > _openPhase) _openPhase = next;
		}

		// ---------------------------------------------------------------- teardown

		public void Dispose()
		{
			if (_disposed) return;
			ThrowIfNotMainThread();

			DetachFromPlayerLoop();

			bool workersQuiet = true;

			if (_busy)
			{
				if (_done.Wait(JoinTimeoutMs))
				{
					_busy = false;
					Collect();
				}
				else
				{
					workersQuiet = false;
					Debug.LogWarning("JobScheduler: workers still busy after " + JoinTimeoutMs + " ms; disposing without collecting the frame in flight.");
				}
			}

			_disposed = true;
			_stopping = true;
			for (int i = 0; i < _kicks.Length; i++) _kicks[i].Set();

			for (int i = 0; i < _workers.Length; i++)
			{
				if (!_workers[i].Join(JoinTimeoutMs))
				{
					workersQuiet = false;
					Debug.LogWarning($"JobScheduler: '{_workers[i].Name}' did not exit within {JoinTimeoutMs} ms.");
				}
			}

			CancelEverything();

			if (workersQuiet)
			{
				_done.Dispose();
				for (int i = 0; i < _kicks.Length; i++) _kicks[i].Dispose();
			}
		}

		private void CancelEverything()
		{
			SlotMap<JobSlot>.Enumerator e = _slots.GetEnumerator();
			while (e.MoveNext())
			{
				IJobCallback callback = e.Current.Callback;
				_slots.Remove(e.CurrentHandle);
				SafeComplete(callback, cancelled: true);
			}

			for (int p = 0; p < _lanes.Length; p++)
			{
				PhaseLane lane = _lanes[p];
				lane.PendingJobs.Clear();
				lane.PendingHandles.Clear();
				lane.ExecutingJobs.Clear();
				lane.ExecutingHandles.Clear();
				lane.RepeatingJobs.Clear();
				lane.RepeatingHandles.Clear();
				lane.StagedRepeatingJobs.Clear();
				lane.StagedRepeatingHandles.Clear();
				lane.CancelledBeforeHandOff = 0;
				lane.RepeatingCancelled     = 0;

				for (int i = 0; i < lane.Batches.Count; i++) lane.Batches[i].Owner = null;
				lane.Batches.Clear();
			}
		}

		// ---------------------------------------------------------------- helpers

		private static void SafeComplete(IJobCallback callback, bool cancelled)
		{
			if (callback == null) return;

			try
			{
				callback.OnComplete(cancelled);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		private void ValidatePhase(int phase)
		{
			if ((uint)phase >= (uint)_lanes.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(phase), phase, $"scheduler has {_lanes.Length} phase(s)");
			}
		}

		private void ThrowIfNotMainThread()
		{
			if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
			{
				throw new InvalidOperationException("JobScheduler is single-writer: call it only from the thread that created it.");
			}
		}

		private void ThrowIfDisposed()
		{
			if (_disposed) throw new ObjectDisposedException(nameof(JobScheduler));
		}
	}
}
