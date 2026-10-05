using System;
using System.Collections.Generic;
using System.Reflection;
using AK.Kernel.Timing;
using AK.Systems;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests.Support
{
	/// <summary>
	/// A running UISystem for play-mode tests, set up the way a scene sets one up. It wakes with
	/// its input blocker registered and ticking, and makes the scene's EventSystem with the input
	/// module the project's input handling calls for. Views are added as prefab-like inactive
	/// objects before the first show.
	///
	/// Private serialized fields are set through reflection rather than SerializedObject, so the
	/// fixture also builds in a player. A renamed field fails the fixture loudly.
	/// </summary>
	internal sealed class LiveUISystem : IDisposable
	{
		private readonly List<Object>     _created = new();
		private readonly UIViewRepository _repository;

		public UISystem  System    { get; }
		public Container Container { get; }

		public LiveUISystem()
		{
			Container = new ContainerBuilder().SetName(nameof(LiveUISystem)).Build();

			var root = new GameObject("UIRoot", typeof(RectTransform));
			_created.Add(root);

			_repository = ScriptableObject.CreateInstance<UIViewRepository>();
			_created.Add(_repository);

			// Configured while inactive, so Awake sees the fields as a scene would have serialized them.
			var go = new GameObject(nameof(UISystem));
			go.SetActive(false);
			_created.Add(go);

			System = go.AddComponent<UISystem>();
			AttributeInjector.Inject(System, Container);
			SetField(System, "_repository", _repository);
			SetField(System, "_viewsContainer", root.transform);
			SetField(System, "_spawnDefaultOverlayView", false);

			go.SetActive(true);
		}

		public void Dispose()
		{
			System.Dispose();
			Container.Dispose();

			foreach (var o in _created)
			{
				if (o != null) Object.DestroyImmediate(o);
			}

			_created.Clear();
		}

		/// <summary>Destroys <paramref name="o"/> with the system.</summary>
		public void Track(Object o) => _created.Add(o);

		/// <summary>Sets the time the system runs its views on.</summary>
		public void SetTimeDomain(TimeDomain domain) => SetField(System, "_timeDomain", domain);

		/// <summary>Gives <paramref name="prefab"/> <paramref name="strategy"/> as its entrance and exit.</summary>
		public void SetAnimation(UIView prefab, AnimationStrategy strategy) => SetField(prefab, "_animationStrategy", strategy);

		/// <summary>Sets what a show of <paramref name="prefab"/> does to the view below it.</summary>
		public void SetStackBehaviour(UIView prefab, ViewStackBehaviour behaviour) => SetField(prefab, "_stackBehaviour", behaviour);

		/// <summary>
		/// A child pre-placed under <paramref name="parent"/> and listed in its Static Fragments.
		/// With <paramref name="showOnStart"/> it shows <paramref name="showDelay"/> seconds after
		/// the parent does; without, it waits for a show of its own.
		/// </summary>
		public T AddStaticChild<T>(UIView parent, bool showOnStart, float showDelay = 0f) where T : UIView
		{
			var go = new GameObject("Static" + typeof(T).Name, typeof(RectTransform), typeof(CanvasGroup));
			go.transform.SetParent(parent.transform, false);
			go.SetActive(false);
			var view = go.AddComponent<T>();

			var entries = (List<StaticViewEntry>)GetField(parent, "_staticViews");
			entries.Add(new StaticViewEntry { View = view, ShowOnStart = showOnStart, ShowOnStartDelay = showDelay });
			return view;
		}

		/// <summary>
		/// A screen prefab in the system's repository: an inactive object carrying a Canvas and a
		/// UIViewChannel. Add every prefab before the first show; the system indexes them then.
		/// </summary>
		public T AddScreenPrefab<T>() where T : UIView
		{
			var go = new GameObject(typeof(T).Name, typeof(RectTransform));
			go.SetActive(false);
			_created.Add(go);

			go.AddComponent<CanvasGroup>();
			go.AddComponent<Canvas>();
			go.AddComponent<UIViewChannel>();
			var view = go.AddComponent<T>();

			((List<UIView>)GetField(_repository, "_views")).Add(view);
			return view;
		}

		/// <summary>
		/// A fragment prefab in the system's repository: an inactive object with no canvas of its
		/// own, shown inside a screen. Add every prefab before the first show.
		/// </summary>
		public T AddFragmentPrefab<T>() where T : UIView
		{
			var go = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(CanvasGroup));
			go.SetActive(false);
			_created.Add(go);

			var view = go.AddComponent<T>();

			((List<UIView>)GetField(_repository, "_views")).Add(view);
			return view;
		}

		/// <summary>The private field <paramref name="name"/>, declared on the target's type or any type it derives from.</summary>
		private static FieldInfo Field(object target, string name)
		{
			for (Type type = target.GetType(); type != null; type = type.BaseType)
			{
				FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				if (field != null) return field;
			}

			throw new MissingFieldException(target.GetType().FullName, name);
		}

		/// <summary>Sets the private field <paramref name="name"/> of <paramref name="target"/>, as the Inspector would.</summary>
		public static void SetField(object target, string name, object value) => Field(target, name).SetValue(target, value);

		private static object GetField(object target, string name) => Field(target, name).GetValue(target);
	}
}
