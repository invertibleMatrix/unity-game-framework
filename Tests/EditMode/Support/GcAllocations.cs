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
	internal static class GcAllocations
	{
		/// <summary>Allocations on the calling thread only, or on every profiled thread when <paramref name="allThreads"/> is set.</summary>
		public static int Count(Action body, bool allThreads = false)
		{
			Recorder recorder = Recorder.Get("GC.Alloc");
			recorder.enabled = false;

			if (allThreads) recorder.CollectFromAllThreads();
			else recorder.FilterToCurrentThread();

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
