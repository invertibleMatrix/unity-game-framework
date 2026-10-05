using System;
using System.Diagnostics;
using System.Threading;

namespace AK.Kernel.Threading
{
	/// <summary>
	/// A development check that main-thread-only code runs on the main thread. The static pools
	/// and buses are not thread-safe: a worker that uses one corrupts it silently, and the
	/// failure shows up later on the main thread, so the check has to be at the call.
	///
	/// The engine layer names the main thread, and how a call from another thread is reported,
	/// with <see cref="SetMainThread"/>; until then nothing is checked. Calls to
	/// <see cref="Assert"/> compile to nothing outside the editor and development builds.
	/// </summary>
	public static class MainThreadGuard
	{
		private static Action<string> _report;
		private static int _mainThreadId;

		/// <summary>
		/// Names the calling thread as the main thread. <paramref name="report"/> gets a message
		/// for each checked call from another thread.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="report"/> is null.</exception>
		public static void SetMainThread(Action<string> report)
		{
			_report = report ?? throw new ArgumentNullException(nameof(report));

			// Published after the report, so a thread that sees the id also sees the report.
			Volatile.Write(ref _mainThreadId, Thread.CurrentThread.ManagedThreadId);
		}

		/// <summary>Reports <paramref name="what"/> when called from a thread other than the main thread.</summary>
		[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
		public static void Assert(string what)
		{
			int mainThreadId = Volatile.Read(ref _mainThreadId);
			if (mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId != mainThreadId)
			{
				_report($"{what} is main-thread only; called from '{Thread.CurrentThread.Name ?? "unnamed thread"}'.");
			}
		}
	}
}
