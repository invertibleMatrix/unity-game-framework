using AK.Systems.Animations;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Serializable entry for static fragments that are pre-placed in the hierarchy.
	/// </summary>
	[Serializable]
	public struct StaticViewEntry
	{
		[SerializeField] public UIView View;
		[SerializeField] public bool   ShowOnStart;
		[SerializeField, Tooltip("Seconds to wait after the parent shows before this fragment starts its entrance. Use to stagger sibling animations. If the fragment closes before the delay elapses, the start is cancelled.")]
		public float ShowOnStartDelay;
		[SerializeField] public bool   SetActive;
	}

	/// <summary>
	/// Unified base class for all UI elements — replaces both UIScreen and UIFragment from V1.
	///
	/// Any UIView can optionally have a <see cref="UIViewChannel"/> component attached.
	/// With UIChannel: the view gets its own Canvas, manages sorting, and acts as a "screen" 
	/// that can host fragments.
	/// Without UIChannel: the view is a "fragment" that spawns inside a parent view's container.
	///
	/// Optional companions, added as sibling components: <see cref="ViewBackgroundOverlay"/>
	/// dims everything behind the view while it is shown; <see cref="ViewHighlight"/> raises it
	/// above everything on demand.
	///
	/// The system drives a view exclusively through <see cref="IViewLifecycle"/>, implemented
	/// explicitly so that surface never shows up on a subclass. A view reaches the system only
	/// through <see cref="IViewHost"/>. <see cref="State"/> tracks where the view is in its
	/// lifecycle; the show and close hooks fire only on its transitions.
	/// </summary>
	[RequireComponent(typeof(CanvasGroup))]
	public abstract class UIView : MonoBehaviour, IViewLifecycle
	{
		// --- Serialized Configuration ---

		[SerializeField, Tooltip("Unique ID for this view variant. Keep empty for default.")]
		private string _viewId = "";

		[SerializeField]
		private ViewStackBehaviour _stackBehaviour = ViewStackBehaviour.DoNothing;

		[SerializeField, Tooltip("Animation strategy applied directly on this view. Migrated from _animationConfig via editor script.")]
		private AnimationStrategy _animationStrategy;

		[SerializeField, Tooltip("Per-instance component animation strategy. Takes precedence over the SO strategy when both are assigned. Use when the animation needs scene references.")]
		private AnimationStrategyComponent _animationComponent;

		[SerializeField, Tooltip("If true, this view's hide animation runs in parallel with the next view's show animation. Migrated from _animationConfig.")]
		private bool _playInParallelWithPrevious;

		[SerializeField, Tooltip("The child containing all visual elements to animate. Falls back to this RectTransform.")]
		private RectTransform _animatableContent;

		[SerializeField, Tooltip("Transform where dynamically spawned fragments are placed. Falls back to this transform.")]
		private Transform _fragmentContainer;

		[Header("Dynamic Instances")]
		[SerializeField, Tooltip("Return to pool on close instead of destroying. Ignored for static views.")]
		private bool _returnToPoolOnClose;

		[SerializeField, Tooltip("Allow multiple instances active simultaneously. Ignored for static views.")]
		private bool _allowMultipleInstances;

		[SerializeField, Tooltip("How children close relative to this view on a graceful close. Each level consults its own policy for its own children. ParentFirst = this view animates out first, children settle after (current default).")]
		private ChildCloseOrder _childCloseOrder = ChildCloseOrder.ParentFirst;

		[SerializeField, Tooltip("If checked, the view will animate to its current anchored position instead of zero.")]
		private bool _lockEntryPosition;

		[Header("Static Fragments")]
		[SerializeField, Tooltip("Fragment views pre-placed in this view's hierarchy.")]
		private List<StaticViewEntry> _staticViews = new();

		[SerializeField, Tooltip("When this view is listed as a static fragment, treat it as a clone source: it never shows itself — ShowFragment spawns a live copy per call. Keep its GameObject inactive and ShowOnStart off.")]
		private bool _isTemplate;

		// --- Runtime State ---

		private readonly ViewAnimator _animator = new();

		private IViewHost               _host;
		private UIView                  _parentView;
		private ViewBackgroundOverlay   _overlay;
		private ViewStackBehaviour?     _stackBehaviourOverride;
		private CancellationTokenSource _delayedStartCts;
		private Vector2                 _entryPosition = Vector2.zero;
		private int                     _run;

		// --- Public Properties ---

		public string                         ViewId                     => _viewId;
		public ViewStackBehaviour             StackBehaviour             => _stackBehaviourOverride ?? _stackBehaviour;
		public IAnimationStrategy             AnimationStrategy          => _animationComponent != null ? _animationComponent : _animationStrategy;
		public bool                           PlayInParallelWithPrevious => _playInParallelWithPrevious;
		public bool                           NoAnimation                => AnimationStrategy == null;
		public bool                           ReturnToPoolOnClose        => _returnToPoolOnClose;
		public bool                           AllowMultipleInstances     => _allowMultipleInstances;
		public ChildCloseOrder                ChildCloseOrder            => _childCloseOrder;
		public bool                           IsTemplate                 => _isTemplate;
		public ViewState                      State                      { get; private set; } = ViewState.Hidden;
		public bool                           IsVisible                  { get; private set; }
		public CanvasGroup                    CanvasGroup                { get; private set; }
		public RectTransform                  RectTransform              { get; private set; }
		public Transform                      FragmentContainer          => _fragmentContainer;
		public UIContext                      Context                    { get; protected set; }
		public UIView                         ParentView                 => _parentView;
		public IReadOnlyList<StaticViewEntry> StaticViews                => _staticViews;
		public IUISystem                      UISystem                   => _host;

		/// <summary>
		/// Returns the UIChannel component if this view has one, null otherwise.
		/// </summary>
		public UIViewChannel Channel { get; private set; }

		/// <summary>
		/// True if this view has a UIChannel component (acts like a "screen").
		/// </summary>
		public bool HasChannel => Channel != null;

		// =====================================================================
		// LIFECYCLE HOOKS — Override these in subclasses
		// =====================================================================

		/// <summary>Called to set context data before the view is shown.</summary>
		public virtual void SetContext(UIContext context) { }

		/// <summary>Called when the view is being prepared. Register event subscriptions here.</summary>
		public virtual void RegisterResources() { }

		/// <summary>Called when the view is being disposed. Unregister event subscriptions here.</summary>
		public virtual void UnRegisterResources() { }

		/// <summary>Called after instantiation, before the show animation starts (view is still invisible).</summary>
		public virtual void OnPrepareShow() { }

		/// <summary>Called before the hide animation starts (view is still visible).</summary>
		public virtual void OnPrepareHide() { }

		/// <summary>Called after the show animation completes and the view is fully visible.</summary>
		public virtual void OnShow() { }

		/// <summary>Called after the hide animation completes and the view is invisible.</summary>
		public virtual void OnHide() { }

		/// <summary>Called when a higher-priority view is pushed on top (this view is paused).</summary>
		public virtual void OnPause() { }

		/// <summary>Called when the view on top is closed and this view resumes.</summary>
		public virtual void OnResume() { }

		/// <summary>Called when a pooled view is returned to the pool. Reset your internal state here.</summary>
		public virtual void OnReset()
		{
			ResetState();
		}

		/// <summary>
		/// Override this to close dynamic fragments before pooling.
		/// </summary>
		public virtual void OnBeforePool()
		{
			// Override in subclasses to close dynamic fragments before pooling
			// Example: Close any dynamically spawned tooltips, popups, etc.
		}

		// =====================================================================
		// PUBLIC API
		// =====================================================================

		/// <summary>
		/// Shows a fragment of <typeparamref name="TFragment"/> hosted by this view. Fire-and-forget.
		/// <see cref="ShowOptions.Parent"/> is always this view.
		/// </summary>
		public TFragment ShowFragment<TFragment>(in ShowOptions options = default, Action<TFragment> onInit = null)
			where TFragment : UIView
		{
			return _host.Show(options.WithParent(this), onInit);
		}

		/// <summary>Shows a fragment hosted by this view and completes when its show animation finishes.</summary>
		public UniTask<TFragment> ShowFragmentAsync<TFragment>(in ShowOptions options = default, Action<TFragment> onInit = null,
		                                                       CancellationToken ct = default)
			where TFragment : UIView
		{
			return _host.ShowAsync(options.WithParent(this), onInit, ct);
		}

		/// <summary>
		/// Shows an already-registered fragment instance (a pre-placed static listed in this
		/// view's Static Fragments). Fire-and-forget.
		/// </summary>
		public void ShowFragment(UIView view, in ShowOptions options = default)
		{
			_host.Show(view, options);
		}

		/// <summary>Awaitable form of <see cref="ShowFragment(UIView, in ShowOptions)"/>.</summary>
		public UniTask ShowFragmentAsync(UIView view, in ShowOptions options = default, CancellationToken ct = default)
		{
			return _host.ShowAsync(view, options, ct);
		}

		/// <summary>
		/// Closes this view. Fire-and-forget — animation runs in the background.
		/// </summary>
		public void Close(Action onClosed = null)
		{
			_host?.Close(this, default, onClosed);
		}

		/// <summary>Closes this view without animation. Lifecycle hooks still run.</summary>
		public void CloseImmediate(Action onClosed = null)
		{
			_host?.Close(this, CloseOptions.Now, onClosed);
		}

		/// <summary>
		/// Closes this view and awaits until the close animation completes.
		/// </summary>
		public UniTask CloseAsync(CancellationToken ct = default)
		{
			return _host?.CloseAsync(this, default, ct) ?? UniTask.CompletedTask;
		}

		public void SetInteractable(bool value)
		{
			CanvasGroup.interactable = value;
			CanvasGroup.blocksRaycasts = value;
		}

		// =====================================================================
		// IViewLifecycle — the system's side of the view
		// =====================================================================

		void IViewLifecycle.Attach(IViewHost host, UIView parent, Container container)
		{
			GameObjectInjector.InjectRecursive(gameObject, container);

			_host = host;
			_parentView = parent;

			CanvasGroup = GetComponent<CanvasGroup>();
			RectTransform = GetComponent<RectTransform>();
			Channel = GetComponent<UIViewChannel>();
			_overlay = GetComponent<ViewBackgroundOverlay>();

			if (_animatableContent == null) _animatableContent = RectTransform;
			if (_fragmentContainer == null) _fragmentContainer = transform;

			if (_lockEntryPosition && _entryPosition == Vector2.zero)
			{
				_entryPosition = _animatableContent.anchoredPosition;
			}

			_animator.Bind(_animatableContent, CanvasGroup);
		}

		void IViewLifecycle.AttachStaticChildren(Container container)
		{
			if (_staticViews.Count == 0) return;

			AttachStaticChildrenRecursive(container, this, 0);
		}

		void IViewLifecycle.Teardown() => Teardown();

		async UniTask IViewLifecycle.ShowAsync(bool immediate, CancellationToken ct)
		{
			int run = BeginRun();
			if (State == ViewState.Hiding) FinishExit();

			gameObject.SetActive(true);
			OnPrepareShow();

			// A re-show of a view that is already shown or paused keeps its resources.
			if (State == ViewState.Hidden) RegisterResources();
			State = ViewState.Showing;

			if (immediate || NoAnimation)
			{
				CanvasGroup.alpha = 1f;
				CompleteShow();
				return;
			}

			bool finished = await _animator.PlayShowAsync(AnimationStrategy, _entryPosition, ct);
			if (run != _run) return;

			if (finished) CompleteShow();
			else AbandonEntrance();
		}

		async UniTask IViewLifecycle.HideAsync(HideMode mode, bool immediate, CancellationToken ct)
		{
			int run = BeginRun();
			bool closing = mode == HideMode.Close;

			if (State == ViewState.Hiding) FinishExit();

			if (closing)
			{
				CancelDelayedStart();

				// An entrance that never reached OnShow gets no hide hooks either.
				if (State == ViewState.Showing) AbandonEntrance();

				if (State == ViewState.Hidden)
				{
					if (CanvasGroup != null) CanvasGroup.alpha = 0f;
					SettleHidden();
					return;
				}

				OnPrepareHide();
				State = ViewState.Hiding;
			}

			if (_overlay != null) _overlay.FadeOut();

			if (immediate || NoAnimation)
			{
				CanvasGroup.alpha = 0f;
				SettleHidden();
				if (closing) FinishExit();
				return;
			}

			bool finished = await _animator.PlayHideAsync(AnimationStrategy, ct);
			if (run != _run) return;

			if (!finished) CanvasGroup.alpha = 0f;
			SettleHidden();
			if (closing) FinishExit();
		}

		async UniTask IViewLifecycle.ResumeAsync(bool immediate, CancellationToken ct)
		{
			int run = BeginRun();
			if (State == ViewState.Hiding) FinishExit();

			gameObject.SetActive(true);

			if (immediate || NoAnimation)
			{
				CanvasGroup.alpha = 1f;
				SettleVisible();
				return;
			}

			bool finished = await _animator.PlayShowAsync(AnimationStrategy, _entryPosition, ct);
			if (run != _run || !finished) return;

			SettleVisible();
		}

		void IViewLifecycle.Conceal()
		{
			CanvasGroup.alpha = 0f;
		}

		void IViewLifecycle.Pause()
		{
			SetInteractable(false);
			OnPause();
			if (State == ViewState.Shown) State = ViewState.Paused;
		}

		void IViewLifecycle.Resume()
		{
			SetInteractable(true);
			OnResume();
			if (State == ViewState.Paused) State = ViewState.Shown;
		}

		void IViewLifecycle.OverrideStackBehaviour(ViewStackBehaviour? behaviour)
		{
			_stackBehaviourOverride = behaviour;
		}

		CancellationToken IViewLifecycle.ArmDelayedStart()
		{
			CancelDelayedStart();
			_delayedStartCts = new CancellationTokenSource();
			return _delayedStartCts.Token;
		}

		// =====================================================================
		// PRIVATE — transitions
		// =====================================================================

		/// <summary>
		/// Starts a new entrance/exit run. Whatever run was in flight is cancelled and its
		/// continuation, seeing a newer run, does nothing; the new run settles the interrupted
		/// state itself (<see cref="FinishExit"/>, <see cref="AbandonEntrance"/>).
		/// </summary>
		private int BeginRun()
		{
			int run = ++_run;
			_animator.Cancel();
			return run;
		}

		private void CompleteShow()
		{
			State = ViewState.Shown;
			SettleVisible();
			OnShow();
			_host.NotifyShown(this);
			_host.ShowStaticChildren(this);
		}

		/// <summary>The entrance was cut short, so OnShow never runs: release what OnPrepareShow registered.</summary>
		private void AbandonEntrance()
		{
			if (State != ViewState.Showing) return;
			State = ViewState.Hidden;
			UnRegisterResources();
		}

		/// <summary>Completes an exit that OnPrepareHide started.</summary>
		private void FinishExit()
		{
			if (State != ViewState.Hiding) return;
			State = ViewState.Hidden;
			OnHide();
			UnRegisterResources();
		}

		private void SettleVisible()
		{
			if (_overlay != null) _overlay.FadeIn();
			IsVisible = true;
		}

		private void SettleHidden()
		{
			gameObject.SetActive(false);
			IsVisible = false;
		}

		/// <summary>
		/// Forced settle before destroy or pool. Runs whatever the current state still owes —
		/// nothing for a hidden view, the close hooks for a shown or paused one, the rest of the
		/// exit for one mid-hide, only the resource release for one mid-show. Idempotent.
		/// </summary>
		private void Teardown()
		{
			_run++;
			CancelDelayedStart();
			_animator.Cancel();
			_animator.KillTweens();

			switch (State)
			{
				case ViewState.Showing:
					AbandonEntrance();
					break;

				case ViewState.Shown:
				case ViewState.Paused:
					OnPrepareHide();
					State = ViewState.Hiding;
					IsVisible = false;
					FinishExit();
					break;

				case ViewState.Hiding:
					IsVisible = false;
					FinishExit();
					break;
			}
		}

		/// <summary>
		/// Resets runtime state for reuse (pool return, static child re-attach). Serialized
		/// data and the ViewId are untouched.
		/// </summary>
		private void ResetState()
		{
			if (State != ViewState.Hidden)
			{
				Debug.LogWarning($"'{name}' reset while {State}; its resources were never released. Tear it down first.", this);
				State = ViewState.Hidden;
			}

			CancelDelayedStart();
			if (_animatableContent != null)
			{
				_animatableContent.localScale = Vector3.one;
				_animatableContent.localRotation = Quaternion.identity;
				_animatableContent.anchoredPosition = Vector2.zero;
			}

			_animator.EnsureLifetime();
		}

		private void CancelDelayedStart()
		{
			if (_delayedStartCts == null) return;
			_delayedStartCts.Cancel();
			_delayedStartCts.Dispose();
			_delayedStartCts = null;
		}

		// =====================================================================
		// PRIVATE — static children
		// =====================================================================

		/// <summary>
		/// A cycle in the static hierarchy is a view listing one of its own ancestors. The
		/// ancestors are exactly the views on the current recursion path, so walking the
		/// <see cref="_parentView"/> chain replaces a visited set; the depth cap is a belt
		/// for a chain that was corrupted before we got here.
		/// </summary>
		private void AttachStaticChildrenRecursive(Container container, UIView root, int depth)
		{
			const int maxDepth = 32;

			for (int i = 0; i < _staticViews.Count; i++)
			{
				StaticViewEntry entry = _staticViews[i];
				UIView child = entry.View;
				if (child == null) continue;

				if (child == this)
				{
					Debug.LogError($"Self-reference detected: View '{name}' cannot be its own static child. Skipping.", this);
					continue;
				}

				if (depth >= maxDepth || IsOnPathToRoot(child, root))
				{
					Debug.LogError($"Cycle detected in static view hierarchy involving '{child.name}'. Skipping.", this);
					continue;
				}

				// Already registered (pool reuse): reset so it can show again, never double-register.
				if (_host.IsRegistered(child))
				{
					child.ResetState();
					child.gameObject.SetActive(entry.SetActive);
					continue;
				}

				child.Lifecycle().Attach(_host, this, container);
				child.gameObject.SetActive(entry.SetActive);
				_host.RegisterStatic(child, this);

				child.AttachStaticChildrenRecursive(container, root, depth + 1);
			}
		}

		/// <summary>True when <paramref name="candidate"/> is this view or any ancestor up to <paramref name="root"/>.</summary>
		private bool IsOnPathToRoot(UIView candidate, UIView root)
		{
			for (UIView v = this; v != null; v = v._parentView)
			{
				if (ReferenceEquals(v, candidate)) return true;
				if (ReferenceEquals(v, root)) break;
			}

			return false;
		}

		protected virtual void OnDestroy()
		{
			// Settle on unexpected destruction (scene unload, direct Destroy): release
			// registered resources (bus subscriptions, listeners) and drop system
			// bookkeeping. Children settle via their own OnDestroy — Unity's destroy
			// cascade visits every descendant. Teardown is idempotent; on system-driven
			// closes it has already run and this is a no-op.
			Teardown();
			_animator.Dispose();

			_host?.NotifyDestroyedExternally(this);
		}
	}

	/// <summary>
	/// Generic UIView with typed context. Same pattern as V1 UIScreen{TContext} and UIFragment{TContext}.
	/// </summary>
	public abstract class UIView<TContext> : UIView where TContext : UIContext, new()
	{
		public new TContext Context => base.Context as TContext;

		public sealed override void SetContext(UIContext context)
		{
			// Null semantics:
			//  - If the view already has a context, KEEP it. This is the parent -> child
			//    sharing path: a parent passes its own context down to a child (static or
			//    dynamic) by showing the child with no context of its own, and the child
			//    retains whatever it was given. It is also the resume path: a paused
			//    fragment resurfacing from under another view keeps its data.
			//  - If the view has NO context yet, give it a fresh default so a typed view
			//    never observes null after a show.
			// Nothing clears the context on teardown either: a pooled instance keeps its
			// last context until the next show passes a new one.
			if (context == null && Context == null)
			{
				base.Context = new TContext();
			}

			if (context is TContext typedContext)
			{
				base.Context = typedContext;
			}
			else if (context != null)
			{
				Debug.LogError(
					$"Invalid context type for view '{gameObject.name}'. Expected {typeof(TContext).Name} but got {context.GetType().Name}.",
					this);
			}

			base.SetContext(context);
		}
	}
}
