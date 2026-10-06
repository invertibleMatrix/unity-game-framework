using System.Collections.Generic;
using System.Text.RegularExpressions;
using AK.Core;
using AK.Kernel.Timing;
using AK.Tests.Support;
using AK.Utilities.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AK.Tests.Utilities
{
	/// <summary>
	/// The audio spawner and its voices in edit mode, where nothing updates by itself: a voice
	/// moves on with <c>Step</c>, and the music with <c>StepMusic</c>. A clip is a second of
	/// silence, so a voice at normal pitch holds it for a second.
	/// </summary>
	public class AudioSpawnerTests
	{
		private readonly List<Object> _created = new();

		private AudioSpawner   _spawner;
		private AudioComponent _prefab;
		private AudioClip      _clip;

		[SetUp]
		public void SetUp()
		{
			_clip    = MakeClip("Second", 1);
			_prefab  = MakePrefab("Voice");
			_spawner = Track(new GameObject("Spawner")).AddComponent<AudioSpawner>();
		}

		[TearDown]
		public void TearDown()
		{
			for (int i = _created.Count - 1; i >= 0; i--)
			{
				if (_created[i] != null) Object.DestroyImmediate(_created[i]);
			}

			_created.Clear();
		}

		private T Track<T>(T obj) where T : Object
		{
			_created.Add(obj);
			return obj;
		}

		private AudioClip MakeClip(string name, int seconds) => Track(AudioClip.Create(name, 44100 * seconds, 1, 44100, false));

		/// <summary>A voice prefab: an inactive template, as a prefab asset never plays itself.</summary>
		private AudioComponent MakePrefab(string name)
		{
			GameObject go = Track(new GameObject(name));
			go.SetActive(false);
			return go.AddComponent<AudioComponent>();
		}

		private AudioConfig MakeConfig(AudioComponent prefab = null, params AudioClip[] clips)
		{
			AudioConfig config = Track(ScriptableObject.CreateInstance<AudioConfig>());
			config.Prefab     = prefab != null ? prefab : _prefab;
			config.Clips      = new List<AudioClip>(clips.Length > 0 ? clips : new[] { _clip });
			config.PitchRange = Vector2.one;
			config.IsSpatial  = false;
			return config;
		}

		private AudioConfig MakeMusic(AudioClip clip)
		{
			AudioConfig config = MakeConfig(null, clip);
			config.Loop = true;
			return config;
		}

		private void UseRegistry(params AudioConfig[] configs)
		{
			AudioRegistry registry = Track(ScriptableObject.CreateInstance<AudioRegistry>());
			foreach (AudioConfig config in configs)
			{
				config.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);
				Assert.IsTrue(registry.Editor_TryTrack(config));
			}

			SetField("_audioRegistry", registry);
		}

		private void SetField(string field, Object value)
		{
			var serialized = new SerializedObject(_spawner);
			serialized.FindProperty(field).objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private int CountVoices() => _spawner.GetComponentsInChildren<AudioComponent>(true).Length;

		private AudioSource Music(string source) => _spawner.transform.Find("AudioSpawner_Music/" + source).GetComponent<AudioSource>();

		// ------------------------------------------------------------------ voices

		[Test]
		public void PlayAudio_PlaysTheClip_AndTheVoiceGoesBackToItsPoolWhenItEnds()
		{
			AudioConfig config = MakeConfig();

			AudioComponent voice = _spawner.PlayAudio(config);

			Assert.IsNotNull(voice);
			Assert.IsTrue(voice.IsPlaying);
			Assert.IsTrue(_spawner.IsPlaying(config));
			Assert.AreSame(_clip, voice.GetComponent<AudioSource>().clip);

			voice.Step(1d);

			Assert.IsFalse(voice.IsPlaying);
			Assert.IsFalse(_spawner.IsPlaying(config));
			Assert.IsFalse(voice.gameObject.activeSelf, "back in its pool");
			Assert.AreSame(voice, _spawner.PlayAudio(config), "and handed out again");
		}

		[Test]
		public void Voices_ArePooledPerPrefab_NotPerType()
		{
			AudioComponent otherPrefab = MakePrefab("OtherVoice");
			otherPrefab.GetComponent<AudioSource>().priority = 7;
			AudioConfig a = MakeConfig();
			AudioConfig b = MakeConfig(otherPrefab);

			AudioComponent fromA = _spawner.PlayAudio(a);
			fromA.Step(1d);
			AudioComponent fromB = _spawner.PlayAudio(b);

			Assert.AreNotSame(fromA, fromB, "b's prefab has a pool of its own, though both are AudioComponents");
			Assert.AreEqual(7, fromB.GetComponent<AudioSource>().priority, "made from b's prefab");

			fromB.Step(1d);
			Assert.AreSame(fromA, _spawner.PlayAudio(a));
			Assert.AreSame(fromB, _spawner.PlayAudio(b));
		}

		[Test]
		public void Pools_StartWithTheLargestSizeAmongTheirPrefabsConfigs()
		{
			AudioComponent otherPrefab = MakePrefab("OtherVoice");
			AudioConfig small = MakeConfig();
			AudioConfig large = MakeConfig();
			AudioConfig other = MakeConfig(otherPrefab);
			small.InitialPoolSize = 2;
			large.InitialPoolSize = 3;
			other.InitialPoolSize = 1;
			UseRegistry(small, large, other);

			_spawner.PlayAudio(small);

			Assert.AreEqual(4, CountVoices(), "three for the first prefab, one for the other, and none made on demand");
		}

		[Test]
		public void Spawn_HandsOutATrackedVoice_ThatCountsOnceItPlays()
		{
			AudioConfig config = MakeConfig();
			UseRegistry(config);

			AudioComponent voice = _spawner.Spawn<AudioComponent>();

			Assert.IsNotNull(voice);
			Assert.IsFalse(voice.IsPlaying);
			Assert.IsFalse(_spawner.IsPlaying(config), "not playing yet");

			voice.Play();
			Assert.IsTrue(_spawner.IsPlaying(config));

			_spawner.StopAll(config);
			Assert.IsFalse(voice.IsPlaying, "StopAll reaches spawned voices");
			Assert.IsFalse(voice.gameObject.activeSelf);
		}

		[Test]
		public void Stop_OnASpawnedVoiceNeverPlayed_GivesItBack()
		{
			UseRegistry(MakeConfig());
			AudioComponent voice = _spawner.Spawn<AudioComponent>();

			voice.Stop();

			Assert.AreSame(voice, _spawner.Spawn<AudioComponent>(), "back in its pool, and handed out again");
		}

		[Test]
		public void StopAll_WithNoConfig_StopsEveryVoice()
		{
			AudioComponent a = _spawner.PlayAudio(MakeConfig());
			AudioComponent b = _spawner.PlayAudio(MakeConfig());

			_spawner.StopAll();

			Assert.IsFalse(a.IsPlaying);
			Assert.IsFalse(b.IsPlaying);
		}

		[Test]
		public void MinIntervalBetweenPlays_TurnsAwayAPlayTooSoon()
		{
			AudioConfig config = MakeConfig();
			config.MinIntervalBetweenPlays = 60f;

			AudioComponent first = _spawner.PlayAudio(config);

			Assert.IsNotNull(first);
			Assert.IsNull(_spawner.PlayAudio(config), "PlayAudio returns null within the replay gap");
			Assert.AreEqual(2, CountVoices());

			first.Step(1d);
			foreach (AudioComponent voice in _spawner.GetComponentsInChildren<AudioComponent>(true))
			{
				Assert.IsFalse(voice.gameObject.activeSelf, "the voice turned away went back to its pool too");
			}
		}

		[Test]
		public void MaxConcurrentVoices_StopsTheOldestVoiceStillSounding()
		{
			AudioConfig config = MakeConfig();
			config.MaxConcurrentVoices = 1;
			config.FadeOutDuration     = 0.5f;
			config.Loop                = true;

			AudioComponent a = _spawner.PlayAudio(config);
			AudioComponent b = _spawner.PlayAudio(config);

			Assert.IsTrue(a.IsPlaying && a.IsStopping, "a fades out to make room for b");
			Assert.IsFalse(b.IsStopping);

			AudioComponent c = _spawner.PlayAudio(config);

			Assert.IsTrue(b.IsStopping, "the cap stops b, not a, which is fading out already");
			Assert.IsFalse(c.IsStopping);
		}

		[Test]
		public void StopAfter_EndsAVoiceMidClip()
		{
			AudioConfig config = MakeConfig(null, MakeClip("Long", 10));
			config.StopAfterSeconds = 2f;

			AudioComponent voice = _spawner.PlayAudio(config);
			voice.Step(1.5d);
			Assert.IsTrue(voice.IsPlaying);

			voice.Step(0.5d);
			Assert.IsFalse(voice.IsPlaying, "two seconds into a ten-second clip");
		}

		[Test]
		public void AVoiceThatDucksTheMusic_DucksItWhenItPlays()
		{
			var mixer = Track(new GameObject("Mixer")).AddComponent<AudioMixerController>();
			SetField("_mixerController", mixer);
			AudioConfig config = MakeConfig();
			config.DuckMusicDb = 6f;
			UseRegistry(config);

			AudioComponent voice = _spawner.Spawn<AudioComponent>();
			Assert.AreEqual(0f, mixer.DuckDb, "not when spawned");

			voice.Play();
			Assert.AreEqual(6f, mixer.DuckDb);
		}

		[Test]
		public void ThousandPlays_ReuseOneVoice_AndAllocateNothing()
		{
			AudioConfig config = MakeConfig();
			config.PitchRange = new Vector2(0.9f, 1.1f);

			void PlayAndEnd()
			{
				for (int i = 0; i < 1000; i++)
				{
					_spawner.PlayAudio(config).Step(2d);
				}
			}

			PlayAndEnd();
			int allocations = GcAllocations.Count(PlayAndEnd);

			Assert.AreEqual(1, CountVoices());
			Assert.IsFalse(_spawner.IsPlaying(config));
			Assert.AreEqual(0, allocations);
		}

		// ------------------------------------------------------------------ a voice's clips

		[Test]
		public void PlayAllSequentially_PlaysEveryClipInOrder()
		{
			AudioClip[] clips = { MakeClip("A", 1), MakeClip("B", 1), MakeClip("C", 1) };
			AudioConfig config = MakeConfig(null, clips);
			config.PlayAllSequentially = true;

			AudioComponent voice = _spawner.PlayAudio(config);
			AudioSource source = voice.GetComponent<AudioSource>();

			for (int i = 0; i < clips.Length; i++)
			{
				Assert.AreSame(clips[i], source.clip);
				voice.Step(1d);
			}

			Assert.IsFalse(voice.IsPlaying);
		}

		[Test]
		public void ALoopingVoice_PlaysEveryClipOnceAPass_InTheSameOrderEachPass()
		{
			AudioClip[] clips = { MakeClip("A", 1), MakeClip("B", 1), MakeClip("C", 1) };
			AudioConfig config = MakeConfig(null, clips);
			config.Loop = true;

			AudioComponent voice = _spawner.PlayAudio(config);
			AudioSource source = voice.GetComponent<AudioSource>();

			var firstPass = new List<AudioClip>();
			for (int i = 0; i < clips.Length; i++)
			{
				firstPass.Add(source.clip);
				voice.Step(1d);
			}

			CollectionAssert.AreEquivalent(clips, firstPass);
			for (int i = 0; i < clips.Length; i++)
			{
				Assert.AreSame(firstPass[i], source.clip, "the second pass repeats the first");
				voice.Step(1d);
			}
		}

		[Test]
		public void AClip_IsHeldForItsLengthAtItsPitch()
		{
			AudioConfig config = MakeConfig();
			config.PitchRange = new Vector2(0.5f, 0.5f);

			AudioComponent voice = _spawner.PlayAudio(config);
			voice.Step(1.5d);
			Assert.IsTrue(voice.IsPlaying, "a second's clip at half speed plays for two");

			voice.Step(0.5d);
			Assert.IsFalse(voice.IsPlaying);
		}

		[Test]
		public void AStopDuringTheStartDelay_EndsTheVoiceAtOnce()
		{
			AudioConfig config = MakeConfig();
			config.StartAfterSeconds = 1f;
			config.FadeOutDuration   = 1f;

			AudioComponent voice = _spawner.PlayAudio(config);
			Assert.IsNull(voice.GetComponent<AudioSource>().clip, "nothing sounds during the start delay");

			voice.Stop();
			Assert.IsFalse(voice.IsPlaying);
		}

		[Test]
		public void APooledVoice_ThatEnded_CannotPlayAgain()
		{
			AudioComponent voice = _spawner.PlayAudio(MakeConfig());
			voice.Step(1d);

			LogAssert.Expect(LogType.Error, new Regex("has no config to play"));
			voice.Play();

			Assert.IsFalse(voice.IsPlaying);
		}

		[Test]
		public void AConfigWithNoClips_PlaysNothing()
		{
			AudioConfig config = MakeConfig();
			config.Clips = new List<AudioClip> { null };

			LogAssert.Expect(LogType.Error, new Regex("has no clips to play"));
			Assert.IsNull(_spawner.PlayAudio(config));
		}

		[Test]
		public void AVoiceOfYourOwn_KeepsItsConfig_AndPlaysAgain()
		{
			AudioComponent voice = Track(new GameObject("Own")).AddComponent<AudioComponent>();
			int stops = 0;
			voice.Init(MakeConfig(), TimeDomain.Unscaled, () => stops++);

			voice.Play();
			voice.Step(1d);
			Assert.AreEqual(1, stops);
			Assert.IsTrue(voice.gameObject.activeSelf, "a voice of your own stays where it is");

			voice.Play();
			Assert.IsTrue(voice.IsPlaying);
			voice.Step(1d);
			Assert.AreEqual(2, stops);
		}

		// ------------------------------------------------------------------ music

		[Test]
		public void StopMusic_DuringACrossfade_StopsBothTracks()
		{
			_spawner.PlayMusic(MakeMusic(_clip), 1f);
			_spawner.StepMusic(1d);
			_spawner.PlayMusic(MakeMusic(MakeClip("Second", 1)), 1f);
			_spawner.StepMusic(0.25d);

			_spawner.StopMusic(0.5f);
			_spawner.StepMusic(0.5d);

			Assert.IsNull(Music("Music_A").clip, "the track fading out stopped");
			Assert.IsNull(Music("Music_B").clip, "and so did the one fading in");
			Assert.IsNull(_spawner.CurrentMusic);
		}

		[Test]
		public void PlayMusic_AfterStopMusic_PlaysTheNewTrack_WhileTheOldOneFinishesItsFadeOut()
		{
			AudioClip second = MakeClip("Second", 1);
			AudioConfig secondMusic = MakeMusic(second);
			_spawner.PlayMusic(MakeMusic(_clip), 1f);
			_spawner.StepMusic(1d);
			_spawner.StopMusic(1f);
			_spawner.StepMusic(0.25d);

			_spawner.PlayMusic(secondMusic, 1f);
			Assert.AreEqual(0.75f, Music("Music_A").volume, 1e-5f, "the old track keeps its fade-out");

			_spawner.StepMusic(1d);
			Assert.IsNull(Music("Music_A").clip, "and stops at its end");
			Assert.AreSame(second, Music("Music_B").clip);
			Assert.AreEqual(1f, Music("Music_B").volume);
			Assert.AreSame(secondMusic, _spawner.CurrentMusic);
		}

		[Test]
		public void PlayMusic_TheTrackPlaying_ChangesNothing()
		{
			AudioConfig music = MakeMusic(_clip);
			_spawner.PlayMusic(music, 1f);
			_spawner.StepMusic(0.5d);

			_spawner.PlayMusic(music, 1f);

			Assert.AreEqual(0.5f, Music("Music_A").volume, 1e-5f, "its fade-in carries on");
			Assert.IsNull(Music("Music_B").clip);
		}

		// ------------------------------------------------------------------ ducking

		[Test]
		public void ADuck_LastsItsDuration_AndTheLatestDuckWins()
		{
			var mixer = Track(new GameObject("Mixer")).AddComponent<AudioMixerController>();

			mixer.DuckMusic(6f, 1f);
			Assert.AreEqual(6f, mixer.DuckDb);

			mixer.StepDuck(0.5d);
			mixer.DuckMusic(-3f, 1f);
			Assert.AreEqual(3f, mixer.DuckDb, "a duck's depth is its size");

			mixer.StepDuck(0.75d);
			Assert.AreEqual(3f, mixer.DuckDb, "the second duck lasts its own second");

			mixer.StepDuck(0.25d);
			Assert.AreEqual(0f, mixer.DuckDb);
		}
	}
}
