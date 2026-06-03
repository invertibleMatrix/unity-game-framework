using Reflex.Extensions;
using UnityEngine;
using AK.Core;
using Sirenix.OdinInspector;
using UI;
using Utilities.AudioSpawner;

namespace UI
{
	public class UIAudioPlayer : MonoBehaviour
	{
		public UID AudioId;

		private IAudioSpawner _audioSpawner;

		private void Awake()
		{
			_audioSpawner = gameObject.scene.GetSceneContainer().Resolve<IAudioSpawner>();
		}

		[Button]
		public void Play()
		{
			if (AudioId == null)
			{
				Debug.LogError($"Audio ID is Null at {gameObject.name}");
				return;
			}

			_audioSpawner.PlayAudio(AudioId);
		}
	}
}