using Reflex.Attributes;
using Reflex.Core;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// A top-level state of the game, run by the <see cref="AppStateMachine"/>: entered and
	/// exited, or paused under another state and resumed.
	///
	/// <para>A state is an asset, so the fields a subclass adds live as long as the asset stays
	/// loaded: across a whole run of the game, and from one Play session to the next in the editor
	/// when domain reload is off. Reset them in <see cref="OnEnter"/>. The machine's own references
	/// (the machine, the context and the container) are cleared when Play mode starts.</para>
	/// </summary>
	public abstract class AppState : ScriptableObject
	{
		internal IAppStateMachine _appStateMachine;

		/// <summary>
		/// The context the state was entered with, kept through pauses unless a resume hands it a
		/// new one. A state entered without one has an empty context.
		/// </summary>
		protected TransitionContext _context;

		[Inject] protected Container _container;

		protected IAppStateMachine AppStateMachine => _appStateMachine;

		public virtual void OnEnter() { }

		public virtual void OnExit() { }

		public virtual void OnPause() { }

		public virtual void OnResume() { }

		public virtual void Tick() { }

		/// <summary>Takes the context of an entry, or of a resume that hands one. Null is none.</summary>
		internal virtual void SetContext(TransitionContext context)
		{
			_context = context ?? TransitionContext.Empty;
		}

		internal void ResetRuntimeState()
		{
			_appStateMachine = null;
			_context = null;
			_container = null;
		}

#if UNITY_EDITOR
		// With domain reload off, a state asset keeps the last session's machine, context and
		// container until it is entered again.
		[UnityEditor.InitializeOnEnterPlayMode]
		private static void ResetOnEnterPlayMode()
		{
			foreach (AppState state in Resources.FindObjectsOfTypeAll<AppState>())
			{
				state.ResetRuntimeState();
			}
		}
#endif
	}

	/// <summary>A state that takes a context of its own type: <c>_context</c> is a <typeparamref name="TTransitionContext"/>.</summary>
	public abstract class AppState<TTransitionContext> : AppState where TTransitionContext : TransitionContext, new()
	{
		protected new TTransitionContext _context => (TTransitionContext)base._context;

		/// <summary>
		/// A context of another type is refused with a warning. Like none, and like a plain
		/// <see cref="TransitionContext"/>, which carries nothing, it leaves the state a new
		/// <typeparamref name="TTransitionContext"/>.
		/// </summary>
		internal override void SetContext(TransitionContext context)
		{
			if (context is TTransitionContext typedContext)
			{
				base._context = typedContext;
				return;
			}

			if (context != null && context.GetType() != typeof(TransitionContext))
			{
				Debug.LogWarning($"AppState '{name}' takes a {typeof(TTransitionContext).Name}, not a {context.GetType().Name}. It gets a new {typeof(TTransitionContext).Name}.", this);
			}

			base._context = new TTransitionContext();
		}
	}
}
