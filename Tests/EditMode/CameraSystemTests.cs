using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using AK.Core;
using AK.StateMachines;
using AK.Systems;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
#if UGFW_URP
using UnityEngine.Rendering.Universal;
#endif
using CameraType = AK.Systems.CameraType;
using Object = UnityEngine.Object;

namespace AK.Tests
{
	/// <summary>
	/// The camera system's binding, overlay stacks and spawning. Unity calls no Awake or Start in
	/// edit mode, so the tests call them. Overlay stacks are URP's, and their tests need it.
	/// </summary>
	public class CameraSystemTests
	{
		// A camera with its own state type, as a game's cameras have.
		private sealed class StateCamera : BaseCamera<StateCamera, StateCamera.Idle>
		{
			public sealed class Idle : BaseState<StateCamera> { }
		}

		private readonly List<Object> _objects = new();

		private CameraSystem _system;
		private CameraType   _main;

		[SetUp]
		public void SetUp()
		{
			_system = Track(new GameObject("Cameras")).AddComponent<CameraSystem>();
			Call(_system, "Awake");
			_main = MakeType("Main");
		}

		[TearDown]
		public void TearDown()
		{
			for (int i = _objects.Count - 1; i >= 0; i--)
			{
				if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
			}

			_objects.Clear();
		}

		// ---------------------------------------------------------------- set-up helpers

		private T Track<T>(T obj) where T : Object
		{
			_objects.Add(obj);
			return obj;
		}

		private static void Call(Component component, string message)
		{
			component.GetType().GetMethod(message, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(component, null);
		}

		private CameraType MakeType(string name)
		{
			var type = Track(ScriptableObject.CreateInstance<CameraType>());
			type.name = name;
			type.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);
			return type;
		}

		private CameraDefinition Definition(CameraRole role, CameraType type, CameraType baseType = null, int layerOrder = 0,
		                                    GameObject prefab = null, bool spawnOnStart = false)
		{
			var definition = Track(ScriptableObject.CreateInstance<CameraDefinition>());
			definition.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);

			var serialized = new SerializedObject(definition);
			serialized.FindProperty("_cameraType").objectReferenceValue     = type;
			serialized.FindProperty("_role").intValue                       = (int)role;
			serialized.FindProperty("_baseCameraType").objectReferenceValue = baseType;
			serialized.FindProperty("_layerOrder").intValue                 = layerOrder;
			serialized.FindProperty("_prefab").objectReferenceValue         = prefab;
			serialized.FindProperty("_spawnOnStart").boolValue              = spawnOnStart;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return definition;
		}

		// A camera as a scene would hold it: awake, configured by the definition, unbound.
		private T MakeCamera<T>(string name, CameraDefinition definition) where T : Component, ISpawnableCamera
		{
			var camera = Track(new GameObject(name)).AddComponent<T>();
			Call(camera, "Awake");
			camera.ApplyDefinition(definition);
			return camera;
		}

		private BaseCamera Base(string name, CameraType type = null) =>
			MakeCamera<BaseCamera>(name, Definition(CameraRole.Base, type ?? _main));

#if UGFW_URP
		private BaseCamera Overlay(string name, int layerOrder = 0) =>
			MakeCamera<BaseCamera>(name, Definition(CameraRole.Overlay, null, _main, layerOrder));

		private static List<Camera> Stack(IGameCamera baseCamera) => baseCamera.Camera.GetUniversalAdditionalCameraData().cameraStack;

		// ---------------------------------------------------------------- overlay stacks

		[Test]
		public void AnOverlay_StacksOnItsBase_WhicheverBindsFirst_InLayerOrder()
		{
			BaseCamera early = Overlay("early", layerOrder: 2);
			BaseCamera late  = Overlay("late", layerOrder: 1);
			BaseCamera main  = Base("main");

			early.BindToSystem(_system);
			main.BindToSystem(_system);
			late.BindToSystem(_system);

			CollectionAssert.AreEqual(new[] { late.Camera, early.Camera }, Stack(main));
		}

		[Test]
		public void TheOverlaysOfABaseThatUnbinds_MoveToTheNextBaseOfItsKind()
		{
			BaseCamera first   = Base("first");
			BaseCamera overlay = Overlay("overlay");
			first.BindToSystem(_system);
			overlay.BindToSystem(_system);

			_system.UnbindCamera(first);

			CollectionAssert.IsEmpty(Stack(first));

			BaseCamera second = Base("second");
			second.BindToSystem(_system);

			CollectionAssert.AreEqual(new[] { overlay.Camera }, Stack(second));
		}

		[Test]
		public void TheOverlaysOfADestroyedBase_MoveToTheNextBaseOfItsKind()
		{
			BaseCamera first   = Base("first");
			BaseCamera overlay = Overlay("overlay");
			first.BindToSystem(_system);
			overlay.BindToSystem(_system);

			Object.DestroyImmediate(first.gameObject);
			_system.UnbindCamera(first);

			BaseCamera second = Base("second");
			second.BindToSystem(_system);

			CollectionAssert.AreEqual(new[] { overlay.Camera }, Stack(second));
		}

