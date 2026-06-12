using System;
using AK.Core.ResourceManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AK.Core.Extensions
{
	public static class AddressableExt
	{
		/// <summary>
		/// Create & Return A New <see cref="OperationStatus"/> From This <see cref="AsyncOperationHandle"/>
		/// </summary>
		public static OperationStatus ToOperationStatus(this AsyncOperationHandle operation)
		{
			if (operation.IsValid() == false) return new OperationStatus(false);

			var status = operation.GetDownloadStatus();
			return new OperationStatus(true, status.IsDone, status.TotalBytes, status.DownloadedBytes);
		}

		/// <summary>
		/// Convert Core Enum Type To <see cref="Addressables.MergeMode"/>
		/// </summary>
		public static Addressables.MergeMode Convert(this MergeMode mode)
		{
			return mode switch
				{
					MergeMode.UseFirst => Addressables.MergeMode.None, // Addressables' "None" = UseFirst
					MergeMode.Union => Addressables.MergeMode.Union,
					MergeMode.Intersection => Addressables.MergeMode.Intersection,
					_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
				};
		}
	}
}