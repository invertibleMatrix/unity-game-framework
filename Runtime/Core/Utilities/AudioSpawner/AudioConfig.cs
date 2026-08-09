using System.Collections.Generic;
using AK.Core;
using Reflex.Extensions;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Utilities.AudioSpawner
{
	[CreateAssetMenu(fileName = "AudioConfig", menuName = "AK/Configs/AudioConfig")]
	public class AudioConfig : MetaDataAsset
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

		[Header("Concurrency")]
		[Tooltip("Max simultaneous voices of this sound. 0 = unlimited. Beyond the cap, the oldest voice is stopped early (steal-oldest).")]
		public int MaxConcurrentVoices = 0;

		[Tooltip("Minimum seconds between plays of this sound. 0 = no limit. Prevents machine-gun repetition.")]
		public float MinIntervalBetweenPlays = 0f;

		[Header("Ducking")]
		[Tooltip("If > 0, playing this sound ducks the music channel by this many dB.")]
		public float DuckMusicDb = 0f;

		[Tooltip("Seconds before the music channel restores after a duck.")]
		public float DuckDuration = 0.5f;
#if UNITY_EDITOR
		public void PlayDebug()
		{
			var audioSpawner = SceneManager.GetActiveScene().GetSceneContainer().Resolve<IAudioSpawner>();
			audioSpawner.Spawn(Prefab.GetType(), UniqueID).Play();
		}
#endif
	}
}