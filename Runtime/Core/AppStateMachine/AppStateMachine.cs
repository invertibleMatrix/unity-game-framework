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

#if UNITY_EDITOR
			if (Input.GetKeyDown(KeyCode.B))
			{
				TryGoBack();
			}
#endif
		}

		public void ChangeState(AppState appState, bool pauseCurrent = false, TransitionContext context = null)
		{
			if (appState == null)
			{
				Debug.LogError("AppStateMachine: appState is null");
				return;
			}

			_previousState = _currentState;

			if (pauseCurrent)
			{
				_previousState.OnPause();
				_pausedStates.Add(_previousState);
			}
			else
			{
				_previousState?.OnExit();
			}

			_currentState = appState;
			_currentState.Inject(_container);
			_currentState._appStateMachine = this;

			context ??= new TransitionContext();

			if (_pausedStates.Contains(_currentState))
			{
				_currentState.SetContext(context);
				_currentState.OnResume();
				_pausedStates.Remove(_currentState);
			}
			else
			{
				_currentState.SetContext(context);
				_currentState.OnEnter();
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
