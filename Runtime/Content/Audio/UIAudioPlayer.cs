using AK.Core;
using AK.Utilities.Audio;
using Reflex.Extensions;
using UnityEngine;

namespace AK.UI
{
	public class UIAudioPlayer : MonoBehaviour
	{
		// Typed as AudioConfig (not raw UID) — the picker filters to audio configs only,
		// and the assignment is type-checked. The field name stays for serialization.
		public AudioConfig AudioId;

		private IAudioSpawner _audioSpawner;

		private void Awake()
		{
			_audioSpawner = gameObject.scene.GetSceneContainer().Resolve<IAudioSpawner>();
		}

		public void Play()
		{
			if (AudioId == null)
			{
				Debug.LogError($"Audio ID is Null at {gameObject.name}");
				return;
			}

			_audioSpawner?.PlayAudio(AudioId);
		}
	}
}