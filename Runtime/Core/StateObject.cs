using System;
using System.Collections;
using AK.StateMachine;

namespace AK.Core
{
    public abstract class StateObject
    {
        internal abstract void InitInternal();
        protected abstract void OnCreate();
        internal abstract void OnUpdate();
        internal abstract void OnDestroy();
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
    }
}