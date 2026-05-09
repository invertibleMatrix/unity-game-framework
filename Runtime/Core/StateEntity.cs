using System;
using AK.StateMachine;
using UnityEngine;

namespace AK.Core
{
    public abstract class StateEntity<TStateEntity, TStateBase> : GameEntity
        where TStateEntity : GameEntity
        where TStateBase : BaseState<TStateEntity>, new()
    {
        protected StateMachine<TStateEntity, TStateBase> _stateMachine;

        protected virtual void Awake()
        {
            _stateMachine = new StateMachine<TStateEntity, TStateBase>(this as TStateEntity);
        }

        protected virtual void Update()
        {
            if (_stateMachine != null)
            {
                _stateMachine.Tick();
            }
        }

        protected virtual void OnDestroy() { }
    }
}