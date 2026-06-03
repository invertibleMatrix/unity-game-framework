using UnityEngine;
using AK.Core;

namespace AK.Examples
{
	[CreateAssetMenu(fileName = "AudioIds", menuName = "AK/MetaData/AudioIds")]
	public class AudioIds : ScriptableObject
	{
		public UID WooshOut;
		public UID WooshIn;
	}
}
