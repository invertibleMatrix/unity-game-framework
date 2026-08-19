using AK.Core;
using UnityEngine;

namespace Utilities.ParticleSpawner
{
	[CreateAssetMenu(fileName = "ParticleConfigBase", menuName = "AK/Configs/ParticleConfigBase")]
	public class ParticleConfigBase : UID
	{
		[Tooltip("ID for this specific variant of the particle. Can be shared across different particle types (e.g., 'small', 'large').")]
		public UID VariantId => this;

		public ParticleComponent Prefab;
		public int               InitialPoolSize = 1;
		public float             StartDelayInSeconds;

		[Tooltip("If particle is set to loop then stop it after these seconds, set 0 to keep looping")]
		public float StopAfterSeconds;

		[Header("Concurrency")]
		[Tooltip("Max live instances of this particle at once. 0 = unlimited. Extra spawns are refused (drop-newest) — a dying particle is more visible than one never born.")]
		public int MaxActiveInstances = 0;

		[Tooltip("If set, a stopped effect recycles only after the whole hierarchy (trails, fading children) is truly dead — avoids the visual pop when the root system ends first.")]
		public bool WaitForChildrenToFinish;
	}
}