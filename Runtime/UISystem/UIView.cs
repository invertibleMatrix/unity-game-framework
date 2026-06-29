using AK.Systems.Animations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

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
	/// All animation is async (UniTask), wrapping DOTween internally. 
	/// Exceptions propagate through async/await for proper stack traces.
	/// </summary>
	[RequireComponent(typeof(CanvasGroup))]
	public abstract class UIView : MonoBehaviour
	{
		// --- Serialized Configuration ---

		[Title("Identifier")] [SerializeField, Tooltip("Unique ID for this view variant. Keep empty for default.")]
		private string _viewId = "";

		[Title("Stack Behaviour")] [SerializeField]
		private ViewStackBehaviour _stackBehaviour = ViewStackBehaviour.DoNothing;

		[Title("Animation Strategy (direct)")]
		[SerializeField, Tooltip("Animation strategy applied directly on this view. Migrated from _animationConfig via editor script.")]
		private AnimationStrategy _animationStrategy;

		[Title("Stack Orchestration")]
		[SerializeField, Tooltip("If true, this view's hide animation runs in parallel with the next view's show animation. Migrated from _animationConfig.")]
		private bool _playInParallelWithPrevious;

		[Title("Animation Target")]
		[SerializeField, Tooltip("The child containing all visual elements to animate. Falls back to this RectTransform.")]
		private RectTransform _animatableContent;

		[Title("Fragment Container")]
		[SerializeField, Tooltip("Transform where dynamically spawned fragments are placed. Falls back to this transform.")]
		private Transform _fragmentContainer;

		[Title("Pooling (Dynamic Views Only)")] [SerializeField, Tooltip("Return to pool on close instead of destroying. Ignored for static views.")]
		private bool _returnToPoolOnClose;

		[SerializeField, Tooltip("Allow multiple instances active simultaneously. Ignored for static views.")]
		private bool _allowMultipleInstances;

		[Title("Background Overlay")] [SerializeField]
		private bool _showBackgroundOverlay;

		[Title("Entry Position")] [SerializeField, Tooltip("If checked, the view will animate to its current anchored position instead of zero.")]
		private bool _lockEntryPosition;

		[Title("Static Fragments")] [SerializeField, Tooltip("Fragment views pre-placed in this view's hierarchy.")]
		private List<StaticViewEntry> _staticViews = new();

		// --- Injected ---
		[Inject] protected Container _diContainer;

		// --- Internal State ---
		internal ViewStackBehaviour? _overriddenStackBehaviour;
		internal UIChannel?          _overriddenChannel;

		private UISystem				_uiSystem;
		private CancellationTokenSource _animationCts;
		private CancellationTokenSource _destroyCts;
		private Vector2                 _entryPosition = Vector2.zero;
		private GameObject              _darkBg;
		private Texture2D               _bgTexture;
		private Sprite                  _bgSprite;
		private Canvas                  _tutorialCanvas;
		private GraphicRaycaster        _tutorialRaycaster;
		private bool                    _isResourcesRegistered;
		private bool                    _isCleanedUp;
		private bool                    _isShowComplete;

		protected UIView    _parentView;

		// --- Public Properties ---

		public string                         ViewId                 => _viewId;
		public ViewStackBehaviour             StackBehaviour         => _overriddenStackBehaviour ?? _stackBehaviour;
		public IAnimationStrategy             AnimationStrategy      => _animationStrategy;
		public bool                           PlayInParallelWithPrevious => _playInParallelWithPrevious;
		public bool                           NoAnimation            => _animationStrategy == null;
		public bool                           ReturnToPoolOnClose    => _returnToPoolOnClose;
		public bool                           AllowMultipleInstances => _allowMultipleInstances;
		public bool                           IsVisible              { get; private set; }
		public CanvasGroup                    CanvasGroup            { get; private set; }
		public RectTransform                  RectTransform          { get; private set; }
		public Transform                      FragmentContainer      => _fragmentContainer;
		public UIContext                      Context                { get; protected set; }
		public UIView                         ParentView             => _parentView;
		public IReadOnlyList<StaticViewEntry> StaticViews            => _staticViews;
		public IUISystem                      UISystem               => _uiSystem;

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
			ResetViewId();
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
		/// Shows a fragment of the specified type within this view's container.
		/// Fire-and-forget — the animation runs in the background.
		/// </summary>
		public TFragment ShowFragment<TFragment>(string viewId = "", UIContext context = null,
		                                         ViewStackBehaviour? stackBehaviour = null,
		                                         Action<TFragment> onInit = null)
			where TFragment : UIView
		{
			return _uiSystem.Show<TFragment>(context, this, viewId, null, stackBehaviour, onInit);
		}

		/// <summary>
		/// Shows a fragment and awaits until its show animation completes.
		/// </summary>
		public UniTask<TFragment> ShowFragmentAsync<TFragment>(string viewId = "", UIContext context = null,
		                                                       ViewStackBehaviour? stackBehaviour = null,
		                                                       Action<TFragment> onInit = null,
		                                                       CancellationToken ct = default)
			where TFragment : UIView
		{
			return _uiSystem.ShowAsync<TFragment>(context, this, viewId, null, stackBehaviour, onInit, ct);
		}

		/// <summary>
		/// Closes this view. Fire-and-forget — animation runs in the background.
		/// </summary>
		[Button]
		public void Close(CloseContext context = CloseContext.Normal, Action onClose = null)
		{
			_uiSystem?.Close(this, context, onClose);
		}

		/// <summary>
		/// Closes this view and awaits until the close animation completes.
		/// </summary>
		public UniTask CloseAsync(CloseContext context = CloseContext.Normal, CancellationToken ct = default)
		{
			return _uiSystem?.CloseAsync(this, context, ct) ?? UniTask.CompletedTask;
		}

		/// <summary>
		/// Navigates back to the previous fragment in this view's history stack.
		/// </summary>
		public void GoBack()
		{
			_uiSystem?.GoBack(this);
		}

		public void SetInteractable(bool value)
		{
			CanvasGroup.interactable = value;
			CanvasGroup.blocksRaycasts = value;
		}

		/// <summary>
		/// Relocates this view to a new anchored position instantly.
		/// Useful for tooltips that need to jump between positions rapidly without
		/// going through the full close/open cycle.
		/// </summary>
		/// <param name="newAnchoredPosition">The new anchored position in parent's local space.</param>
		/// <param name="onInit">Optional callback to reinitialize content for the new position.</param>
		public void Relocate(Vector2 newAnchoredPosition, Action onInit = null)
		{
			onInit?.Invoke();
			RectTransform.anchoredPosition = newAnchoredPosition;
		}

		/// <summary>
		/// Relocates this view to a new anchored position instantly with a new context.
		/// Useful for tooltips that need to jump between positions rapidly while updating content.
		/// </summary>
		/// <param name="newAnchoredPosition">The new anchored position in parent's local space.</param>
		/// <param name="newContext">New context data to apply at the new position.</param>
		/// <param name="onInit">Optional callback to reinitialize content for the new position.</param>
		public void Relocate(Vector2 newAnchoredPosition, UIContext newContext, Action onInit = null)
		{
			SetContext(newContext);
			Relocate(newAnchoredPosition, onInit);
		}

		// =====================================================================
		// BACKGROUND OVERLAY
		// =====================================================================

		[Button]
		public virtual void ShowBackgroundOverlay(float alpha = UIConstants.DEFAULT_OVERLAY_ALPHA, bool blockRayCasts = true)
		{
			if (_darkBg != null)
			{
				var img = _darkBg.GetComponent<Image>();
				img.raycastTarget = blockRayCasts;
				img.color = new Color(0f, 0f, 0f, alpha);
				img.canvasRenderer.SetAlpha(0f);
				img.CrossFadeAlpha(1f, UIConstants.OVERLAY_FADE_IN_DURATION, false);
				return;
			}

			if (_bgTexture != null)
			{
				if (Application.isPlaying) Destroy(_bgTexture);
				else DestroyImmediate(_bgTexture);
				_bgTexture = null;
			}

			_bgTexture = new Texture2D(1, 1);
			_bgTexture.SetPixel(0, 0, Color.black);
			_bgTexture.Apply();

			_darkBg = new GameObject("ViewBackground");
			Image image = _darkBg.AddComponent<Image>();
			image.raycastTarget = blockRayCasts;
			RectTransform bgRect = image.rectTransform;

			_darkBg.transform.SetParent(transform, false);
			_darkBg.transform.SetSiblingIndex(0);

			// Compute coverage from the nearest parent canvas
			Canvas parentCanvas = GetComponentInParent<Canvas>();
			if (parentCanvas != null)
			{
				RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
				Vector3[] canvasCorners = new Vector3[4];
				canvasRect.GetWorldCorners(canvasCorners);

				Vector2 bottomLeft = RectTransform.InverseTransformPoint(canvasCorners[0]);
				Vector2 topRight = RectTransform.InverseTransformPoint(canvasCorners[2]);

				Vector2 center = (bottomLeft + topRight) * 0.5f;
				Vector2 size = (topRight - bottomLeft) * 4f; // Generous padding

				bgRect.anchorMin = new Vector2(0.5f, 0.5f);
				bgRect.anchorMax = new Vector2(0.5f, 0.5f);
				bgRect.pivot = new Vector2(0.5f, 0.5f);
				bgRect.anchoredPosition = center;
				bgRect.sizeDelta = size;
			}

			Rect rect = new Rect(0, 0, _bgTexture.width, _bgTexture.height);
			_bgSprite = Sprite.Create(_bgTexture, rect, new Vector2(0.5f, 0.5f), 1f);
			image.material.mainTexture = _bgTexture;
			image.sprite = _bgSprite;

			image.color = new Color(0f, 0f, 0f, alpha);
			image.canvasRenderer.SetAlpha(0f);
			image.CrossFadeAlpha(1f, UIConstants.OVERLAY_FADE_IN_DURATION, false);

			_darkBg.transform.localScale = Vector3.one;
		}

		public void HideBackgroundOverlay()
		{
			if (_darkBg == null) return;

			var image = _darkBg.GetComponent<Image>();
			if (image == null) return;
			image.raycastTarget = false;
			image.CrossFadeAlpha(UIConstants.ZERO_ALPHA, UIConstants.OVERLAY_FADE_OUT_DURATION, false);
		}

		public void DestroyBackgroundOverlay()
		{
			if (_darkBg != null)
			{
				Destroy(_darkBg);
				_darkBg = null;
			}

			if (_bgSprite != null)
			{
				if (Application.isPlaying) Destroy(_bgSprite);
				else DestroyImmediate(_bgSprite);
				_bgSprite = null;
			}

			if (_bgTexture != null)
			{
				if (Application.isPlaying) Destroy(_bgTexture);
				else DestroyImmediate(_bgTexture);
				_bgTexture = null;
			}
		}

		// =====================================================================
		// TUTORIAL MODE
		// =====================================================================

		[Button]
		public virtual void SetupTutorialMode()
		{
			CleanupTutorialMode();
			ShowBackgroundOverlay(UIConstants.TUTORIAL_OVERLAY_ALPHA, true);

			if (HasChannel)
			{
				Channel.Canvas.overrideSorting = true;
				Channel.Canvas.sortingOrder = (int)UIChannel.Overlay + 1;
			}
			else
			{
				if (_parentView == null)
				{
					Debug.LogError(
						$"Tutorial mode cannot be set up on view '{name}' because it has no parent view. " +
						"Non-channel views must be children of a channel-owning screen.");
				}
				else if (!_parentView.HasChannel)
				{
					Debug.LogError(
						$"Tutorial mode cannot be set up on view '{name}' because its parent '{_parentView.name}' has no UIChannel. " +
						"Non-channel views must be children of a channel-owning screen.");
				}

				_tutorialCanvas = gameObject.AddComponent<Canvas>();
				_tutorialCanvas.overrideSorting = true;
				Canvas parentCanvas = _parentView.Channel != null ? _parentView.Channel.Canvas : null;
				_tutorialCanvas.sortingOrder = (parentCanvas != null ? parentCanvas.sortingOrder : 0) + 1;
				_tutorialRaycaster = gameObject.AddComponent<GraphicRaycaster>();
			}
		}

		[Button]
		public virtual void CleanupTutorialMode()
		{
			HideBackgroundOverlay();

			if (_tutorialRaycaster != null)
			{
				if (Application.isPlaying) Destroy(_tutorialRaycaster);
				else DestroyImmediate(_tutorialRaycaster);
				_tutorialRaycaster = null;
			}

			if (_tutorialCanvas != null)
			{
				if (Application.isPlaying) Destroy(_tutorialCanvas);
				else DestroyImmediate(_tutorialCanvas);
				_tutorialCanvas = null;
			}

			if (HasChannel)
			{
				Channel.Canvas.overrideSorting = false;
				Channel.Canvas.sortingOrder = (int)Channel.SortOrder;
			}
		}

		// =====================================================================
		// INTERNAL — Called by UIViewSystem
		// =====================================================================

		internal void InternalInitialize(UISystem uiSystem, UIView parentView)
		{
			_uiSystem = uiSystem;
			_parentView = parentView;

			CanvasGroup = GetComponent<CanvasGroup>();
			RectTransform = GetComponent<RectTransform>();
			Channel = GetComponent<UIViewChannel>();

			if (_animatableContent == null) _animatableContent = RectTransform;
			if (_fragmentContainer == null) _fragmentContainer = transform;

			if (_lockEntryPosition && _entryPosition == Vector2.zero)
			{
				_entryPosition = _animatableContent.anchoredPosition;
			}

			// Create a CTS tied to this GameObject's lifetime.
			// All animation tasks link to this so they auto-cancel on Destroy.
			if (_destroyCts == null || _destroyCts.IsCancellationRequested)
			{
				_destroyCts?.Dispose();
				_destroyCts = new CancellationTokenSource();
			}
		}

		internal void InitializeStaticChildren(Container diContainer)
		{
			InitializeStaticChildrenRecursive(diContainer, new HashSet<UIView>());
		}

		private void InitializeStaticChildrenRecursive(Container diContainer, HashSet<UIView> visited)
		{
			foreach (var entry in _staticViews)
			{
				if (entry.View == null) continue;

				if (entry.View == this)
				{
					Debug.LogError($"Self-reference detected: View '{name}' cannot be its own static child. Skipping.", this);
					continue;
				}

				if (visited.Contains(entry.View))
				{
					Debug.LogError($"Cycle detected in static view hierarchy involving '{entry.View.name}'. Skipping.", this);
					continue;
				}

				// Idempotent: if this static child is already registered (e.g., pool reuse),
				// just reset its state so it can be shown again. Don't double-register.
				if (_uiSystem.IsViewRegistered(entry.View))
				{
					// Reset state for re-initialization without clearing ViewId.
					// Static children must preserve their ViewId unlike pooled instances.
					entry.View.ResetState();
					entry.View.gameObject.SetActive(entry.SetActive);
					continue;
				}

				entry.View.Inject(diContainer);
				entry.View.InternalInitialize(_uiSystem, this);
				entry.View.gameObject.SetActive(entry.SetActive);
				_uiSystem.RegisterStaticView(entry.View, this);

				visited.Add(entry.View);
				entry.View.InitializeStaticChildrenRecursive(diContainer, visited);
				visited.Remove(entry.View);
			}
		}

		/// <summary>
		/// Full SHOW lifecycle. Used when a view is being opened for the first time or re-opened.
		/// Order: SetActive → OnPrepareShow → RegisterResources → [animation] → IsVisible → OnShow
		/// </summary>
		internal async UniTask InternalShowAsync(CancellationToken ct = default, bool immediate = false)
		{
			_isCleanedUp = false;
			_isShowComplete = false;
			gameObject.SetActive(true);
			OnPrepareShow();

			if (!_isResourcesRegistered)
			{
				_isResourcesRegistered = true;
				RegisterResources();
			}

			if (immediate || NoAnimation)
			{
				CanvasGroup.alpha = 1f;
				if (_showBackgroundOverlay) ShowBackgroundOverlay();
				IsVisible = true;
				_isShowComplete = true;
				OnShow();
				ShowStaticChildrenOnStart();
				return;
			}

			CancelCurrentAnimation();
			_animationCts = CreateLinkedAnimationCts(ct);

			try
			{
				await AnimationStrategy.PlayShowAsync(
					_animatableContent, CanvasGroup, _entryPosition, _animationCts.Token);
			}
			catch (OperationCanceledException)
			{
				// Show animation was cancelled (e.g., view destroyed mid-animation or rapid close).
				// OnShow will never run. Unregister resources now so that InternalHideAsync/InternalCleanup
				// won't call OnPrepareHide/OnHide on a view that never completed showing.
				UnRegisterResourcesSafe();
				return;
			}

			if (_showBackgroundOverlay) ShowBackgroundOverlay();
			IsVisible = true;
			_isShowComplete = true;
			OnShow();
			ShowStaticChildrenOnStart();
		}

		/// <summary>
		/// Full HIDE lifecycle. Used when a view is being closed/destroyed.
		/// Order: OnPrepareHide → [animation] → IsVisible=false → OnHide → UnRegisterResources
		/// </summary>
		internal async UniTask InternalHideAsync(bool immediate = false, CancellationToken ct = default)
		{
			OnPrepareHide();

			if (_showBackgroundOverlay) HideBackgroundOverlay();

			if (immediate || NoAnimation)
			{
				CanvasGroup.alpha = 0f;
				gameObject.SetActive(false);
				IsVisible = false;
				OnHide();
				UnRegisterResourcesSafe();
				return;
			}

			CancelCurrentAnimation();
			_animationCts = CreateLinkedAnimationCts(ct);

			try
			{
				await AnimationStrategy.PlayHideAsync(
					_animatableContent, CanvasGroup, _animationCts.Token);
			}
			catch (OperationCanceledException)
			{
				// Hide animation was cancelled — still need to finalize state.
				// InternalCleanup will run the full teardown. Just ensure visibility is correct.
				CanvasGroup.alpha = 0f;
				gameObject.SetActive(false);
				IsVisible = false;
				OnHide();
				UnRegisterResourcesSafe();
				return;
			}

			gameObject.SetActive(false);
			IsVisible = false;
			OnHide();
			UnRegisterResourcesSafe();
		}

		/// <summary>
		/// PAUSE hide. Used when this view is being hidden because a higher-priority view was pushed on top.
		/// Resources stay registered — the view is still logically "alive", just not visible.
		/// Order: [animation] → IsVisible=false
		/// OnPause is called by the SYSTEM, not here, because the system controls the timing.
		/// </summary>
		internal async UniTask InternalPauseHideAsync(bool immediate = false, CancellationToken ct = default)
		{
			if (_showBackgroundOverlay) HideBackgroundOverlay();

			if (immediate || NoAnimation)
			{
				CanvasGroup.alpha = 0f;
				gameObject.SetActive(false);
				IsVisible = false;
				return;
			}

			CancelCurrentAnimation();
			_animationCts = CreateLinkedAnimationCts(ct);

			try
			{
				await AnimationStrategy.PlayHideAsync(
					_animatableContent, CanvasGroup, _animationCts.Token);
			}
			catch (OperationCanceledException)
			{
				CanvasGroup.alpha = 0f;
				gameObject.SetActive(false);
				IsVisible = false;
				return;
			}

			gameObject.SetActive(false);
			IsVisible = false;
		}

		/// <summary>
		/// RESUME show. Used when this view is being shown again after the higher-priority view was closed.
		/// Resources are already registered — no lifecycle hooks for show, just animation.
		/// Order: SetActive → [animation] → IsVisible=true
		/// OnResume is called by the SYSTEM, not here, because the system controls the timing.
		/// </summary>
		internal async UniTask InternalResumeShowAsync(CancellationToken ct = default, bool immediate = false)
		{
			gameObject.SetActive(true);

			if (immediate || NoAnimation)
			{
				CanvasGroup.alpha = 1f;
				if (_showBackgroundOverlay) ShowBackgroundOverlay();
				IsVisible = true;
				return;
			}

			CancelCurrentAnimation();
			_animationCts = CreateLinkedAnimationCts(ct);

			try
			{
				await AnimationStrategy.PlayShowAsync(
					_animatableContent, CanvasGroup, _entryPosition, _animationCts.Token);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			if (_showBackgroundOverlay) ShowBackgroundOverlay();
			IsVisible = true;
		}

		/// <summary>
		/// Force teardown. Kills all animations and runs the full close lifecycle if not already run.
		/// Called when the view is being destroyed or returned to pool.
		/// Idempotent — safe to call multiple times.
		/// Order: CancelAnimation → KillTweens → OnPrepareHide → OnHide → UnRegisterResources → NullifyContext
		/// </summary>
		internal void InternalCleanup()
		{
			if (_isCleanedUp) return;
			_isCleanedUp = true;

			CancelCurrentAnimation();
			_animatableContent?.DOKill();
			if (CanvasGroup != null) CanvasGroup.DOKill();

			// Run the close lifecycle hooks if they haven't already been run by InternalHideAsync.
			// We use _isResourcesRegistered as the indicator: if resources are still registered,
			// the close hooks (OnPrepareHide/OnHide/UnRegister) haven't been called yet.
			// This covers: visible views, paused views (hidden but resources still live), and 
			// views mid-show-animation that were never fully shown.
			if (_isResourcesRegistered)
			{
				OnPrepareHide();
				IsVisible = false;
				OnHide();
			}

			UnRegisterResourcesSafe();
			// NullifyContext(); //TODO: Take another Look here why should we nullify it in first place
		}

		internal void MoveContentOffScreen()
		{
			CanvasGroup.alpha = 0f;
		}

		internal void PrepareForShowAnimation()
		{
			if (NoAnimation) return;
			CanvasGroup.alpha = 0f;
		}

		internal void Inject(Container diContainer)
		{
			GameObjectInjector.InjectRecursive(gameObject, diContainer);
		}

		internal         void SetViewId(string id) => _viewId = id;
		internal         void ResetViewId()        => _viewId = "";
		internal virtual void NullifyContext()     { }

		/// <summary>
		/// Idempotent unregister — safe to call multiple times.
		/// Guards against double-unregister when InternalHideAsync and InternalCleanup both run.
		/// </summary>
		private void UnRegisterResourcesSafe()
		{
			if (!_isResourcesRegistered) return;
			_isResourcesRegistered = false;
			UnRegisterResources();
		}

		// =====================================================================
		// PRIVATE
		// =====================================================================

		private void ShowStaticChildrenOnStart()
		{
			_uiSystem.ShowStaticChildrenBatch(this, _staticViews);
		}

		private void CancelCurrentAnimation()
		{
			_animationCts?.Cancel();
			_animationCts?.Dispose();
			_animationCts = null;
		}

		/// <summary>
		/// Creates a new CTS linked to both the caller's token and the destroy token.
		/// If the GameObject is destroyed, all in-flight animations auto-cancel.
		/// </summary>
		private CancellationTokenSource CreateLinkedAnimationCts(CancellationToken ct)
		{
			if (_destroyCts != null && !_destroyCts.IsCancellationRequested)
			{
				return CancellationTokenSource.CreateLinkedTokenSource(ct, _destroyCts.Token);
			}

			return CancellationTokenSource.CreateLinkedTokenSource(ct);
		}

		/// <summary>
		/// Resets internal state without clearing the ViewId. Used for static children
		/// that need their state reset on re-initialization but must preserve their ViewId.
		/// </summary>
		private void ResetState()
		{
			_isResourcesRegistered = false;
			_isCleanedUp = false;
			_isShowComplete = false;
			if (_animatableContent != null)
			{
				_animatableContent.localScale = Vector3.one;
				_animatableContent.localRotation = Quaternion.identity;
				_animatableContent.anchoredPosition = Vector2.zero;
			}

			DestroyBackgroundOverlay();
			CleanupTutorialMode();

			if (_destroyCts == null || _destroyCts.IsCancellationRequested)
			{
				_destroyCts?.Dispose();
				_destroyCts = new CancellationTokenSource();
			}
		}
		
		protected virtual void OnDestroy()
		{
			// Cancel the destroy CTS — this kills any fire-and-forget animation tasks
			// that might still be running, preventing use-after-destroy errors.
			_destroyCts?.Cancel();
			_destroyCts?.Dispose();
			_destroyCts = null;

			CancelCurrentAnimation();
			DestroyBackgroundOverlay();
			CleanupTutorialMode();
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

		internal override void NullifyContext()
		{
			base.Context = null;
		}
	}
}