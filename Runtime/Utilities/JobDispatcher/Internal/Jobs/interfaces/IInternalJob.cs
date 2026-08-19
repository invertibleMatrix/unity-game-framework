using System.Runtime.CompilerServices;

namespace Utilities.Jobs
{
    internal interface IInternalJob
    {
        public uint             Traits { get; set; }
        public IDispatchableJob Job    { get; set; }
    }

    internal static class InternalJobExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkForCancellation(this IInternalJob job)
        {
            job.Traits |= JobTraitBits.MarkedForCancellation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMarkedForCancellation(this IInternalJob job)
        {
            return (job.Traits & JobTraitBits.MarkedForCancellation) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkForExecution(this IInternalJob job)
        {
            job.Traits |= JobTraitBits.MarkedForExecution;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearMarkForExecution(this IInternalJob job)
        {
            job.Traits &= ~JobTraitBits.MarkedForExecution;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMarkedForExecution(this IInternalJob job)
        {
            return (job.Traits & JobTraitBits.MarkedForExecution) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetExecuteCompleteOnMainThread(this IInternalJob job)
        {
            job.Traits |= JobTraitBits.ExecuteCompleteOnMainThread;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldCallCompleteOnMainThread(this IInternalJob job)
        {
            return (job.Traits & JobTraitBits.ExecuteCompleteOnMainThread) != 0;
        }
    }
}