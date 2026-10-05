using System.Collections.Generic;
using AK.Core;
using Reflex.Extensions;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace AK.Utilities.Audio
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

		[Tooltip("Voices made ahead for this config's prefab. A prefab's pool starts with the largest size among its configs, and grows when it runs dry.")]
		public int InitialPoolSize = 5;

		[Tooltip("Seconds waited after Play before the first clip starts.")]
		public float StartAfterSeconds;

		[Tooltip("If above 0, the sound stops this long after its first clip starts: its fade-out ends then.")]
		public float StopAfterSeconds;

		[Range(0f, 1f)] public float Volume = 1.0f;

		[Range(0f, 2f)] [Tooltip("The range of random pitch to apply. X is min, Y is max.")]
		public Vector2 PitchRange = new(0.95f, 1.05f);

		[Tooltip("If above 0, the longest each clip is held at full volume before it fades out and the next one starts.")]
		public float LoopInterval = 0;

		[Tooltip("Seconds each clip fades in over. If 0, it starts at full volume.")]
		public float FadeInDuration = 0f;

		[Tooltip("Seconds each clip fades out over before the next one starts, and the sound when stopped. If 0, it cuts at once.")]
		public float FadeOutDuration = 0f;

		[Tooltip("If ticked, every clip plays, in the listed order. Otherwise one random clip plays, or, when looping, all of them in a random order.")]
		public bool PlayAllSequentially;

		[Tooltip("If true, the clips play over and over until the sound is stopped, or until StopAfterSeconds.")]
		public bool Loop = false;

		[Tooltip("If true, the sound will be played in 3D space. If false, it will be 2D and heard everywhere.")]
		public bool IsSpatial = true;

		[Header("Concurrency")]
		[Tooltip("Max voices of this sound sounding at once. 0 = unlimited. Beyond the cap, the oldest one still sounding fades out to make room (steal-oldest).")]
		public int MaxConcurrentVoices = 0;

		[Tooltip("Minimum seconds between plays of this sound. 0 = no limit. A play sooner is turned away, so PlayAudio returns null. Prevents machine-gun repetition.")]
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
			audioSpawner.PlayAudio(this);
		}
#endif
	}
}