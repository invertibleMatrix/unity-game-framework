namespace AK.Core.ResourceManagement
{
	/// <summary>
	/// <see cref="IOperationStatusProvider"/> Is Contract To Get <see cref="DownloadStatus"/> From System...
	/// </summary>
	public interface IOperationStatusProvider
	{
		/// <summary>
		/// <see cref="GetStatus"/> Is Going To Return Current Downloading Operation Status Otherwise Return default...
		/// </summary>
		OperationStatus GetStatus();


		public static readonly IOperationStatusProvider Default = new OperationStatusProvider();

		/// <inheritdoc />
		private sealed class OperationStatusProvider : IOperationStatusProvider
		{
			/// <inheritdoc />
			public OperationStatus GetStatus() => new(false);
		}
	}
}