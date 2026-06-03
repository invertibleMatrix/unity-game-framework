using UnityEngine;
using AK.Core;

namespace AK.CoreDomain
{
	[CreateAssetMenu(fileName = "AudioIds", menuName = "Gameplay/MetaData/AudioIds")]
	public class AudioIds : ScriptableObject
	{
		public UID PropsSpawned;
		public UID LevelFail;
		public UID BoxMerge;
		public UID PropSelected;
		public UID PropAutoMoved;
		public UID NewBoxSpawned;
		public UID LevelComplete;
		public UID StarClick;
		public UID WooshOut;
		public UID WooshIn;
	}
}