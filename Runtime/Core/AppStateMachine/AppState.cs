using Reflex.Attributes;
using Reflex.Core;
using UnityEngine;

namespace AK.Core
{
	public abstract class AppState : ScriptableObject
	{
		internal IAppStateMachine _appStateMachine;

		protected TransitionContext _context;

		[Inject] protected Container _container;

		protected IAppStateMachine AppStateMachine => _appStateMachine;

		public virtual void OnEnter() { }

		public virtual void OnExit() { }

		public virtual void OnPause() { }

		public virtual void OnResume() { }

		public virtual void Tick() { }

		public virtual void SetContext(TransitionContext context)
		{
			_context = context;
		}
	}

	public abstract class AppState<TTransitionContext> : AppState where TTransitionContext : TransitionContext, new()
	{
		protected new TTransitionContext _context => (TTransitionContext)base._context;

		public override void SetContext(TransitionContext context)
		{
			if (context is TTransitionContext typedContext)
			{
				base._context = typedContext;
			}
			else
			{
				base._context = new TTransitionContext();
			}
		}
	}
}
