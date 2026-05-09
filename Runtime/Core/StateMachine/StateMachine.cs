using System.Collections;
using UnityEngine;

namespace AK.StateMachine
{
    public sealed class StateMachine<TMediator, TBaseState> where TBaseState : BaseState<TMediator>, new()
    {
        private TMediator  _mediator;
        private TBaseState _mainState;
        private TBaseState _currentState;

        private bool _isTransitioning;

        // NOTE: Don't call this one inside this class
        public TBaseState ActiveState
        {
            get
            {
                // If state hasn't Booted yet then assign it to the main state instance to avoid null ref
                if (_currentState == null)
                {
                    _currentState = _mainState;
                }

                return _currentState;
            }
        }

        public StateMachine(TMediator mediator)
        {
            _mediator = mediator;
            _mainState = new TBaseState
            {
                _mediator = _mediator
            };
        }

        public void Tick()
        {
            if (_currentState != null && !_isTransitioning)
            {
                _currentState.Tick();
            }
        }

        public void ChangeState(TBaseState newState, bool transition = false)
        {
            if (newState == null)
            {
                Debug.LogError("newState is null");
                return;
            }

            // if (_currentState != null && _currentState == newState)
            // {
            //     Debug.Log($"{_currentState.GetType()} is same, Needs attention");
            // }

            if (_currentState != null)
            {
                _currentState.OnExit();
            }

            _currentState = newState;
            _currentState._mediator = _mediator;
            _currentState.OnEnter(transition);
        }

        public IEnumerator Transition(TBaseState newState, StateTransition<TMediator> transitionPreEnter = null,
                                      StateTransition<TMediator> transitionPreExit = null)
        {
            if (newState == null)
            {
                Debug.LogError("newState is null");
                yield break;
            }

            if (_currentState != null && _currentState == newState)
            {
                Debug.Log($"{_currentState.GetType()} is same, skipping!");
                yield break;
            }

            if (transitionPreEnter == null && transitionPreExit == null)
            {
                yield break;
            }

            _isTransitioning = true;

            if (transitionPreExit != null)
            {
                transitionPreExit._mediator = _mediator;
                yield return transitionPreExit.Execute();
            }

            if (_currentState != null && _currentState != newState)
            {
                Debug.Log($"{_currentState.GetType()} OnExit Via Transition");
                _currentState.OnExit();
            }

            if (transitionPreEnter != null)
            {
                transitionPreEnter._mediator = _mediator;
                yield return transitionPreEnter.Execute();
            }

            if (_currentState != null && _currentState != newState)
            {
                _currentState = newState;
                Debug.Log($"{_currentState.GetType()} OnEnter Via Transition");
                _currentState.OnEnter(false);
            }
            else
            {
                Debug.LogError("Alert! This should have never happened!");
            }

            _isTransitioning = false;
        }

        public StateTransition<TMediator> SkipOneFrame()
        {
            return new StateTransition<TMediator>();
        }

        public void Dispose()
        {
            if (_currentState != null)
            {
                _currentState.OnExit();
            }
        }
    }
}