using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace AK.Jobs
{
	/// <summary>
	/// Runs a slice of an <see cref="IFrameJob"/> array. <see cref="Jobs"/> is re-pointed by the main
	/// thread at the barrier (to the array that just became "executing"); workers only read it.
	/// </summary>
	internal sealed class ReferenceJobRunner : IChunkRunner
	{
		public JobArray<IFrameJob> Jobs;

		// Indices come from chunks the scheduler built over this very array, and entries are never null.
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		public int ExecuteRange(int start, int count, in FrameContext ctx)
		{
			IFrameJob[] items   = Jobs.Items;
			int         end     = start + count;
			int         faulted = 0;

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

			return faulted;
		}
	}
}
