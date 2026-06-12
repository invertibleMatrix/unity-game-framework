using UnityEngine.ResourceManagement.AsyncOperations;
using AK.Core.Extensions;

namespace AK.Core.ResourceManagement
{
	internal sealed class OperationStatusProvider : IOperationStatusProvider
	{
		private readonly AsyncOperationHandle _asyncOperation = default;

		public OperationStatusProvider(AsyncOperationHandle asyncOperation)
			=> _asyncOperation = asyncOperation;

		/// <inheritdoc />
		public OperationStatus GetStatus() => _asyncOperation.ToOperationStatus();
	}
}