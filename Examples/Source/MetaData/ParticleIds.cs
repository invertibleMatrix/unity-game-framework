using UnityEngine;
using Utilities.ParticleSpawner;

namespace AK.Examples
{
	[CreateAssetMenu(fileName = "ParticleIds", menuName = "AK/MetaData/ParticleIds")]
	public class ParticleIds : ScriptableObject
	{
		public ParticleConfigBase Confetti;
		public ParticleConfigBase Sparkle;
	}
}
