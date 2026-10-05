using System;
using UnityEngine.Profiling;

namespace AK.Tests.Support
{
	/// <summary>
	/// Counts managed allocations performed while <paramref name="body"/> runs, using the profiler's
	/// <c>GC.Alloc</c> marker — the same mechanism as Unity's <c>Is.Not.AllocatingGCMemory()</c>.
	/// Unity's Mono does not implement <c>GC.GetAllocatedBytesForCurrentThread</c>: it returns 0 on
	/// every call, so a byte delta taken from it is always zero and proves nothing.
	/// </summary>
	public static class GcAllocations
	{
		/// <summary>Allocations on the calling thread while <paramref name="body"/> runs.</summary>
		public static int Count(Action body)
		{
			Recorder recorder = Recorder.Get("GC.Alloc");
			recorder.enabled = false;
			recorder.FilterToCurrentThread();
			return Record(recorder, body);
		}

		/// <summary>
		/// Allocations on every thread while <paramref name="body"/> runs: the fewest over up to
		/// <paramref name="runs"/> runs, as <paramref name="body"/> is run again while a count isn't 0.
		/// The editor's own threads allocate now and then, a few 8 ms windows in a few hundred when
		/// idle, and a count over every thread takes theirs in too. The profiler can't count a set of
		/// threads, but an allocation the body makes on every run still shows in the fewest.
		/// </summary>
		public static int CountOnAllThreads(Action body, int runs = 3)
		{
			Recorder recorder = Recorder.Get("GC.Alloc");
			int      fewest   = int.MaxValue;

			for (int run = 0; run < runs && fewest > 0; run++)
			{
				recorder.enabled = false;
				recorder.CollectFromAllThreads();
				fewest = Math.Min(fewest, Record(recorder, body));
			}

			return fewest;
		}

		private static int Record(Recorder recorder, Action body)
		{
			recorder.enabled = true;
			try
			{
				body();
			}
			finally
			{
				recorder.enabled = false;
			}

			return recorder.sampleBlockCount;
		}
	}
}
