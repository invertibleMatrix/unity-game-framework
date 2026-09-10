using UnityEngine;
using Utilities.AudioSpawner;

namespace AK.Examples
{
	[CreateAssetMenu(fileName = "AudioIds", menuName = "AK/MetaData/AudioIds")]
	public class AudioIds : ScriptableObject
	{
		public AudioConfig WooshOut;
		public AudioConfig WooshIn;
	}
}
