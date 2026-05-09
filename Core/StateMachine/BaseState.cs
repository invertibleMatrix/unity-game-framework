using Cysharp.Threading.Tasks;

namespace AK.StateMachine
{
    public abstract class BaseState<TMediator>
    {
        internal TMediator _mediator;

        protected TMediator Mediator => _mediator;
        
        public virtual void OnEnter(bool isTransition) { }

        public virtual void Tick() { }

        public virtual void OnExit() { }

        public virtual void Dispose() { }

        public virtual async UniTask OnEnterAsync() { }
        public virtual async UniTask TickAsync() { }
        public virtual async UniTask OnExitAsync() { }
        public virtual async UniTask DisposeAsync() { }
    }
}