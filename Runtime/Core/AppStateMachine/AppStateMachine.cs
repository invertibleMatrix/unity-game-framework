using System;
using System.Collections.Generic;
using Reflex.Attributes;
using Reflex.Core;
using UnityEngine;
using AK.Core.Extensions;

namespace AK.Core
{
	public sealed class AppStateMachine : MonoBehaviour, IAppStateMachine
	{
		[SerializeField] private AppState _bootState;

		[Inject] private readonly Container _container;

		private AppState _currentState;
		private AppState _previousState;
		private readonly List<AppState> _pausedStates = new();

		public event Action<AppState> OnStateChange;
		
		public AppState CurrentState => _currentState;
		public AppState PreviousState => _previousState;

		private void Awake()
		{
			if (_bootState == null)
			{
				Debug.LogError("No Boot State Provided, Halting!");
				enabled = false;
				return;
			}
		}

		// Booting in Start so that all objects in the scene have been loaded
		private void Start()
		{
			Boot();
		}

		private void Boot()
		{
			_currentState = _bootState;
			_bootState.Inject(_container);
			_bootState._appStateMachine = this;
			_bootState.SetContext(new TransitionContext());
			_bootState.OnEnter();
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

			_previousState = _currentState;

			// NOTE: self-transitions are INTENTIONALLY allowed - OnExit -> OnEnter on the same
			// state is the standard way to restart it (e.g. GameState re-enter on level restart).

			if (pauseCurrent && _previousState != null)
			{
				_previousState.OnPause();

				// Guard against pause-stacking the same state twice: a duplicate entry would make
				// TryGoBack "resume" a state that is already current.
				if (!_pausedStates.Contains(_previousState))
				{
					_pausedStates.Add(_previousState);
				}
				else
				{
					Debug.LogWarning($"AppStateMachine: state '{_previousState.name}' is already paused - not stacking a duplicate.");
				}
			}
			else
			{
				// Note: may be legitimately re-entered from the pause stack below; OnExit there
				// is skipped by design (resume semantics).
				_previousState?.OnExit();
			}

			_currentState = appState;
			_currentState.Inject(_container);
			_currentState._appStateMachine = this;

			context ??= new TransitionContext();

			if (_pausedStates.Contains(_currentState))
			{
				_currentState.SetContext(context);
				_pausedStates.Remove(_currentState);
				_currentState.OnResume();
			}
			else
			{
				_currentState.SetContext(context);
				_currentState.OnEnter();
				OnStateChange?.Invoke(_currentState);
			}
		}

		public void TryGoBack()
		{
			if (_pausedStates.Count > 0)
			{
				ChangeState(_pausedStates[^1]);
			}
		}
	}
}
