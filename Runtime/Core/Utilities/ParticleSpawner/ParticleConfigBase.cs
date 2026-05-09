using AK.Core;
using UnityEngine;

namespace Utilities.ParticleSpawner
{
	[CreateAssetMenu(fileName = "ParticleConfigBase", menuName = "Gameplay/Configs/ParticleConfigBase")]
	public class ParticleConfigBase : UID
	{
		[Tooltip("ID for this specific variant of the particle. Can be shared across different particle types (e.g., 'small', 'large').")]
		public UID VariantId => this;

		public ParticleComponent Prefab;
		public int               InitialPoolSize = 1;
		public float             StartDelayInSeconds;

		[Tooltip("If particle is set to loop then stop it after these seconds, set 0 to keep looping")]
		public float StopAfterSeconds;
	}
}