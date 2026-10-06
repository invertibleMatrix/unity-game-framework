using System.Collections.Generic;
using System.Text.RegularExpressions;
using AK.Core;
using AK.Tests.Support;
using AK.Utilities.Particles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AK.Tests.Utilities
{
	/// <summary>
	/// The particle spawner in edit mode, where particle systems don't simulate: these tests
	/// hand effects out and take them back without waiting on particles to die. The Play-mode
	/// tests cover effects stopping by themselves.
	/// </summary>
	public class ParticleSpawnerTests
	{
		private sealed class OtherEffect : ParticleComponent
		{
		}

		private readonly List<Object> _created = new();

		private ParticleSpawner   _spawner;
		private ParticleComponent _prefab;

		[SetUp]
		public void SetUp()
		{
			_prefab  = MakePrefab("Effect");
			_spawner = Track(new GameObject("Spawner")).AddComponent<ParticleSpawner>();
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

		/// <summary>An effect prefab: an inactive template whose root system doesn't play on awake.</summary>
		private ParticleComponent MakePrefab(string name, bool colorTarget = false)
		{
			GameObject go = Track(new GameObject(name));
			go.SetActive(false);

			var system = go.AddComponent<ParticleSystem>();
			ParticleSystem.MainModule main = system.main;
			main.playOnAwake = false;
			main.startColor  = Color.white;

			var effect = go.AddComponent<ParticleComponent>();
			var serialized = new SerializedObject(effect);
			serialized.FindProperty("_rootParticle").objectReferenceValue = system;

			if (colorTarget)
			{
				SerializedProperty targets = serialized.FindProperty("_colorTargets");
				targets.arraySize = 1;
				targets.GetArrayElementAtIndex(0).objectReferenceValue = system;
			}

			serialized.ApplyModifiedPropertiesWithoutUndo();
			return effect;
		}

		private ParticleConfigBase MakeConfig(ParticleComponent prefab = null, int poolSize = 1)
		{
			ParticleConfigBase config = Track(ScriptableObject.CreateInstance<ParticleConfigBase>());
			config.Prefab          = prefab != null ? prefab : _prefab;
			config.InitialPoolSize = poolSize;
			return config;
		}

		private int CountEffects() => _spawner.GetComponentsInChildren<ParticleComponent>(true).Length;

		[Test]
		public void Spawn_AConfigOutsideTheRegistry_HandsOutAnEffect_ThatWaitsToBeShown()
		{
			ParticleComponent effect = _spawner.Spawn(MakeConfig());

			Assert.IsNotNull(effect, "a config needs no registry to have a pool");
			Assert.IsFalse(effect.gameObject.activeSelf);
		}

		[Test]
		public void Pools_StartWithTheirConfigsSizes()
		{
			ParticleConfigBase two = MakeConfig(poolSize: 2);
			ParticleConfigBase three = MakeConfig(poolSize: 3);
			ParticlesRegistry registry = Track(ScriptableObject.CreateInstance<ParticlesRegistry>());
			foreach (ParticleConfigBase config in new[] { two, three })
			{
				config.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);
				Assert.IsTrue(registry.Editor_TryTrack(config));
			}

			var serialized = new SerializedObject(_spawner);
			serialized.FindProperty("_particlesRegistry").objectReferenceValue = registry;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			_spawner.Spawn(two);

			Assert.AreEqual(5, CountEffects(), "two and three made ahead, and none on demand");
		}

		[Test]
		public void Stop_BeforeShow_GivesTheEffectBack()
		{
			ParticleConfigBase config = MakeConfig();
			config.MaxActiveInstances = 1;
			int stops = 0;

			ParticleComponent effect = _spawner.Spawn(config, () => stops++);
			effect.Stop();

			Assert.AreEqual(1, stops);
			Assert.AreSame(effect, _spawner.Spawn(config), "back in its pool, and off its config's cap");
		}

		[Test]
		public void TheStopCallback_RunsOnceTheEffectIsBack()
		{
			ParticleComponent effect = null;
			bool backFirst = false;
			effect = _spawner.Spawn(MakeConfig(), () => backFirst = !effect.gameObject.activeSelf && effect.Config == null);

			effect.Stop();

			Assert.IsTrue(backFirst);
		}

		[Test]
		public void SpawnAsTheWrongType_GivesTheEffectBack()
		{
			ParticleConfigBase config = MakeConfig();
			config.MaxActiveInstances = 1;

			LogAssert.Expect(LogType.Error, new Regex("spawned a ParticleComponent"));
			Assert.IsNull(_spawner.Spawn<OtherEffect>(config));

			Assert.IsNotNull(_spawner.Spawn(config), "the effect went back, so its config's cap has room");
		}

		[Test]
		public void StopAll_GivesBackEffectsNotShownYet()
		{
			ParticleConfigBase config = MakeConfig();
			config.MaxActiveInstances = 2;
			_spawner.Spawn(config);
			_spawner.Spawn(config);

			_spawner.StopAll(config);

			Assert.IsNotNull(_spawner.Spawn(config));
			Assert.AreEqual(2, CountEffects(), "both were reused");
		}

		[Test]
		public void ShowWithAColor_WithNoColorTargets_ShowsTheEffect()
		{
			ParticleComponent effect = _spawner.Spawn(MakeConfig());

			effect.Show(Vector3.zero, Quaternion.identity, Color.red);

			Assert.IsTrue(effect.gameObject.activeSelf);
		}

		[Test]
		public void ShowWithAColor_TintsItsColorTargets_UntilTheEffectIsReset()
		{
			ParticleComponent effect = _spawner.Spawn(MakeConfig(MakePrefab("Tinted", colorTarget: true)));

			effect.Show(Vector3.zero, Quaternion.identity, Color.red);
			Assert.AreEqual(Color.red, effect.RootParticle.main.startColor.color);

			effect.ResetState();
			Assert.AreEqual(Color.white, effect.RootParticle.main.startColor.color);
		}

		[Test]
		public void APooledEffect_Recycled_CannotShowAgain()
		{
			ParticleComponent effect = _spawner.Spawn(MakeConfig());
			effect.Stop();

			LogAssert.Expect(LogType.Error, new Regex("has no config to show"));
			effect.Show();

			Assert.IsFalse(effect.gameObject.activeSelf);
		}

		[Test]
		public void AThousandSpawnsAndStops_ReuseOneEffect_AndAllocateNothing()
		{
			ParticleConfigBase config = MakeConfig();

			void SpawnAndStop()
			{
				for (int i = 0; i < 1000; i++)
				{
					_spawner.Spawn(config).Stop();
				}
			}

			SpawnAndStop();
			int allocations = GcAllocations.Count(SpawnAndStop);

			Assert.AreEqual(1, CountEffects());
			Assert.AreEqual(0, allocations);
		}
	}
}
