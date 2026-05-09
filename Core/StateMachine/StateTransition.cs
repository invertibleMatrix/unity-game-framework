using System.Collections;
using UnityEngine;

namespace AK.StateMachine
{
    public class StateTransition<TMediator>
    {
        internal TMediator _mediator;

        protected TMediator Mediator => _mediator;

        public StateTransition()
        {
            
        }

        public StateTransition(TMediator mediator)
        {
            _mediator = mediator;
        }
        
        public virtual IEnumerator Execute()
        {
            yield return null;
        }
    }
}