		[Test]
		public void ADisabledOverlay_StaysOffTheNextBase_UntilEnabled()
		{
			BaseCamera first   = Base("first");
			BaseCamera overlay = Overlay("overlay");
			first.BindToSystem(_system);
			overlay.BindToSystem(_system);

			overlay.Disable(disableGameObject: false);
			CollectionAssert.IsEmpty(Stack(first));

			_system.UnbindCamera(first);
			BaseCamera second = Base("second");
			second.BindToSystem(_system);
			CollectionAssert.IsEmpty(Stack(second));

			overlay.Enable(enableGameObject: false);
			CollectionAssert.AreEqual(new[] { overlay.Camera }, Stack(second));
			Assert.IsTrue(overlay.gameObject.activeSelf);
		}

		[Test]
		public void ABaseThatWasReplaced_LeavesTheStackOfItsReplacementAlone()
		{
			BaseCamera first   = Base("first");
			BaseCamera second  = Base("second");
			BaseCamera overlay = Overlay("overlay");
			first.BindToSystem(_system);
			second.BindToSystem(_system);
			overlay.BindToSystem(_system);

			_system.UnbindCamera(first);

			CollectionAssert.AreEqual(new[] { overlay.Camera }, Stack(second));
		}

		[Test]
		public void EnableAndDisable_ActOnThisCamera()
		{
			BaseCamera main  = Base("main");
			BaseCamera one   = Overlay("one");
			BaseCamera other = Overlay("other");
			main.BindToSystem(_system);
			one.BindToSystem(_system);
			other.BindToSystem(_system);

			one.Disable();

			Assert.IsFalse(one.gameObject.activeSelf);
			Assert.IsTrue(other.gameObject.activeSelf);
			CollectionAssert.AreEqual(new[] { other.Camera }, Stack(main));

			one.Enable();

			Assert.IsTrue(one.gameObject.activeSelf);
			CollectionAssert.AreEquivalent(new[] { one.Camera, other.Camera }, Stack(main));
		}
#endif

		// ---------------------------------------------------------------- binding

		[Test]
		public void ACameraWithoutASystem_EnablesAndDisablesOnlyItsGameObject()
		{
			BaseCamera camera = Base("alone");

			camera.Disable(disableGameObject: false);
			Assert.IsTrue(camera.gameObject.activeSelf);

			camera.Disable();
			Assert.IsFalse(camera.gameObject.activeSelf);

			camera.Enable();
			Assert.IsTrue(camera.gameObject.activeSelf);
		}

		[Test]
		public void ACameraWithTheVirtualRole_ThatIsNoVirtualCamera_IsBoundByTypeAndIdentityOnly()
		{
			CameraType odd    = MakeType("Odd");
			BaseCamera camera = MakeCamera<BaseCamera>("odd", Definition(CameraRole.Virtual, odd));

			LogAssert.Expect(LogType.Warning, new Regex(@"^\[CameraSystem\] 'odd' "));
			camera.BindToSystem(_system);

			Assert.AreSame(camera, _system.GetCamera(odd));
		}

		// ---------------------------------------------------------------- spawning

		private void UseRegistry(params CameraDefinition[] definitions)
		{
			var registry = Track(ScriptableObject.CreateInstance<CameraRegistry>());
			foreach (CameraDefinition definition in definitions)
			{
				registry.Registry.Add(definition);
			}

			var serialized = new SerializedObject(_system);
			serialized.FindProperty("_cameraRegistry").objectReferenceValue = registry;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		// A prefab stand-in: a scene object carrying a generic camera, its Camera wired as the
		// Inspector wires it. Its own config is a base camera of no type; a definition overrides it.
		private GameObject GenericCameraPrefab(string name)
		{
			var camera = Track(new GameObject(name)).AddComponent<StateCamera>();
			var serialized = new SerializedObject(camera);
			serialized.FindProperty("_camera").objectReferenceValue = camera.GetComponent<Camera>();
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return camera.gameObject;
		}

		[Test]
		public void AGenericCamera_SpawnsConfiguredByItsDefinition_AndBound()
		{
			BaseCamera main = Base("main");
			main.BindToSystem(_system);
			CameraType hud = MakeType("Hud");
			UseRegistry(Definition(CameraRole.Overlay, hud, _main, prefab: GenericCameraPrefab("HudPrefab")));

			var spawned = _system.SpawnCamera<StateCamera>(hud);

			Assert.IsNotNull(spawned);
			Assert.AreEqual(CameraRole.Overlay, spawned.Role);
			Assert.AreSame(spawned, _system.GetCamera(hud));
#if UGFW_URP
			CollectionAssert.AreEqual(new[] { spawned.Camera }, Stack(main));

			spawned.Disable(disableGameObject: false);
			CollectionAssert.IsEmpty(Stack(main), "bound to the system that spawned it");
#endif
		}

		[Test]
		public void StartupSpawns_SkipATypeASceneCameraHas_GenericCamerasIncluded()
		{
			CameraType placed  = MakeType("Placed");
			CameraType missing = MakeType("Missing");
			MakeCamera<StateCamera>("placed", Definition(CameraRole.Base, placed));
			UseRegistry(
				Definition(CameraRole.Base, placed, prefab: GenericCameraPrefab("PlacedPrefab"), spawnOnStart: true),
				Definition(CameraRole.Base, missing, prefab: GenericCameraPrefab("MissingPrefab"), spawnOnStart: true));

			Call(_system, "Start");

			Assert.AreEqual(1, _system.transform.childCount);
			Assert.AreEqual("MissingPrefab", _system.transform.GetChild(0).name);
			Assert.IsNotNull(_system.GetCamera(missing));
			Assert.IsNull(_system.GetCamera(placed), "the scene camera binds itself, in its Start");
		}
	}
}
