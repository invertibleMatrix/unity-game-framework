using UnityEngine;
using AK.Core;

namespace AK.CoreDomain
{
	[CreateAssetMenu(fileName = "ParticleIds", menuName = "Gameplay/MetaData/ParticleIds")]
	public class ParticleIds : ScriptableObject
	{
		public UID BoxMergeParticle;
		public UID StarsPop;
	}
}