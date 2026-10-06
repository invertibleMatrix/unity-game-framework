using System;
#if UGFW_ADDRESSABLES
using UnityEngine;
using UnityEngine.AddressableAssets;
#endif

namespace AK.Utilities.Previews
{
	/// <summary>
	/// App-lifetime service that mints dialog-scoped <see cref="ModelPreviewSession"/>s. Its
	/// sessions share one offscreen stage space, so booths from different sessions never overlap.
	/// Keep one service per app: two would place their booths in the same space.
	/// </summary>
	public sealed class ModelPreviewService : IModelPreviewService
	{
		private readonly ModelPreviewStageSpace _space = new();
		private readonly IModelPreviewAssets _assets;

#if UGFW_ADDRESSABLES
		/// <summary>Loads the stage from <paramref name="stagePrefab"/> and models by address, through UniResources.</summary>
		public ModelPreviewService(AssetReferenceT<GameObject> stagePrefab)
		{
			var assets = new AddressablesModelPreviewAssets(stagePrefab);
			if (!assets.HasValidStage)
			{
				// Every preview load will fail until this is fixed; say so at boot, not at the first preview.
				Debug.LogError($"{nameof(ModelPreviewService)}: missing or invalid stage addressable reference.");
			}

			_assets = assets;
		}
#endif

		/// <summary>Takes stage and model prefabs from <paramref name="assets"/>.</summary>
		public ModelPreviewService(IModelPreviewAssets assets)
		{
			_assets = assets ?? throw new ArgumentNullException(nameof(assets));
		}

		public ModelPreviewSession CreateSession(ModelPreviewSessionOptions options = null) => new(_space, _assets, options);
	}
}
