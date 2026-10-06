using System;
using System.Collections.Generic;
using System.Reflection;
using AK.Core;
using AK.Tests.Support;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests
{
	public class AppStateMachineTests
	{
		private readonly List<string> _log     = new();
		private readonly List<Object> _objects = new();

		private Container       _container;
		private AppStateMachine _machine;

		[SetUp]
		public void SetUp()
		{
			_container = new ContainerBuilder().SetName(nameof(AppStateMachineTests)).Build();

			var host = new GameObject(nameof(AppStateMachineTests));
			_objects.Add(host);
			_machine = host.AddComponent<AppStateMachine>();
			AttributeInjector.Inject(_machine, _container);

			_machine.OnStateChange += state => _log.Add("changed " + state.name);
			_machine.OnTransition += info => _log.Add(
				$"reported {Name(info.From)}>{Name(info.To)}{(info.PreviousPaused ? " paused" : "")}{(info.Resumed ? " resumed" : "")}");
		}

		[TearDown]
		public void TearDown()
		{
			foreach (Object o in _objects)
			{
				Object.DestroyImmediate(o);
			}

			_objects.Clear();
			_log.Clear();
			_container.Dispose();
		}

		// ---------------------------------------------------------------- boot

		[Test]
		public void TheBootState_IsEnteredAndReported_LikeAnyOther()
		{
			LoggingState boot = State<LoggingState>("boot");
			var serialized = new SerializedObject(_machine);
			serialized.FindProperty("_bootState").objectReferenceValue = boot;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			// Start boots the machine; Unity doesn't call it in edit mode.
			typeof(AppStateMachine).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_machine, null);

			CollectionAssert.AreEqual(new[] { "enter boot", "entered boot", "changed boot", "reported none>boot" }, _log);
			Assert.AreSame(boot, _machine.CurrentState);
			Assert.IsNull(_machine.PreviousState);
			Assert.AreSame(_machine, boot.Machine);
			Assert.AreSame(_container, boot.InjectedContainer, "injected from the machine's container");
		}

		// ---------------------------------------------------------------- one transition at a time

		[Test]
		public void AChangeRequestedInOnEnter_RunsOnceTheEntryIsReported()
		{
			LoggingState a = State<LoggingState>("a");
			LoggingState b = State<LoggingState>("b");
			a.Entered = () => _machine.ChangeState(b);

			_machine.ChangeState(a);

			CollectionAssert.AreEqual(new[]
			{
				"enter a", "entered a", "changed a", "reported none>a",
				"exit a", "enter b", "entered b", "changed b", "reported a>b",
			}, _log);
			Assert.AreSame(b, _machine.CurrentState);
			Assert.AreSame(a, _machine.PreviousState);
		}

		[Test]
		public void AChangeRequestedByAListener_RunsOnceTheTransitionIsReported()
		{
			LoggingState a = State<LoggingState>("a");
			LoggingState b = State<LoggingState>("b");
			_machine.OnStateChange += state =>
			{
				if (state == a) _machine.ChangeState(b);
			};

			_machine.ChangeState(a);

			CollectionAssert.AreEqual(new[]
			{
				"enter a", "entered a", "changed a", "reported none>a",
				"exit a", "enter b", "entered b", "changed b", "reported a>b",
			}, _log);
		}

		[Test]
		public void ChangesRequestedDuringATransition_RunInTheOrderMade()
		{
			LoggingState a = State<LoggingState>("a");
			LoggingState b = State<LoggingState>("b");
			LoggingState c = State<LoggingState>("c");
			a.Entered = () =>
			{
				_machine.ChangeState(b);
				_machine.ChangeState(c);
			};

			_machine.ChangeState(a);

			CollectionAssert.AreEqual(new[]
			{
				"enter a", "entered a", "changed a", "reported none>a",
				"exit a", "enter b", "entered b", "changed b", "reported a>b",
				"exit b", "enter c", "entered c", "changed c", "reported b>c",
			}, _log);
			Assert.AreSame(c, _machine.CurrentState);
		}

		[Test]
		public void AGoBackRequestedDuringATransition_TakesThePauseStackAsItIsWhenItRuns()
		{
			LoggingState board = State<LoggingState>("board");
			LoggingState popup = State<LoggingState>("popup");
			_machine.ChangeState(board);
			_log.Clear();
			popup.Entered = () => _machine.TryGoBack();

			_machine.ChangeState(popup, pauseCurrent: true);

			CollectionAssert.AreEqual(new[]
			{
				"pause board", "enter popup", "entered popup", "changed popup", "reported board>popup paused",
				"exit popup", "resume board", "reported popup>board resumed",
			}, _log);
			Assert.AreSame(board, _machine.CurrentState);
			CollectionAssert.IsEmpty(_machine.PausedStates);
		}

		[Test]
		public void TryGoBack_WithNothingPaused_DoesNothing()
		{
			LoggingState a = State<LoggingState>("a");
			_machine.TryGoBack();
			CollectionAssert.IsEmpty(_log, "before any state");

			a.Entered = () => _machine.TryGoBack();
			_machine.ChangeState(a);

			CollectionAssert.AreEqual(new[] { "enter a", "entered a", "changed a", "reported none>a" }, _log, "requested during a transition");
			Assert.AreSame(a, _machine.CurrentState);
		}

		// ---------------------------------------------------------------- pause and resume

		[Test]
		public void PausedStates_ResumeLastFirst_WithoutEnteringAgain()
		{
			LoggingState board = State<LoggingState>("board");
			LoggingState menu  = State<LoggingState>("menu");
			LoggingState popup = State<LoggingState>("popup");
			_machine.ChangeState(board);
			_machine.ChangeState(menu, pauseCurrent: true);
			_machine.ChangeState(popup, pauseCurrent: true);
			CollectionAssert.AreEqual(new AppState[] { board, menu }, _machine.PausedStates);
			_log.Clear();

			_machine.TryGoBack();
			_machine.TryGoBack();

			CollectionAssert.AreEqual(new[]
			{
				"exit popup", "resume menu", "reported popup>menu resumed",
				"exit menu", "resume board", "reported menu>board resumed",
			}, _log, "a resume doesn't fire OnStateChange");
			CollectionAssert.IsEmpty(_machine.PausedStates);
		}

		[Test]
		public void ChangingToTheCurrentState_ExitsAndEntersItAgain()
		{
			LoggingState a = State<LoggingState>("a");
			_machine.ChangeState(a);
			_log.Clear();

			_machine.ChangeState(a);

			CollectionAssert.AreEqual(new[] { "exit a", "enter a", "entered a", "changed a", "reported a>a" }, _log);
			Assert.AreSame(a, _machine.PreviousState, "a restarted state was its own previous state");
		}

		// ---------------------------------------------------------------- contexts

		[Test]
		public void AResumedState_KeepsItsContext_UnlessHandedANewOne()
		{
			TypedState typed = State<TypedState>("typed");
			LoggingState untyped = State<LoggingState>("untyped");
			LoggingState overlay = State<LoggingState>("overlay");
			var first = new ScoreContext { Score = 1 };
			var untypedContext = new ScoreContext { Score = 7 };

			_machine.ChangeState(untyped, context: untypedContext);
			_machine.ChangeState(overlay, pauseCurrent: true);
			_machine.TryGoBack();
			Assert.AreSame(untypedContext, untyped.Context);

			_machine.ChangeState(typed, context: first);
			_machine.ChangeState(overlay, pauseCurrent: true);
			_machine.TryGoBack();
			Assert.AreSame(first, typed.Typed, "kept through the pause");

			var second = new ScoreContext { Score = 2 };
			_machine.ChangeState(overlay, pauseCurrent: true);
			_machine.ChangeState(typed, context: second);
			Assert.AreSame(typed, _machine.CurrentState);
			Assert.AreEqual(2, typed.Resumes, "resumed, not entered again");
			Assert.AreSame(second, typed.Typed, "a resume handed a context takes it");
		}

		[Test]
		public void AStateEnteredWithoutAContext_HasAnEmptyOne_OrANewOneOfItsType()
		{
			LoggingState untyped = State<LoggingState>("untyped");
			TypedState typed = State<TypedState>("typed");

			_machine.ChangeState(untyped);
			Assert.IsNotNull(untyped.Context);
			Assert.AreEqual(typeof(TransitionContext), untyped.Context.GetType());

			_machine.ChangeState(typed);
			Assert.IsNotNull(typed.Typed);

			ScoreContext earlier = typed.Typed;
			using (var log = new LogRecorder())
			{
				_machine.ChangeState(typed, context: new TransitionContext());
				Assert.AreEqual(0, log.Count(LogType.Warning, "."), "a plain context carries nothing, so it counts as none");
			}

			Assert.IsNotNull(typed.Typed);
			Assert.AreNotSame(earlier, typed.Typed, "each entry gets its own");
		}

		[Test]
		public void ATypedState_RefusesAContextOfAnotherType_WithAWarning()
		{
			TypedState typed = State<TypedState>("typed");

			using (ExpectedLog.Warning("'typed' takes a ScoreContext, not a OtherContext"))
			{
				_machine.ChangeState(typed, context: new OtherContext());
			}

			Assert.IsNotNull(typed.Typed);
			Assert.AreEqual(0, typed.Typed.Score);
		}

		// ---------------------------------------------------------------- failures

		[Test]
		public void AnExceptionFromACallback_ReachesTheCaller_DropsWhatWasQueued_AndTheMachineCarriesOn()
		{
			LoggingState a = State<LoggingState>("a");
			LoggingState b = State<LoggingState>("b");
			LoggingState c = State<LoggingState>("c");
			a.Entered = () =>
			{
				_machine.ChangeState(b);
				throw new InvalidOperationException("a broke");
			};

			var thrown = Assert.Throws<InvalidOperationException>(() => _machine.ChangeState(a));

			Assert.AreEqual("a broke", thrown.Message);
			Assert.AreSame(a, _machine.CurrentState);
			CollectionAssert.AreEqual(new[] { "enter a" }, _log, "not reported, and b never entered");

			_machine.ChangeState(c);

			Assert.AreSame(c, _machine.CurrentState);
			CollectionAssert.DoesNotContain(_log, "enter b");
		}

		[Test]
		public void StatesThatKeepChangingState_AreStoppedAtTheLimit()
		{
			LoggingState a = State<LoggingState>("a");
			LoggingState b = State<LoggingState>("b");
			LoggingState c = State<LoggingState>("c");
			a.Entered = () => _machine.ChangeState(b);
			b.Entered = () => _machine.ChangeState(a);

			var thrown = Assert.Throws<InvalidOperationException>(() => _machine.ChangeState(a));

			StringAssert.Contains($"{AppStateMachine.MaxTransitionsPerRun} transitions in one run", thrown.Message);
			Assert.AreEqual(AppStateMachine.MaxTransitionsPerRun, _log.FindAll(entry => entry.StartsWith("reported", StringComparison.Ordinal)).Count);

			a.Entered = null;
			b.Entered = null;
			_machine.ChangeState(c);
			Assert.AreSame(c, _machine.CurrentState, "the machine takes new changes");
		}

		// ---------------------------------------------------------------- play mode

		[Test]
		public void EnteringPlayMode_ClearsWhatAStateKeptFromTheMachine()
		{
			LoggingState a = State<LoggingState>("a");
			_machine.ChangeState(a, context: new ScoreContext());

			typeof(AppState).GetMethod("ResetOnEnterPlayMode", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);

			Assert.IsNull(a.Machine);
			Assert.IsNull(a.Context);
			Assert.IsNull(a.InjectedContainer);
		}

		// ---------------------------------------------------------------- helpers

		private T State<T>(string name) where T : AppState, ILoggingState
		{
			var state = ScriptableObject.CreateInstance<T>();
			state.name = name;
			state.Log = _log;
			_objects.Add(state);
			return state;
		}

		private static string Name(AppState state) => state == null ? "none" : state.name;

		private interface ILoggingState
		{
			List<string> Log { set; }
		}

		private sealed class ScoreContext : TransitionContext
		{
			public int Score;
		}

		private sealed class OtherContext : TransitionContext { }

		private sealed class LoggingState : AppState, ILoggingState
		{
			public List<string> Log { private get; set; }

			/// <summary>Runs inside OnEnter, after it is logged.</summary>
			public Action Entered;

			public TransitionContext Context           => _context;
			public IAppStateMachine  Machine           => AppStateMachine;
			public Container         InjectedContainer => _container;

			public override void OnEnter()
			{
				Log.Add("enter " + name);
				Entered?.Invoke();
				Log.Add("entered " + name);
			}

			public override void OnExit()   => Log.Add("exit " + name);
			public override void OnPause()  => Log.Add("pause " + name);
			public override void OnResume() => Log.Add("resume " + name);
		}

		private sealed class TypedState : AppState<ScoreContext>, ILoggingState
		{
			public List<string> Log { private get; set; }

			public int Resumes;

			public ScoreContext Typed => _context;

			public override void OnEnter()  => Log.Add("enter " + name);
			public override void OnExit()   => Log.Add("exit " + name);
			public override void OnPause()  => Log.Add("pause " + name);

			public override void OnResume()
			{
				Resumes++;
				Log.Add("resume " + name);
			}
		}
	}
}
