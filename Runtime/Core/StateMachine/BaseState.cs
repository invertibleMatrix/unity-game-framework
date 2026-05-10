namespace AK.StateMachine
{
    public abstract class BaseState<TMediator>
    {
        internal TMediator _mediator;

        protected TMediator Mediator => _mediator;
        
        public virtual void OnEnter() { }

        public virtual void Tick() { }

        public virtual void OnExit() { }

        public virtual void Dispose() { }
    }
}