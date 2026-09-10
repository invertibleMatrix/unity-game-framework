using System;

namespace AK.Core
{
	/// <summary>
	/// Something that can hold its writes back for a while: <see cref="Suspend"/> stops it
	/// flushing, <see cref="Resume"/> lets it flush and performs the deferred flush if any
	/// write happened in between. Calls nest; only the outermost <see cref="Resume"/> flushes.
	/// </summary>
	public interface IBatchable
	{
		void Suspend();
		void Resume();
	}

	/// <summary>
	/// Scope guard over an <see cref="IBatchable"/>: resumes on dispose, on every exit path,
	/// exceptions included. <c>ref struct</c> so it cannot be stored in a field, captured by a
	/// lambda, boxed, or awaited across — the scope is the stack frame that opened it.
	///
	/// <code>
	/// using (facts.BeginBatch())
	/// {
	///     facts.Record(a);
	///     facts.Record(b);
	///     facts.Record(c);
	/// }   // one serialization, one disk flush
	/// </code>
	/// </summary>
	public readonly ref struct LedgerBatch
	{
		private readonly IBatchable _target;

		public LedgerBatch(IBatchable target)
		{
			_target = target ?? throw new ArgumentNullException(nameof(target));
			_target.Suspend();
		}

		public void Dispose()
		{
			_target.Resume();
		}
	}

	/// <summary>
	/// Reusable suspend/resume bookkeeping for anything that commits to disk. Wrap the
	/// commit in <see cref="Commit"/>; hand <see cref="Suspend"/>/<see cref="Resume"/> to the
	/// <see cref="IBatchable"/> surface.
	/// </summary>
	public sealed class DeferredCommit
	{
		private readonly Action _commit;
		private int             _depth;
		private bool            _dirty;

		public DeferredCommit(Action commit)
		{
			_commit = commit ?? throw new ArgumentNullException(nameof(commit));
		}

		public bool IsSuspended => _depth > 0;

		/// <summary>Commits now, or marks dirty when suspended.</summary>
		public void Commit()
		{
			if (_depth > 0)
			{
				_dirty = true;
				return;
			}

			_commit();
		}

		public void Suspend()
		{
			_depth++;
		}

		public void Resume()
		{
			if (_depth == 0) throw new InvalidOperationException("Resume without a matching Suspend.");

			if (--_depth == 0 && _dirty)
			{
				_dirty = false;
				_commit();
			}
		}
	}
}
