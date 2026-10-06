using AK.StateMachines;

namespace AK.Core
{
    /// <summary>
    /// A plain object run by a state machine, created and ticked by the
    /// <see cref="StateObjectFactory"/>.
    /// </summary>
    public abstract class StateObject
    {
        internal abstract void InitInternal();
        internal abstract void OnUpdate();
        internal abstract void DestroyInternal();

        /// <summary>Called once the object is created and its state machine is in its first state.</summary>
        protected abstract void OnCreate();

        /// <summary>Called when the factory destroys the object, after its state machine is disposed.</summary>
        protected virtual void OnDestroy() { }
    }

    public abstract class StateObject<TStateObject, TStateBase> : StateObject
        where TStateBase : BaseState<TStateObject>, new()
        where TStateObject : class
    {
        protected StateMachine<TStateObject, TStateBase> _stateMachine;

        internal override void InitInternal()
        {
            _stateMachine = new StateMachine<TStateObject, TStateBase>(this as TStateObject);
            OnCreate();
        }

        internal override void OnUpdate()
        {
            _stateMachine.Tick();
        }

        internal override void DestroyInternal()
        {
            try
            {
                _stateMachine?.Dispose();
            }
            finally
            {
                OnDestroy();
            }
        }
    }
}
