namespace Utilities.Jobs
{
    public static class JobTraitBits
    {
        public static readonly uint None                        = 1u << 0;
        public static readonly uint ExecuteCompleteOnMainThread = 1u << 1; // take this as parameter in Dispatch call
        public static readonly uint MarkedForCancellation       = 1u << 2;
        public static readonly uint MarkedForExecution          = 1u << 3; // take this as parameter in Dispatch call
    }
}