using System.Collections.Generic;
using Reflex.Attributes;
using Reflex.Core;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using AK.Core.Extensions;

namespace AK.Core
{
	public sealed class AppStateMachine : MonoBehaviour, IAppStateMachine
	{
		[ShowInInspector] private AppState _currentState;

		[SerializeField] private AppState _bootState;

		[Inject] private readonly Container _container;

		private readonly List<AppState> _pausedStates = new();

		private void Awake()
		{
			Debug.Log("Application Entry!");
			if (_bootState == null)
			{
				Debug.LogError("No Boot State Provided, Halting!");
				return;
			}

			_currentState = _bootState;
			Debug.Log($"App: {_currentState.GetType()} OnEnter State");
		}

		//Booting In Start so that all the objects in the scene have been loaded
		private void Start()
		{
			Boot();
		}

		private void Boot()
		{
			_bootState.Inject(_container);
			_bootState._appStateMachine = this;
			_bootState.SetContext(null);
			_bootState.OnEnter();
		}

		private void Update()
		{
			if (_currentState)
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

		[Button]
		public void Restart()
		{
			_currentState.OnExit();
			SceneManager.LoadScene(0);
		}

		public void ChangeState(AppState appState, bool pauseCurrent = false, TransitionContext context = null)
		{
			if (appState == null)
			{
				Debug.LogError("App: appState is null");
				return;
			}
			
			if (_currentState != null)
			{
				Debug.Log($"{_currentState.GetType()} OnExit State");
				if (pauseCurrent)
				{
					_currentState.OnPause();
					_pausedStates.Add(_currentState);
				}
				else
				{
					_currentState.OnExit();
				}
			}

			var previousState = _currentState;
			_currentState = appState;
			_currentState.Inject(_container);
			Debug.Log($"App: {_currentState.GetType()} OnEnter State");
			_currentState._appStateMachine = this;

			if (_pausedStates.Contains(_currentState))
			{
				_currentState.OnResume();
				_pausedStates.Remove(_currentState);
			}
			else
			{
				context ??= new TransitionContext();
				context.PreviousState = previousState;
				_currentState.SetContext(context);
				_currentState.OnEnter();
			}
		}

		[Button]
		public void TryGoBack()
		{
			if (_pausedStates.Count > 0)
			{
				ChangeState(_pausedStates[^1]);
			}
		}
	}
}