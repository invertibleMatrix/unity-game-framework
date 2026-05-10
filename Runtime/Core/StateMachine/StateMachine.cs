using System;

namespace AK.StateMachine
{
    public sealed class StateMachine<TMediator, TBaseState> where TBaseState : BaseState<TMediator>, new()
    {
        private readonly TMediator _mediator;
        private readonly TBaseState _initialState;

        private TBaseState _currentState;
        private bool _disposed;

        public TBaseState CurrentState => _currentState;

        public StateMachine(TMediator mediator)
        {
            _mediator = mediator;
            _initialState = new TBaseState { _mediator = _mediator };
            _currentState = _initialState;
            _currentState.OnEnter();
        }

        public void Tick()
        {
            if (_currentState != null && !_disposed)
            {
                _currentState.Tick();
            }
        }

        public void ChangeState(TBaseState newState)
        {
            if (_disposed) return;

            if (newState == null)
                throw new ArgumentNullException(nameof(newState));

            _currentState?.OnExit();

            _currentState = newState;
            _currentState._mediator = _mediator;
            _currentState.OnEnter();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _currentState?.OnExit();
            _currentState?.Dispose();
            _currentState = null;
        }
    }
}
