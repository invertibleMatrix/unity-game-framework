using AK.StateMachines;

namespace AK.Core
{
    /// <summary>
    /// A <see cref="GameEntity"/> run by a <see cref="StateMachine{TMediator, TBaseState}"/>: the
    /// machine starts in a new <typeparamref name="TStateBase"/> in <c>Awake</c>, ticks in
    /// <c>Update</c>, and is disposed in <c>OnDestroy</c>, exiting and disposing its state.
    /// Subclasses that override these call the base.
    /// </summary>
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

        protected virtual void OnDestroy()
        {
            _stateMachine?.Dispose();
        }
    }
}
