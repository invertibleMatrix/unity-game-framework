using System;
using System.Collections.Generic;
using Reflex.Attributes;
using Reflex.Core;
using UnityEngine;
using AK.Core.Extensions;

namespace AK.Core
{
	public readonly struct StateTransitionInfo
	{
		public readonly AppState From;
		public readonly AppState To;
		public readonly bool     PreviousPaused;
		public readonly bool     Resumed;

		public StateTransitionInfo(AppState from, AppState to, bool previousPaused, bool resumed)
		{
			From = from;
			To = to;
			PreviousPaused = previousPaused;
			Resumed = resumed;
		}
	}

	/// <summary>
	/// Runs the game's top-level states. One state is current; states paused under it wait on a
	/// stack until <see cref="TryGoBack"/> resumes them.
	///
	/// <para><b>One transition at a time.</b> A transition exits or pauses the current state,
	/// enters or resumes the next, then reports itself: <see cref="OnStateChange"/> after an
	/// entry, and <see cref="OnTransition"/> after every transition. A change requested while a
	/// transition runs, from a state's callback or from a listener, waits until that transition
	/// has been reported, and requests run in the order made. So each transition is reported
	/// once, and a state never exits before its own OnEnter has returned.</para>
	///
	/// <para><b>Failures.</b> An exception from a state's callback ends its transition there and
	/// reaches the caller of the change that started the run. Changes queued behind it are
	/// dropped, and the machine takes new ones.</para>
	///
	/// <para>Main thread only.</para>
	/// </summary>
	public sealed class AppStateMachine : MonoBehaviour, IAppStateMachine
	{
		/// <summary>
		/// Transitions one request may lead to, those its states request included. More means
		/// states keep changing state from their callbacks without end.
		/// </summary>
		internal const int MaxTransitionsPerRun = 64;

		[SerializeField] private AppState _bootState;

		[Inject] private readonly Container _container;

		private AppState _currentState;
		private AppState _previousState;
		private readonly List<AppState> _pausedStates = new();

		private readonly Queue<Request> _requests = new();
		private bool _running;

		/// <summary>Fires after a state is entered, the boot state included. A resumed state doesn't fire it.</summary>
		public event Action<AppState> OnStateChange;

		/// <summary>Fires after every transition, the boot state's entry and resumes included.</summary>
		public event Action<StateTransitionInfo> OnTransition;

		public AppState CurrentState => _currentState;
		public AppState PreviousState => _previousState;
		public IReadOnlyList<AppState> PausedStates => _pausedStates;

		private void Awake()
		{
			if (_bootState == null)
			{
				Debug.LogError("No Boot State Provided, Halting!");
				enabled = false;
			}
		}

		// Boots in Start, once every object in the scene has loaded. The boot state is entered
		// like any other: OnEnter, then OnStateChange and OnTransition.
		private void Start()
		{
			ChangeState(_bootState);
		}

		private void Update()
		{
			if (_currentState != null)
			{
				_currentState.Tick();
			}
		}

		public void ChangeState(AppState appState, bool pauseCurrent = false, TransitionContext context = null)
		{
			if (appState == null)
			{
				Debug.LogError("AppStateMachine: appState is null");
				return;
			}

			Run(new Request(appState, pauseCurrent, context, goBack: false));
		}

		public void TryGoBack()
		{
			// Requested during a transition, the go-back takes the top of the stack as it stands
			// when its turn comes.
			if (!_running && _pausedStates.Count == 0) return;

			Run(new Request(null, pauseCurrent: false, context: null, goBack: true));
		}

		private void Run(in Request request)
		{
			_requests.Enqueue(request);
			if (_running) return;

			_running = true;
			try
			{
				for (int transitions = 0; _requests.Count > 0; transitions++)
				{
					if (transitions == MaxTransitionsPerRun)
					{
						throw new InvalidOperationException(
							$"AppStateMachine: {MaxTransitionsPerRun} transitions in one run. States keep changing state from their callbacks.");
					}

					Transition(_requests.Dequeue());
				}
			}
			finally
			{
				// Empty unless a callback threw: what it left queued is dropped.
				_requests.Clear();
				_running = false;
			}
		}

		private void Transition(in Request request)
		{
			AppState next = request.GoBack ? TopOfPauseStack() : request.State;
			if (next == null) return;

			AppState from = _currentState;
			bool paused = request.PauseCurrent && from != null;

			_previousState = from;

			if (paused)
			{
				from.OnPause();

				// Guard against pause-stacking the same state twice: a duplicate entry would make
				// TryGoBack "resume" a state that is already current.
				if (!_pausedStates.Contains(from))
				{
					_pausedStates.Add(from);
				}
				else
				{
					Debug.LogWarning($"AppStateMachine: state '{from.name}' is already paused - not stacking a duplicate.");
				}
			}
			else if (from != null)
			{
				// Self-transitions are intended: OnExit then OnEnter restarts a state.
				from.OnExit();
			}

			_currentState = next;
			next.Inject(_container);
			next._appStateMachine = this;

			bool resumed = _pausedStates.Remove(next);
			if (resumed)
			{
				// A resumed state keeps the context it was entered with, unless it is handed a new one.
				if (request.Context != null)
				{
					next.SetContext(request.Context);
				}

				next.OnResume();
			}
			else
			{
				next.SetContext(request.Context);
				next.OnEnter();
				OnStateChange?.Invoke(next);
			}

			OnTransition?.Invoke(new StateTransitionInfo(from, next, paused, resumed));
		}

		private AppState TopOfPauseStack() => _pausedStates.Count > 0 ? _pausedStates[^1] : null;

		private readonly struct Request
		{
			public readonly AppState          State;
			public readonly bool              PauseCurrent;
			public readonly TransitionContext Context;

			/// <summary>Resume the top of the pause stack, decided when the request runs.</summary>
			public readonly bool GoBack;

			public Request(AppState state, bool pauseCurrent, TransitionContext context, bool goBack)
			{
				State        = state;
				PauseCurrent = pauseCurrent;
				Context      = context;
				GoBack       = goBack;
			}
		}
	}
}
