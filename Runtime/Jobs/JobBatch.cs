using System;
using System.Threading;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace AK.Jobs
{
	/// <summary>
	/// Homogeneous struct jobs stored contiguously, so a worker streams through the array and the
	/// element's fields are the job's inputs and outputs — no object per job, no pointer to chase,
	/// and the constrained generic call is direct.
	///
	/// Three buffers rotate at the barrier: the one <see cref="Add"/> fills during the frame, the one
	/// the workers execute, and the one holding the last finished frame, readable through
	/// <see cref="Results"/> for the whole frame. Elements added during frame N run during frame N + 1
	/// and appear in <see cref="Results"/> from the barrier that opens frame N + 2. Results stay until
	/// a later frame with at least one element replaces them.
	///
	/// Register the batch with a scheduler once; call <see cref="Add"/> and read <see cref="Results"/>
	/// from the scheduler's thread only. Each element writes only itself, so elements never contend.
	/// </summary>
	public sealed class JobBatch<TJob> : IJobBatch where TJob : struct, IJob
	{
		private TJob[] _pending;
		private TJob[] _executing;
		private TJob[] _results;

		private int _pendingCount;
		private int _executingCount;
		private int _resultCount;

		private int _faulted;
		private int _resultFaults;

		private readonly int _ownerThreadId;

		public JobBatch(int capacity, int phase = 0, int itemsPerChunk = 0)
		{
			if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "at least one");
			if (phase < 0) throw new ArgumentOutOfRangeException(nameof(phase), phase, "non-negative");
			if (itemsPerChunk < 0) throw new ArgumentOutOfRangeException(nameof(itemsPerChunk), itemsPerChunk, "0 for automatic, otherwise at least one");

			_pending   = new TJob[capacity];
			_executing = new TJob[capacity];
			_results   = new TJob[capacity];

			Phase          = phase;
			ItemsPerChunk  = itemsPerChunk;
			_ownerThreadId = Thread.CurrentThread.ManagedThreadId;
		}

		public int Phase         { get; }
		public int ItemsPerChunk { get; }

		/// <summary>Elements added since the last barrier; they run next frame.</summary>
		public int PendingCount => _pendingCount;

		/// <summary>Elements of the last finished frame. Mutable so a result can be fed straight back into <see cref="Add"/>.</summary>
		public Span<TJob> Results => new(_results, 0, _resultCount);

		/// <summary>Elements in <see cref="Results"/> whose <see cref="IJob.Execute"/> threw; their fields may be partially written.</summary>
		public int ResultFaults => _resultFaults;

		public bool IsRegistered => Owner != null;

		int          IJobBatch.ExecutingCount => _executingCount;
		JobScheduler IJobBatch.Owner          { get => Owner; set => Owner = value; }

		internal JobScheduler Owner { get; private set; }

		/// <summary>
		/// Reserves the next element and returns it by reference, cleared, so the caller fills the
		/// fields in place without copying the struct.
		/// </summary>
		public ref TJob Add()
		{
			ThrowIfNotOwnerThread();

			if (_pendingCount == _pending.Length) Array.Resize(ref _pending, _pending.Length * 2);

			ref TJob slot = ref _pending[_pendingCount++];
			slot = default;
			return ref slot;
		}

		public void Add(in TJob job)
		{
			ThrowIfNotOwnerThread();

			if (_pendingCount == _pending.Length) Array.Resize(ref _pending, _pending.Length * 2);

			_pending[_pendingCount++] = job;
		}

		/// <summary>Drops everything added since the last barrier.</summary>
		public void ClearPending()
		{
			ThrowIfNotOwnerThread();
			_pendingCount = 0;
		}

		void IJobBatch.Rotate()
		{
			if (_executingCount > 0)
			{
				TJob[] finished = _executing;

				_executing = _pending;
				_pending   = _results;
				_results   = finished;

				_resultCount  = _executingCount;
				_resultFaults = _faulted;
			}
			else
			{
				(_executing, _pending) = (_pending, _executing);
			}

			_executingCount = _pendingCount;
			_pendingCount   = 0;
			_faulted        = 0;
		}

		// Indices come from chunks the scheduler built over this very array; there is no null to check in a struct array.
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		int IChunkRunner.ExecuteRange(int start, int count, in FrameContext ctx)
		{
			TJob[] items   = _executing;
			int    end     = start + count;
			int    faulted = 0;

			for (int i = start; i < end; i++)
			{
				try
				{
					items[i].Execute(in ctx);
				}
				catch (Exception e)
				{
					faulted++;
					Debug.LogException(e);
				}
			}

			if (faulted > 0) Interlocked.Add(ref _faulted, faulted);
			return faulted;
		}

		private void ThrowIfNotOwnerThread()
		{
			if (Thread.CurrentThread.ManagedThreadId != _ownerThreadId)
			{
				throw new InvalidOperationException("JobBatch is single-writer: add and read from the thread that created it.");
			}
		}
	}
}
