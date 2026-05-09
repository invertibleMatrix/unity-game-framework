namespace AK.Core.ResourceManagement
{
	public readonly ref struct OperationStatus
	{
		/// <summary>
		/// Is operation running. This is used to check whether to use status or not.
		/// </summary>
		public readonly bool IsRunning;

		/// <summary>
		/// The number of bytes downloaded by the operation and all of its dependencies.
		/// </summary>
		public readonly long TotalBytes;

		/// <summary>
		/// The total number of bytes needed to download by the operation and dependencies.
		/// </summary>
		public readonly long DownloadedBytes;

		/// <summary>
		/// Is the operation completed.  This is used to determine if the computed Percent should be 0 or 1 when TotalBytes is 0.
		/// </summary>
		public readonly bool IsDone;

		/// <summary>
		/// Returns the computed percent complete as a float value between 0 &amp; 1.  If TotalBytes == 0, 1 is returned.
		/// </summary>
		public float Percent =>
			(TotalBytes > 0) ? ((float) DownloadedBytes / (float) TotalBytes) : (IsDone ? 1.0f : 0f);

		/// <summary>
		/// private .ctor to control allocation from factory method. 
		/// </summary>
		public OperationStatus(bool isRunning, bool isDone = false, long totalBytes = 0, long downloadedBytes = 0)
		{
			IsRunning = isRunning;
			IsDone = isDone;
			TotalBytes = totalBytes;
			DownloadedBytes = downloadedBytes;
		}
	}
}