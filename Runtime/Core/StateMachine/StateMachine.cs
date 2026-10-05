using System;
using System.Collections.Generic;

namespace AK.StateMachines
{
    /// <summary>
    /// <para>A state machine for an entity: one state at a time, ticked by the entity. It starts in
    /// a new <typeparamref name="TBaseState"/>.</para>
    ///
    /// <para><b>Ownership.</b> The machine owns the states handed to it: it exits and disposes each
    /// one it leaves, and the current one when it is disposed. A state handed in that is never
    /// entered is disposed too. Hand it a new state each time; changing to the current state exits
    /// and enters it again, without disposing it.</para>
    ///
    /// <para><b>One change at a time.</b> A change requested while one runs, from a state's
    /// <c>OnExit</c> or <c>OnEnter</c>, runs once it has finished, in the order requested. A run is
    /// capped at <see cref="MaxChangesPerRun"/> changes, so states that keep changing state throw
    /// instead of hanging. If a state throws, the exception reaches the caller, and the changes
    /// still waiting are dropped.</para>
    ///
    /// <para><b>Disposal.</b> <see cref="Dispose"/> exits and disposes the current state; called
    /// during a change, it does so once the state callback that called it returns. Afterwards a
    /// change disposes the state handed in, and a tick does nothing.</para>
    /// </summary>
    public sealed class StateMachine<TMediator, TBaseState> where TBaseState : BaseState<TMediator>, new()
    {
        internal const int MaxChangesPerRun = 64;

        private readonly TMediator _mediator;

        // States handed in and not entered yet, oldest first. A state stays here until it is
        // entered, so a change that throws leaves it here to be disposed.
        private readonly Queue<TBaseState> _pending = new();

        private TBaseState _currentState;
        private bool       _changing;
        private bool       _disposed;

        public TBaseState CurrentState => _currentState;

        public StateMachine(TMediator mediator)
        {
            _mediator = mediator;
            ChangeState(new TBaseState());
        }

        public void Tick()
        {
            if (_currentState != null && !_disposed)
            {
                _currentState.Tick();
            }
        }

        /// <exception cref="ArgumentNullException"><paramref name="newState"/> is null.</exception>
        /// <exception cref="InvalidOperationException">States changed state <see cref="MaxChangesPerRun"/> times in one run.</exception>
        public void ChangeState(TBaseState newState)
        {
            if (newState == null)
                throw new ArgumentNullException(nameof(newState));

            if (_disposed)
            {
                if (!ReferenceEquals(newState, _currentState)) newState.Dispose();
                return;
            }

            _pending.Enqueue(newState);
            if (_changing) return;

            _changing = true;
            try
            {
                for (int changes = 0; _pending.Count > 0 && !_disposed; changes++)
                {
                    if (changes == MaxChangesPerRun)
                    {
                        throw new InvalidOperationException(
                            $"StateMachine: {MaxChangesPerRun} state changes in one run. States keep changing state from OnEnter or OnExit.");
                    }

                    EnterNext();
                }
            }
            finally
            {
                _changing = false;

                if (_disposed) Close();
                else DisposePending();
            }
        }

        /// <summary>Exits and disposes the current state, and any state waiting to be entered. Idempotent.</summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            // During a change, the change closes the machine once the callback returns.
            if (!_changing) Close();
        }

        private void EnterNext()
        {
            TBaseState next = _pending.Peek();

            TBaseState previous = _currentState;
            if (previous != null)
            {
                previous.OnExit();
                _currentState = null;
                if (!ReferenceEquals(previous, next)) previous.Dispose();

                // Disposed from OnExit: the next state is never entered.
                if (_disposed) return;
            }

            _pending.Dequeue();
            _currentState = next;
            next._mediator = _mediator;
            next.OnEnter();
        }

        private void Close()
        {
            TBaseState last = _currentState;
            _currentState = null;

            try
            {
                last?.OnExit();
            }
            finally
            {
                last?.Dispose();
                DisposePending();
            }
        }

        private void DisposePending()
        {
            while (_pending.Count > 0)
            {
                TBaseState dropped = _pending.Dequeue();
                if (!ReferenceEquals(dropped, _currentState)) dropped.Dispose();
            }
        }
    }
}
