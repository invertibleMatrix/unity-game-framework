using System.Collections.Generic;
using AK.Core;
using Reflex.Extensions;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Utilities.AudioSpawner
{
	[CreateAssetMenu(fileName = "AudioConfig", menuName = "Gameplay/Configs/AudioConfig")]
	public class AudioConfigBase : UID
	{
		[Tooltip("The prefab for this audio type. All variants of the same type share this prefab.")]
		public AudioComponent Prefab;

		[Tooltip("The AudioMixerGroup to route this sound to (e.g., SFX, Music).")]
		public AudioMixerGroup OutputGroup;

		[Tooltip("The audio clips to be chosen from when this effect is played.")]
		public List<AudioClip> Clips;

		[Tooltip("The initial number of instances to create in the pool for this effect.")]
		public int InitialPoolSize = 5;

		[Tooltip("Time it will take to play the sound after Play has called")]
		public float StartAfterSeconds;

		[Tooltip("If not 0 , the audio will will be stopped after this time.")]
		public float StopAfterSeconds;

		[Range(0f, 1f)] public float Volume = 1.0f;

		[Range(0f, 2f)] [Tooltip("The range of random pitch to apply. X is min, Y is max.")]
		public Vector2 PitchRange = new(0.95f, 1.05f);

		[Tooltip("If this is not zero and Loop is set to true then loop will break and start at this specified time")]
		public float LoopInterval = 0;

		[Tooltip("Time in seconds to fade in the audio. If 0, plays instantly.")]
		public float FadeInDuration = 0f;

		[Tooltip("Time in seconds to fade out the audio. If 0, stops instantly.")]
		public float FadeOutDuration = 0f;

		[Tooltip("If ticked then all the Clips will be played sequentially otherwise random clip will be played from list")]
		public bool PlayAllSequentially;

		[Tooltip("If true, the audio will loop until stopped manually or after specified StopAfterSeconds")]
		public bool Loop = false;

		[Tooltip("If true, the sound will be played in 3D space. If false, it will be 2D and heard everywhere.")]
		public bool IsSpatial = true;
#if UNITY_EDITOR
		[Button]
		public void PlayDebug()
		{
			var audioSpawner = SceneManager.GetActiveScene().GetSceneContainer().Resolve<IAudioSpawner>();
			audioSpawner.Spawn(Prefab.GetType(), UniqueID).Play();
		}
#endif
	}
}