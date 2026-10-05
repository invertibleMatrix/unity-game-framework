#if UGFW_ADDRESSABLES
using System;
using System.Threading;
using AK.Core.ResourceManagement;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AK.Utilities.Previews
{
	/// <summary>
	/// <see cref="IModelPreviewAssets"/> over <see cref="UniResources"/>: the stage from an
	/// addressable reference, models by address. Each load is one UniResources claim, given
	/// back by <see cref="Release"/>.
	/// </summary>
	public sealed class AddressablesModelPreviewAssets : IModelPreviewAssets
	{
		private readonly AssetReferenceT<GameObject> _stagePrefab;

		public AddressablesModelPreviewAssets(AssetReferenceT<GameObject> stagePrefab)
		{
			_stagePrefab = stagePrefab;
		}

		/// <summary>False when the stage reference is missing or empty; every stage load would then fail.</summary>
		public bool HasValidStage => _stagePrefab != null && _stagePrefab.RuntimeKeyIsValid();

		public UniTask<GameObject> LoadStageAsync(CancellationToken cancellation)
		{
			if (!HasValidStage)
			{
				return UniTask.FromException<GameObject>(
					new InvalidOperationException("ModelPreview: the stage addressable reference is missing or invalid."));
			}

			return UniResources.LoadAssetAsync(_stagePrefab, cToken: cancellation);
		}

		public UniTask<GameObject> LoadModelAsync(string address, CancellationToken cancellation)
		{
			return UniResources.LoadAssetAsync<GameObject>(address, cToken: cancellation);
		}

		public void Release(GameObject asset)
		{
			// A claim is given back even if the prefab was somehow destroyed.
			if (!ReferenceEquals(asset, null))
			{
				UniResources.DisposeAsset(asset);
			}
		}
	}
}
#endif
