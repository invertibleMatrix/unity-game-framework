using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Utilities.ModelPreview
{
	/// <summary>
	/// App-lifetime service that mints dialog-scoped <see cref="ModelPreviewSession"/>s.
	/// </summary>
	public sealed class ModelPreviewService : IModelPreviewService
	{
		private readonly AssetReferenceT<GameObject> _stagePrefab;

		public ModelPreviewService(AssetReferenceT<GameObject> stagePrefab)
		{
			_stagePrefab = stagePrefab;
		}

		public ModelPreviewSession CreateSession(ModelPreviewSessionOptions options = null)
		{
			if (_stagePrefab == null || !_stagePrefab.RuntimeKeyIsValid())
			{
				Debug.LogError($"{nameof(ModelPreviewService)}: missing or invalid stage addressable reference.");
			}

			return new ModelPreviewSession(_stagePrefab, options);
		}
	}
}
