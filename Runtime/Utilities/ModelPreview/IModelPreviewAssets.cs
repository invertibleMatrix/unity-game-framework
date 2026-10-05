using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Utilities.Previews
{
	/// <summary>
	/// Where a <see cref="ModelPreviewSession"/> gets the prefabs it instantiates.
	///
	/// Every prefab a load returns is one claim, and the session gives each one back with
	/// exactly one <see cref="Release"/>: each booth claims the stage and its model, and gives
	/// both back when it goes away, or just the model when it swaps to another. A failed load
	/// throws. A cancelled load throws <see cref="System.OperationCanceledException"/> and
	/// leaves no claim behind.
	/// </summary>
	public interface IModelPreviewAssets
	{
		/// <summary>The stage prefab: a <see cref="ModelPreviewCamera"/> rig every booth is instantiated from.</summary>
		UniTask<GameObject> LoadStageAsync(CancellationToken cancellation);

		/// <summary>A model prefab by address.</summary>
		UniTask<GameObject> LoadModelAsync(string address, CancellationToken cancellation);

		/// <summary>Gives back one claim on a prefab returned by a load.</summary>
		void Release(GameObject asset);
	}
}
