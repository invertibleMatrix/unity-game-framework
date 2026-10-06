using System;
using System.Collections.Generic;
using AK.Kernel.Timing;
using AK.Systems;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests.Support
{
	/// <summary>
	/// Edit-mode fixture for the UISystem. Builds a system with an empty repository and a
	/// Reflex container, and offers factories for prefab-like views (serialized fields set
	/// through <see cref="SerializedObject"/> exactly as the Inspector would). Views carry no
	/// animation strategy, so every show/close settles synchronously.
	/// </summary>
	public sealed class UISystemHarness : IDisposable
	{
		private readonly List<Object> _created = new();

		public UISystem         System     { get; }
		public UIViewRepository Repository { get; }
		public Container        Container  { get; }
		public Transform        Root       { get; }

		/// <summary>The system as its views see it.</summary>
		public IViewHost Host => System;

		public UISystemHarness(int poolCapacityPerKind = ViewPool.DefaultCapacityPerKind)
		{
			Container = new ContainerBuilder().SetName("UISystemHarness").Build();

			// The root stays ACTIVE: a GameObject destroyed while inactive in the hierarchy never
			// receives OnDestroy, and the external-destroy contract depends on that callback.
			// Views are instantiated inactive (their prefabs are) and the system activates them.
			var root = new GameObject("UIRoot", typeof(RectTransform));
			_created.Add(root);
			Root = root.transform;

			Repository = ScriptableObject.CreateInstance<UIViewRepository>();
			_created.Add(Repository);

			var systemGo = new GameObject("UISystem");
			systemGo.SetActive(false);
			_created.Add(systemGo);
			System = systemGo.AddComponent<UISystem>();
			AttributeInjector.Inject(System, Container);

			var so = new SerializedObject(System);
			so.FindProperty("_repository").objectReferenceValue = Repository;
			so.FindProperty("_viewsContainer").objectReferenceValue = Root;
			so.FindProperty("_spawnDefaultOverlayView").boolValue = false;
			so.FindProperty("_ensureEventSystem").boolValue = false;
			so.FindProperty("_poolCapacityPerKind").intValue = poolCapacityPerKind;
			so.ApplyModifiedPropertiesWithoutUndo();

			System.EnsureInitialized();
		}

		public void Dispose()
		{
			System?.Dispose();
			Container?.Dispose();

			foreach (var o in _created)
			{
				if (o != null) Object.DestroyImmediate(o);
			}

			_created.Clear();
		}

		public void Track(Object o) => _created.Add(o);

		/// <summary>A repository prefab. Screens get a Canvas + UIViewChannel.</summary>
		public T MakePrefab<T>(string viewId = "", bool screen = false, bool pooled = false, bool allowMultiple = false,
		                       ViewStackBehaviour behaviour = ViewStackBehaviour.DoNothing,
		                       bool parallelWithPrevious = false,
		                       ChildCloseOrder childCloseOrder = ChildCloseOrder.ParentFirst,
		                       UIChannel channel = UIChannel.HUD) where T : UIView
		{
			var go = new GameObject(typeof(T).Name + (viewId.Length > 0 ? "#" + viewId : ""), typeof(RectTransform), typeof(CanvasGroup));
			go.SetActive(false);
			_created.Add(go);

			if (screen)
			{
				go.AddComponent<Canvas>();
				var ch = go.AddComponent<UIViewChannel>();
				var cso = new SerializedObject(ch);
				cso.FindProperty("_sortOrder").intValue = (int)channel;
				cso.ApplyModifiedPropertiesWithoutUndo();
			}

			var view = go.AddComponent<T>();
			var so = new SerializedObject(view);
			so.FindProperty("_viewId").stringValue = viewId;
			so.FindProperty("_returnToPoolOnClose").boolValue = pooled;
			so.FindProperty("_allowMultipleInstances").boolValue = allowMultiple;
			so.FindProperty("_stackBehaviour").enumValueIndex = (int)behaviour;
			so.FindProperty("_playInParallelWithPrevious").boolValue = parallelWithPrevious;
			so.FindProperty("_childCloseOrder").enumValueIndex = (int)childCloseOrder;
			so.ApplyModifiedPropertiesWithoutUndo();

			var repo = new SerializedObject(Repository);
			var views = repo.FindProperty("_views");
			views.arraySize++;
			views.GetArrayElementAtIndex(views.arraySize - 1).objectReferenceValue = view;
			repo.ApplyModifiedPropertiesWithoutUndo();

			return view;
		}

		/// <summary>
		/// Gives a prefab an animation that never finishes on its own, so a show or hide stays
		/// in flight until the test releases it. Instances get their own copy; read it back with
		/// <c>instance.GetComponent&lt;HoldAnimation&gt;()</c>.
		/// </summary>
		public HoldAnimation AddHoldAnimation(UIView prefab)
		{
			var hold = prefab.gameObject.AddComponent<HoldAnimation>();
			var so = new SerializedObject(prefab);
			so.FindProperty("_animationComponent").objectReferenceValue = hold;
			so.ApplyModifiedPropertiesWithoutUndo();
			return hold;
		}

		/// <summary>Pre-placed child under <paramref name="parent"/>, listed in its Static Fragments.</summary>
		public T AddStaticChild<T>(UIView parent, string viewId = "", bool showOnStart = false, bool template = false,
		                           ViewStackBehaviour behaviour = ViewStackBehaviour.DoNothing) where T : UIView
		{
			var go = new GameObject("Static" + typeof(T).Name, typeof(RectTransform), typeof(CanvasGroup));
			go.transform.SetParent(parent.transform, false);
			go.SetActive(false);
			var view = go.AddComponent<T>();

			var vso = new SerializedObject(view);
			vso.FindProperty("_viewId").stringValue = viewId;
			vso.FindProperty("_isTemplate").boolValue = template;
			vso.FindProperty("_stackBehaviour").enumValueIndex = (int)behaviour;
			vso.ApplyModifiedPropertiesWithoutUndo();

			var pso = new SerializedObject(parent);
			var list = pso.FindProperty("_staticViews");
			list.arraySize++;
			var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
			entry.FindPropertyRelative("View").objectReferenceValue = view;
			entry.FindPropertyRelative("ShowOnStart").boolValue = showOnStart;
			entry.FindPropertyRelative("SetActive").boolValue = false;
			pso.ApplyModifiedPropertiesWithoutUndo();

			return view;
		}

		/// <summary>Asserts a task settled synchronously and rethrows its failure, if any.</summary>
		public static void Complete(UniTask task)
		{
			Assert.That(task.Status.IsCompleted(), Is.True, "synchronous settle expected (no animation strategies in tests)");
			task.GetAwaiter().GetResult();
		}

		public static T Complete<T>(UniTask<T> task)
		{
			Assert.That(task.Status.IsCompleted(), Is.True, "synchronous settle expected (no animation strategies in tests)");
			return task.GetAwaiter().GetResult();
		}
	}

	/// <summary>
	/// Records every lifecycle hook and interactability change in order. The recorded
	/// sequence is the contract the stack-policy and cascade tests pin.
	/// </summary>
	public class RecordingView : UIView
	{
		public readonly List<string> Log = new();

		public override void OnPrepareShow()        => Log.Add("PrepareShow");
		public override void RegisterResources()    => Log.Add("Register");
		public override void OnShow()               => Log.Add("Show");
		public override void OnPause()              => Log.Add("Pause");
		public override void OnResume()             => Log.Add("Resume");
		public override void OnPrepareHide()        => Log.Add("PrepareHide");
		public override void OnHide()               => Log.Add("Hide");
		public override void UnRegisterResources()  => Log.Add("Unregister");
		public override void OnReset()              { Log.Add("Reset"); base.OnReset(); }
		public override void OnBeforePool()         => Log.Add("BeforePool");

		public bool Interactable => CanvasGroup != null && CanvasGroup.interactable && CanvasGroup.blocksRaycasts;

		public string Trace => string.Join(" ", Log);

		public void Clear() => Log.Clear();
	}

	public sealed class RecordingScreen : RecordingView { }
	public sealed class RecordingFragment : RecordingView { }
	public sealed class RecordingFragmentB : RecordingView { }

	/// <summary>A pooled parent that closes one child itself in <see cref="OnBeforePool"/>, the way game views do.</summary>
	public sealed class ChildClosingParent : RecordingView
	{
		public UIView ChildToClose;

		public override void OnBeforePool()
		{
			base.OnBeforePool();
			if (ChildToClose != null) UISystem.Close(ChildToClose, CloseOptions.Now);
		}
	}

	/// <summary>
	/// An entrance/exit that stays in flight until <see cref="Release"/> and honours the
	/// cancellation token, so tests can observe and interrupt the Showing and Hiding states.
	/// </summary>
	public sealed class HoldAnimation : AnimationStrategyComponent, IAsyncAnimationStrategy
	{
		private UniTaskCompletionSource _pending;

		/// <summary>How many entrances or exits were started.</summary>
		public int Starts { get; private set; }

		/// <summary>The time the last entrance or exit was asked to run on.</summary>
		public TimeDomain LastTime { get; private set; }

		public override DG.Tweening.Tween PlayShowAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time,
		                                                    Vector2 entryPos = default) => null;

		public override DG.Tweening.Tween PlayHideAnimation(RectTransform target, CanvasGroup canvasGroup, TimeDomain time) => null;

		public UniTask PlayShowAsync(RectTransform target, CanvasGroup canvasGroup, TimeDomain time, Vector2 entryPos = default,
		                             System.Threading.CancellationToken ct = default) => Hold(time, ct);

		public UniTask PlayHideAsync(RectTransform target, CanvasGroup canvasGroup, TimeDomain time,
		                             System.Threading.CancellationToken ct = default) => Hold(time, ct);

		/// <summary>Finishes the animation in flight.</summary>
		public void Release() => _pending?.TrySetResult();

		private UniTask Hold(TimeDomain time, System.Threading.CancellationToken ct)
		{
			Starts++;
			LastTime = time;
			var pending = new UniTaskCompletionSource();
			_pending = pending;
			ct.Register(() => pending.TrySetCanceled(ct));
			return pending.Task;
		}
	}
}
