using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AK.Systems.UI;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AK.Systems
{
	/// <summary>
	/// Unified UI system for V2. Replaces both UISystem and FragmentSystem from V1.
	///
	/// Architecture:
	/// - Views with <see cref="UIViewChannel"/> component are "screens".
	///   They get their own Canvas, are pushed onto channel-based stacks, and manage sorting.
	/// - Views without UIChannel are "fragments".
	///   They live inside a parent view's container and are tracked in per-parent history stacks.
	/// - A DefaultChannel (sort order 0) always exists as a fallback.
	/// - All animation is async via UniTask wrapping DOTween. Exceptions propagate through await.
	/// - Show() is fire-and-forget (no compiler warnings). ShowAsync() awaits animation completion.
	/// </summary>
	public sealed class UISystem : MonoBehaviour, IUISystem, IDisposable
	{
		// =================================================================
		// SERIALIZED
		// =================================================================

		[InlineEditor, SerializeField]
		private UIViewRepository _repository;

		[SerializeField] private Transform _viewsContainer;
		[SerializeField] private Camera    _uiCamera;
		[SerializeField] private bool      _spawnDefaultOverlayView = true;
		[SerializeField] private bool      _ensureEventSystem = true;

		[Inject] internal Container _diContainer;

		// =================================================================
		// INTERNAL STATE
		// =================================================================

		/// <summary>
		/// Channel stacks keyed by UIChannel.SortOrder.
		/// The default channel (sort order 0) is always present.
		/// </summary>
		private readonly Dictionary<UIChannel, Stack<UIView>> _channelStacks = new();

		/// <summary>
		/// Per-parent history stacks for fragments (views without UIChannel).
		/// </summary>
		private readonly Dictionary<UIView, Stack<UIView>> _historyStacks = new();

		/// <summary>
		/// Per-parent pending show task. Serializes concurrent show operations on the same parent
		/// to prevent two animations from fighting over the same CanvasGroup/history stack.
		/// </summary>
		private readonly Dictionary<UIView, UniTaskCompletionSource> _pendingShowTasks = new();

		/// <summary>
		/// Central registry of all active views.
		/// </summary>
		private readonly Dictionary<UIView, ViewRecord> _viewRegistry = new();

		/// <summary>
		/// Lookup cache for fast prefab resolution by (Type, ViewId).
		/// Built lazily on first access and invalidated when repository changes.
		/// </summary>
		private Dictionary<(Type, string), UIView> _prefabLookup;

		/// <summary>
		/// Guards against double-close on screens (like V1's _closingScreens).
		/// </summary>
		private readonly HashSet<UIView> _closingViews = new();

		private readonly ViewPool _viewPool = new();

		// =================================================================
		// VIEW RECORD
		// =================================================================

		private class ViewRecord
		{
			public UIView       Instance  { get; }
			public UIView       Parent    { get; }
			public bool         IsStatic  { get; }
			public bool         IsDynamic => !IsStatic;
			public List<UIView> Children  { get; } = new();

			public ViewRecord(UIView instance, UIView parent, bool isStatic)
			{
				Instance = instance;
				Parent = parent;
				IsStatic = isStatic;
			}

			public void AddChild(UIView child)
			{
				if (child != null && !Children.Contains(child))
					Children.Add(child);
			}

			public void RemoveChild(UIView child)
			{
				Children.Remove(child);
			}
		}

		// =================================================================
		// LIFECYCLE
		// =================================================================

		private void Awake()
		{
			// Ensure the default channel stack always exists
			_channelStacks[UIChannel.HUD] = new Stack<UIView>();

			if (_ensureEventSystem && FindFirstObjectByType<EventSystem>() == null)
			{
				var go = new GameObject("EventSystem");
				go.transform.SetParent(_viewsContainer);
				go.AddComponent<EventSystem>();
				go.AddComponent<StandaloneInputModule>();
			}

			if (_spawnDefaultOverlayView)
			{
				Show<UIViewOverlay>();	
			}
		}

		public void Dispose()
		{
			_viewPool.Clear();
		}

		// =================================================================
		// IUIViewSystem — SHOW (Fire-and-Forget)
		// =================================================================

		public TView Show<TView>(UIContext context = null, UIView parent = null, string viewId = "",
		                         UIChannel? channelOverride = null, ViewStackBehaviour? stackBehaviour = null, Action<TView> onInit = null)
			where TView : UIView
		{
			return Show(typeof(TView), context, parent, viewId, channelOverride, stackBehaviour, onInit);
		}

		public TView Show<TView>(Type type, UIContext context = null, UIView parent = null, string viewId = "",
		                         UIChannel? channelOverride = null, ViewStackBehaviour? stackBehaviour = null, Action<TView> onInit = null)
			where TView : UIView
		{
			var (view, animationTask) = PrepareAndRegisterView(type, context, parent, viewId, channelOverride, stackBehaviour, onInit);
			if (view != null)
			{
				animationTask.Forget();
			}

			return view;
		}

		// =================================================================
		// IUIViewSystem — SHOW ASYNC
		// =================================================================

		public async UniTask<TView> ShowAsync<TView>(UIContext context = null, UIView parent = null, string viewId = "",
		                                             UIChannel? channelOverride = null, ViewStackBehaviour? stackBehaviour = null,
		                                             Action<TView> onInit = null,
		                                             CancellationToken ct = default)
			where TView : UIView
		{
			return await ShowAsync(typeof(TView), context, parent, viewId, channelOverride, stackBehaviour, onInit, ct);
		}

		public async UniTask<TView> ShowAsync<TView>(Type type, UIContext context = null, UIView parent = null, string viewId = "",
		                                             UIChannel? channelOverride = null, ViewStackBehaviour? stackBehaviour = null,
		                                             Action<TView> onInit = null,
		                                             CancellationToken ct = default)
			where TView : UIView
		{
			var (view, animationTask) = PrepareAndRegisterView(type, context, parent, viewId, channelOverride, stackBehaviour, onInit);
			if (view == null) return null;

			// Await the full animation pipeline — this is the key UniTask advantage.
			// Exceptions propagate with full async stack traces.
			await animationTask.AttachExternalCancellation(ct);
			return view;
		}

		// =================================================================
		// IUIViewSystem — CLOSE
		// =================================================================

		public void Close(UIView view, CloseContext context = CloseContext.Normal, Action onClose = null)
		{
			// Only fire the callback when the close will actually do something - a double-close
			// or unregistered view early-returns inside CloseInternalAsync and "closed" nothing.
			bool willClose = view != null && _viewRegistry.ContainsKey(view) && !_closingViews.Contains(view);
			CloseInternalAsync(view, context, false).ContinueWith(() =>
			{
				if (willClose) onClose?.Invoke();
			}).Forget();
		}

		public UniTask CloseAsync(UIView view, CloseContext context = CloseContext.Normal, CancellationToken ct = default)
		{
			return CloseInternalAsync(view, context, false, ct);
		}

		public void CloseImmediate(UIView view, CloseContext context = CloseContext.Normal, Action onClose = null)
		{
			bool willClose = view != null && _viewRegistry.ContainsKey(view) && !_closingViews.Contains(view);
			CloseInternalAsync(view, context, true).ContinueWith(() =>
			{
				if (willClose) onClose?.Invoke();
			}).Forget();
		}

		// =================================================================
		// IUIViewSystem — RAPID SHOW/CLOSE (For tooltips)
		// =================================================================

		public TView ShowImmediate<TView>(UIContext context = null, UIView parent = null, string viewId = "",
		                                  UIChannel? channelOverride = null, ViewStackBehaviour? stackBehaviour = null, Action<TView> onInit = null)
			where TView : UIView
		{
			return ShowImmediate(typeof(TView), context, parent, viewId, channelOverride, stackBehaviour, onInit);
		}

		public TView ShowImmediate<TView>(Type type, UIContext context = null, UIView parent = null, string viewId = "",
		                                  UIChannel? channelOverride = null, ViewStackBehaviour? stackBehaviour = null, Action<TView> onInit = null)
			where TView : UIView
		{
			var (view, animTask) = PrepareAndRegisterView(type, context, parent, viewId, channelOverride, stackBehaviour, onInit, immediate: true);
			if (view != null)
			{
				animTask.Forget();
			}

			return view;
		}

		public void DisplayToast(string text)
		{
			Show<UIViewToast>(onInit: toast =>
			{
				toast.Init(0,text);
			});
		}

		public void DisplayBanner(string text, string variantId = "")
		{
			Show<UIViewBanner>(viewId: variantId, onInit: banner =>
			{
				banner.Init(text);
			});
		}

		// =================================================================
		// IUIViewSystem — GO BACK
		// =================================================================

		public void GoBack(UIView parentView)
		{
			GoBackInternalAsync(parentView).Forget();
		}

		public UniTask GoBackAsync(UIView parentView, CancellationToken ct = default)
		{
			return GoBackInternalAsync(parentView, ct:ct);
		}

		// =================================================================
		// IUIViewSystem — QUERY
		// =================================================================

		public TView GetView<TView>(string viewId = "") where TView : UIView
		{
			var id = viewId ?? string.Empty;
			var type = typeof(TView);

			// Check channel stacks first
			foreach (var stack in _channelStacks.Values)
			{
				var match = stack.FirstOrDefault(v => v != null && v.GetType() == type && v.ViewId == id);
				if (match != null) return match as TView;
			}

			// Then check registry - exclude views currently closing to prevent operating on dying views
			return _viewRegistry.Values
			                    .FirstOrDefault(r => r.Instance != null && r.Instance.GetType() == type && r.Instance.ViewId == id && !_closingViews.Contains(r.Instance))?
			                    .Instance as TView;
		}

		// =================================================================
		// STATIC FRAGMENT REGISTRATION (Internal — not on IUIViewSystem)
		// =================================================================

		internal void RegisterStaticView(UIView view, UIView parent)
		{
			var record = new ViewRecord(view, parent, isStatic: true);

			if (parent != null && _viewRegistry.TryGetValue(parent, out var parentRecord))
			{
				parentRecord.AddChild(view);
			}

			if (!_viewRegistry.TryAdd(view, record))
			{
				Debug.LogWarning($"Static view '{view.name}' is already registered.", view);
			}
		}

		internal void ShowExistingView(UIView view, string viewId = "", UIContext context = null,
		                               ViewStackBehaviour? stackBehaviour = null)
		{
			if (view == null || view.gameObject == null)
			{
				Debug.LogError("Cannot show view: view or its GameObject is null.");
				return;
			}

			if (!_viewRegistry.TryGetValue(view, out var record))
			{
				Debug.LogError($"Cannot show view '{view.name}': not registered.", view);
				return;
			}

			ShowRegisteredViewAsync(view, record.Parent, context, stackBehaviour).Forget();
		}

		/// <summary>
		/// Shows multiple static children in parallel. All children are pushed onto the parent's
		/// history stack synchronously first, then all show animations run simultaneously.
		/// This avoids the sequential _pendingShowTasks serialization that would cause
		/// child N to wait for child N-1's animation before starting.
		/// </summary>
		internal void ShowStaticChildrenBatch(UIView parent, IReadOnlyList<StaticViewEntry> entries)
		{
			if (parent == null || entries == null || entries.Count == 0) return;

			// Ensure the history stack exists
			if (!_historyStacks.TryGetValue(parent, out var history))
			{
				history = new Stack<UIView>();
				_historyStacks[parent] = history;
			}

			var tasks = new List<UniTask>();

			foreach (var entry in entries)
			{
				if (entry.View == null || !entry.ShowOnStart) continue;
				if (!_viewRegistry.TryGetValue(entry.View, out _)) continue;

				// Prepare and push onto history stack synchronously (no stack behaviour between siblings)
				entry.View.PrepareForShowAnimation();
				RemoveFromStack(entry.View, history);
				history.Push(entry.View);

				// Collect animation tasks — they'll all run in parallel
				tasks.Add(entry.View.InternalShowAsync());
			}

			// Fire-and-forget the parallel batch
			UniTask.WhenAll(tasks).Forget();
		}

		internal bool IsViewRegistered(UIView view)
		{
			return view != null && view.gameObject != null && _viewRegistry.ContainsKey(view);
		}

		// =================================================================
		// CORE — Prepare and Register (synchronous instantiation)
		// =================================================================

		/// <summary>
		/// Core preparation logic. Instantiates (or reuses) the view, registers it, configures stacking.
		/// Returns the view and a UniTask representing the full animation pipeline.
		/// The caller decides whether to fire-and-forget (Show) or await (ShowAsync).
		/// </summary>
		private (TView view, UniTask animationTask) PrepareAndRegisterView<TView>(
			Type type, UIContext context, UIView parent, string viewId,
			UIChannel? channelOverride = null,
			ViewStackBehaviour? stackBehaviour = null, Action<TView> onInit = null,
			bool immediate = false)
			where TView : UIView
		{
			string id = viewId ?? string.Empty;

			// =================================================================
			//   • Explicit parent → reuse only a static already registered under THAT parent.
			//   • No parent       → reuse any static for (type, id) and re-route it onto its
			//                       own registered host parent (its effective parent).
			//
			// Only when no static is registered do we fall through to the repository
			// (instantiate/clone a prefab). Views mid-close are excluded so we never
			// reuse a view that is currently tearing down.
			// =================================================================
			ViewRecord staticRecord = parent != null
				? _viewRegistry.Values.FirstOrDefault(r =>
					r.Instance != null &&
					r.Instance.GetType() == type &&
					r.Instance.ViewId == id &&
					r.Parent == parent &&
					r.IsStatic &&
					!_closingViews.Contains(r.Instance))
				: _viewRegistry.Values.FirstOrDefault(r =>
					r.Instance != null &&
					r.Instance.GetType() == type &&
					r.Instance.ViewId == id &&
					r.IsStatic &&
					!_closingViews.Contains(r.Instance));

			if (staticRecord != null && staticRecord.Instance is TView staticView)
			{
				UIView effectiveParent = parent ?? staticRecord.Parent;
				onInit?.Invoke(staticView);
				return (staticView, ShowRegisteredViewAsync(staticView, effectiveParent, context, stackBehaviour, immediate));
			}

			// --- Find prefab (repository fallback) ---
			var prefab = FindPrefab<TView>(type, id);
			if (prefab == null)
			{
				Debug.LogError($"View prefab of type {type.Name} with ID '{id}' not found in UIViewRepository.");
				return (null, UniTask.CompletedTask);
			}

			bool isScreen = prefab.GetComponent<UIViewChannel>() != null;

			// --- Resolve parent for fragments ---
			if (!isScreen && parent == null)
			{
				parent = FindBestParentView(channelOverride);
				if (parent == null)
				{
					Debug.LogError($"Cannot show fragment '{type.Name}': no suitable parent found.");
					return (null, UniTask.CompletedTask);
				}
			}

			// --- Check for an existing DYNAMIC instance on this parent ---
			// Static instances are resolved above; this only finds a dynamic instance that
			// must be closed-and-replaced when multiple instances are not allowed.
			ViewRecord existingRecord = _viewRegistry.Values.FirstOrDefault(r =>
				r.Instance.GetType() == type && r.Instance.ViewId == id && r.Parent == parent
				&& !r.IsStatic
				&& !_closingViews.Contains(r.Instance));

			// When replacing an existing dynamic instance, the close must be sequenced BEFORE the
			// new show pipeline: otherwise the old close's resume-of-previous can run after the new
			// show paused that same view, leaving it visible/interactable on top of the new view.
			UniTask replaceCloseTask = UniTask.CompletedTask;
			bool    hasReplaceClose  = false;

			if (existingRecord != null && !prefab.AllowMultipleInstances)
			{
				replaceCloseTask = CloseInternalAsync(existingRecord.Instance, CloseContext.Normal, false);
				hasReplaceClose  = true;
			}

			// --- Instantiate or get from pool ---
			Transform spawnParent = isScreen
				? _viewsContainer
				: (parent != null ? parent.FragmentContainer : _viewsContainer);

			TView newView = _viewPool.Get(prefab, spawnParent);

			if (!string.IsNullOrEmpty(id) && string.IsNullOrEmpty(newView.ViewId))
			{
				newView.SetViewId(id);
			}

			// --- Configure ---
			newView.Inject(_diContainer);
			newView.InternalInitialize(this, parent);
			newView._overriddenStackBehaviour = stackBehaviour;
			newView._overriddenChannel = channelOverride;
			newView.MoveContentOffScreen();

			// --- onInit callback (replaces V1's onPrepare) ---
			onInit?.Invoke(newView);
			newView.SetContext(context);

			// --- Register ---
			var record = new ViewRecord(newView, parent, isStatic: false);
			_viewRegistry.Add(newView, record);

			if (parent != null && _viewRegistry.TryGetValue(parent, out var parentRecord))
			{
				parentRecord.AddChild(newView);
			}

			// --- Initialize static children ---
			newView.InitializeStaticChildren(_diContainer);

			// --- Build the animation pipeline (but don't start it yet) ---
			UniTask animTask;
			if (isScreen)
			{
				animTask = hasReplaceClose
					? AwaitReplaceCloseThen(replaceCloseTask, () => RunScreenShowAsync(newView, channelOverride, immediate))
					: RunScreenShowAsync(newView, channelOverride, immediate);
			}
			else
			{
				animTask = hasReplaceClose
					? AwaitReplaceCloseThen(replaceCloseTask, () => RunFragmentShowAsync(newView, parent, immediate))
					: RunFragmentShowAsync(newView, parent, immediate);
			}

			return (newView, animTask);
		}

		/// <summary>
		/// Awaits a replace-close before starting the show pipeline (see PrepareAndRegisterView).
		/// Close failures are logged but never block the new show.
		/// </summary>
		private async UniTask AwaitReplaceCloseThen(UniTask closeTask, Func<UniTask> showPipeline)
		{
			try
			{
				await closeTask;
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}

			await showPipeline();
		}

		// =================================================================
		// SHOW ANIMATION PIPELINES
		// =================================================================

		/// <summary>
		/// Full show pipeline for a screen: configure Canvas, push to stack,
		/// handle pause of previous screen, play show animation.
		/// </summary>
		private async UniTask RunScreenShowAsync(UIView newView, UIChannel? overrideChannel = null, bool immediate = false, CancellationToken ct = default)
		{
			UIViewChannel channel = newView.Channel;
			UIChannel sortOrder = channel.SortOrder;

			if (overrideChannel != null)
			{
				sortOrder = overrideChannel.Value;
			}

			if (!_channelStacks.ContainsKey(sortOrder))
			{
				_channelStacks[sortOrder] = new Stack<UIView>();
			}

			var stack = _channelStacks[sortOrder];
			stack.Push(newView);

			channel.Initialize(_uiCamera, stack.Count);

			UIView previousView = stack.Count > 1 ? stack.Skip(1).First() : null;

			if (previousView != null)
			{
				bool parallel = !immediate &&
				                previousView.PlayInParallelWithPrevious;

				if (parallel)
				{
					await UniTask.WhenAll(
						HandlePauseAsync(newView, previousView, ct),
						newView.InternalShowAsync(ct, immediate)
					);
				}
				else
				{
					if (!immediate)
						await HandlePauseAsync(newView, previousView, ct);
					else
						PausePreviousScreenImmediate(newView, previousView);

					await newView.InternalShowAsync(ct, immediate);
				}
			}
			else
			{
				await newView.InternalShowAsync(ct, immediate);
			}
		}

		private void PausePreviousScreenImmediate(UIView newView, UIView previousView)
		{
			switch (newView.StackBehaviour)
			{
				case ViewStackBehaviour.PauseAndHideBelow:
				case ViewStackBehaviour.HideBelow:
					previousView.OnPause();
					PauseFragments(previousView);
					previousView.MoveContentOffScreen();
					previousView.SetInteractable(false);
					break;

				case ViewStackBehaviour.PauseOnlyBelow:
					previousView.OnPause();
					PauseFragments(previousView);
					previousView.SetInteractable(false);
					break;

				case ViewStackBehaviour.CloseBelow:
					CloseInternalAsync(previousView, CloseContext.Normal, false).Forget();
					break;

				case ViewStackBehaviour.DoNothing:
				default:
					break;
			}
		}

		/// <summary>
		/// Full show pipeline for a fragment: manage parent's history stack,
		/// handle previous fragment's stack behaviour, play show animation.
		/// Serialized per-parent to prevent concurrent animation conflicts.
		/// </summary>
		private async UniTask RunFragmentShowAsync(UIView newView, UIView parent, bool immediate = false, CancellationToken ct = default)
		{
			// Wait for any pending show on this parent to complete first.
			// This serializes rapid Show calls (e.g., double-tap) so they don't fight.
			if (_pendingShowTasks.TryGetValue(parent, out var pending))
			{
				try
				{
					await pending.Task;
				}
				catch
				{
					/* swallow — we only care about sequencing */
				}
			}

			var completionSource = new UniTaskCompletionSource();
			_pendingShowTasks[parent] = completionSource;

			try
			{
				await RunFragmentShowInternalAsync(newView, parent, immediate, ct);
				completionSource.TrySetResult();
			}
			catch (Exception ex)
			{
				completionSource.TrySetException(ex);
				throw;
			}
			finally
			{
				// Clean up — only remove if it's still our completion source (prevents race with concurrent shows)
				if (_pendingShowTasks.TryGetValue(parent, out var existing) && ReferenceEquals(existing, completionSource))
					_pendingShowTasks.Remove(parent);
			}
		}

		private async UniTask RunFragmentShowInternalAsync(UIView newView, UIView parent, bool immediate, CancellationToken ct)
		{
			if (!_historyStacks.TryGetValue(parent, out var history))
			{
				history = new Stack<UIView>();
				_historyStacks[parent] = history;
			}

			// Capture previous fragment before pushing
			UIView previousFragment = null;
			if (history.Count > 0)
			{
				var top = history.Peek();
				if (top != newView) previousFragment = top;
			}

			// Remove if already in history (bring to top)
			RemoveFromStack(newView, history);
			history.Push(newView);

			newView.PrepareForShowAnimation();

			if (previousFragment == null)
			{
				await newView.InternalShowAsync(ct, immediate);
				return;
			}

			if (immediate)
			{
				PausePreviousFragmentImmediate(newView, previousFragment);
				await newView.InternalShowAsync(ct, immediate);
				return;
			}

			await HandleFragmentStackBehaviourAsync(newView, previousFragment, ct);
		}

		private void PausePreviousFragmentImmediate(UIView newView, UIView previousFragment)
		{
			switch (newView.StackBehaviour)
			{
				case ViewStackBehaviour.HideBelow:
				case ViewStackBehaviour.PauseAndHideBelow:
					previousFragment.OnPause();
					previousFragment.SetInteractable(false);
					previousFragment.MoveContentOffScreen();
					break;

				case ViewStackBehaviour.PauseOnlyBelow:
					previousFragment.OnPause();
					previousFragment.SetInteractable(false);
					break;

				case ViewStackBehaviour.CloseBelow:
					CloseInternalAsync(previousFragment, CloseContext.Normal, false).Forget();
					break;

				case ViewStackBehaviour.DoNothing:
				default:
					break;
			}
		}

		/// <summary>
		/// Shows a registered view (static or existing) within a parent's history stack.
		/// Serialized per-parent to prevent concurrent animation conflicts.
		/// </summary>
		private async UniTask ShowRegisteredViewAsync(UIView view, UIView parent, UIContext context,
		                                              ViewStackBehaviour? stackBehaviour, bool immediate = false, CancellationToken ct = default)
		{
			if (parent != null)
			{
				// Wait for any pending show on this parent to complete first.
				if (_pendingShowTasks.TryGetValue(parent, out var pending))
				{
					try
					{
						await pending.Task;
					}
					catch
					{
						/* swallow — we only care about sequencing */
					}
				}

				UniTaskCompletionSource completionSource = new();
				_pendingShowTasks[parent] = completionSource;

				try
				{
					await ShowRegisteredViewInternalAsync(view, parent, context, stackBehaviour, immediate, ct);
					completionSource.TrySetResult();
				}
				catch (Exception ex)
				{
					completionSource.TrySetException(ex);
					throw;
				}
				finally
				{
					// Clean up — only remove if it's still our completion source (prevents race with concurrent shows)
					if (_pendingShowTasks.TryGetValue(parent, out var existing) && ReferenceEquals(existing, completionSource))
						_pendingShowTasks.Remove(parent);
				}

				return;
			}

			await ShowRegisteredViewInternalAsync(view, parent, context, stackBehaviour, immediate, ct);
		}

		private async UniTask ShowRegisteredViewInternalAsync(UIView view, UIView parent, UIContext context,
		                                                      ViewStackBehaviour? stackBehaviour, bool immediate, CancellationToken ct)
		{
			view._overriddenStackBehaviour = stackBehaviour;
			view.PrepareForShowAnimation();
			view.SetContext(context);

			if (parent == null)
			{
				await view.InternalShowAsync(ct, immediate);
				return;
			}

			if (!_historyStacks.TryGetValue(parent, out var history))
			{
				history = new Stack<UIView>();
				_historyStacks[parent] = history;
			}

			UIView previousView = null;
			if (history.Count > 0)
			{
				var top = history.Peek();
				if (top != view) previousView = top;
			}

			RemoveFromStack(view, history);
			history.Push(view);

			if (previousView != null)
			{
				if (immediate)
				{
					PausePreviousFragmentImmediate(view, previousView);
					await view.InternalShowAsync(ct, immediate);
				}
				else
				{
					await HandleFragmentStackBehaviourAsync(view, previousView, ct);
				}
			}
			else
			{
				await view.InternalShowAsync(ct, immediate);
			}
		}

		// =================================================================
		// CORE — Close Internal
		// =================================================================

		private async UniTask CloseInternalAsync(UIView view, CloseContext context, bool immediate = false, CancellationToken ct = default)
		{
			if (view == null || view.gameObject == null) return;

			// Guard against double-close
			if (_closingViews.Contains(view)) return;

			if (!_viewRegistry.TryGetValue(view, out var record))
			{
				// Not registered — could be already closed via parent
				Debug.LogWarning($"Cannot close view '{view.name}': not registered (may have been closed via parent).");
				return;
			}

			if (view.HasChannel)
			{
				await CloseScreenAsync(view, record, context, immediate, ct);
			}
			else
			{
				await CloseFragmentAsync(view, record, context, immediate, ct);
			}
		}

		private async UniTask CloseScreenAsync(UIView view, ViewRecord record, CloseContext context,
		                                       bool immediate, CancellationToken ct)
		{
			UIChannel sortOrder = view.Channel.SortOrder;

			if (!_channelStacks.TryGetValue(sortOrder, out var stack))
			{
				await DestroyViewAsync(view, record, context, immediate, ct);
				return;
			}

			// CASE 1: Not at the top — remove from stack, destroy immediately
			if (stack.Count == 0 || stack.Peek() != view)
			{
				if (stack.Contains(view))
				{
					RemoveFromStack(view, stack);
					RecomputeChannelSorting(stack);
				}

				await DestroyViewAsync(view, record, context, true, ct);
				return;
			}

			// CASE 2: At the top — play close animation, then resume previous
			stack.Pop();
			RecomputeChannelSorting(stack);
			_closingViews.Add(view);

			UIView previousView = stack.Count > 0 ? stack.Peek() : null;

			try
			{
				bool parallel = !immediate &&
				                previousView != null &&
				                previousView.PlayInParallelWithPrevious;

				if (parallel)
				{
					// Run close and resume in parallel
					await UniTask.WhenAll(
						HideAndDestroyAsync(view, record, context, immediate, ct),
						HandleResumeAsync(view, previousView, immediate, ct)
					);
				}
				else
				{
					// Sequential: close first, then resume
					await HideAndDestroyAsync(view, record, context, immediate, ct);
					if (previousView != null)
					{
						await HandleResumeAsync(view, previousView, immediate, ct);
					}
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Debug.LogError($"Error closing screen '{view.name}': {ex.Message}\n{ex.StackTrace}");
			}
			finally
			{
				_closingViews.Remove(view);
			}
		}

		private async UniTask CloseFragmentAsync(UIView view, ViewRecord record, CloseContext context,
		                                         bool immediate, CancellationToken ct)
		{
			_closingViews.Add(view);

			try
			{
				// Find the parent's history stack
				UIView parent = record.Parent;
				if (parent != null && _historyStacks.TryGetValue(parent, out var history))
				{
					// CASE 1: If it's the top of the stack, use GoBack logic (handles resume naturally)
					if (history.Count > 0 && history.Peek() == view && context == CloseContext.Normal)
					{
						await GoBackInternalAsync(parent, immediate, ct);
						return;
					}

					// CASE 2: Mid-stack removal.
					// Before removing, find the fragment directly below the one being closed.
					// We need this to determine if it should be resumed after removal.
					UIView fragmentBelow = FindBelowInStack(view, history);

					RemoveFromStack(view, history);

					await HideAndDestroyAsync(view, record, context, immediate, ct);

					// Check if the closed fragment was hiding/blocking the one below it.
					// If so, and nothing else in the remaining stack is still covering that fragment,
					// we need to resume it.
					//
					// Example scenario:
					//   Stack (bottom→top): StartGame → Shop(HideBelow) → Toast(DoNothing)
					//   If Shop is closed while Toast is on top, StartGame was hidden by Shop.
					//   Toast has DoNothing so it doesn't cover StartGame.
					//   → StartGame must be resumed (shown back).
					//
					// Counter-example:
					//   Stack: A → B(HideBelow) → C(HideBelow) → Toast
					//   If B is closed, A was hidden by B, but C also hides below.
					//   → A should stay hidden because C still covers it.
					if (fragmentBelow != null && _viewRegistry.ContainsKey(fragmentBelow))
					{
						bool wasHiddenOrBlocked = view.StackBehaviour is ViewStackBehaviour.HideBelow
							or ViewStackBehaviour.PauseAndHideBelow
							or ViewStackBehaviour.PauseOnlyBelow;

						if (wasHiddenOrBlocked && !IsViewCoveredByAnythingAbove(fragmentBelow, history))
						{
							await ResumeFragmentFromMidStackAsync(fragmentBelow, view.StackBehaviour, immediate, ct);
						}
					}

					return;
				}

				// CASE 3: No parent or not in history — just destroy
				await HideAndDestroyAsync(view, record, context, immediate, ct);
			}
			finally
			{
				_closingViews.Remove(view);
			}
		}

		// =================================================================
		// CORE — Go Back
		// =================================================================

		private async UniTask GoBackInternalAsync(UIView parentView, bool immediate = false, CancellationToken ct = default)
		{
			if (parentView == null || parentView.gameObject == null) return;
			if (!_historyStacks.TryGetValue(parentView, out var history) || history.Count == 0)
				return;

			// Peek before popping — validate the view is still registered.
			// If it's not in the registry, it was already cleaned up (e.g., via parent destruction),
			// so we must not pop it blindly (that would corrupt the stack and skip resume of the previous view).
			var currentView = history.Peek();

			if (!_viewRegistry.TryGetValue(currentView, out var record))
			{
				// The top view is no longer registered — remove it from history and bail out.
				history.Pop();
				return;
			}

			// Now safe to pop — the view is valid and we have its record.
			history.Pop();
			UIView previousView = history.Count > 0 ? history.Peek() : null;

			bool parallel = !immediate &&
			                previousView != null &&
			                previousView.PlayInParallelWithPrevious;

			try
			{
				if (parallel && previousView != null)
				{
					await UniTask.WhenAll(
						HideAndDestroyAsync(currentView, record, CloseContext.Normal, immediate, ct),
						ResumeFragmentAsync(currentView, previousView, immediate, ct)
					);
				}
				else
				{
					await HideAndDestroyAsync(currentView, record, CloseContext.Normal, immediate, ct);
					if (previousView != null)
					{
						await ResumeFragmentAsync(currentView, previousView, immediate, ct);
					}
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Debug.LogError($"Error during GoBack: {ex.Message}\n{ex.StackTrace}");
			}
		}

		// =================================================================
		// PAUSE / RESUME — Screens
		// =================================================================

		private async UniTask HandlePauseAsync(UIView newView, UIView previousView, CancellationToken ct = default)
		{
			try
			{
				switch (newView.StackBehaviour)
				{
					case ViewStackBehaviour.PauseAndHideBelow:
						previousView.OnPause();
						PauseFragments(previousView);
						await previousView.InternalPauseHideAsync(false, ct);
						previousView.SetInteractable(false);
						break;

					case ViewStackBehaviour.PauseOnlyBelow:
						previousView.OnPause();
						PauseFragments(previousView);
						previousView.SetInteractable(false);
						break;

					case ViewStackBehaviour.HideBelow:
						previousView.OnPause();
						PauseFragments(previousView);
						await previousView.InternalPauseHideAsync(false, ct);
						previousView.SetInteractable(false);
						break;

					case ViewStackBehaviour.CloseBelow:
						await CloseInternalAsync(previousView, CloseContext.Normal, true, ct);
						break;

					case ViewStackBehaviour.DoNothing:
					default:
						break;
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Debug.LogError($"Error during pause: {ex.Message}\n{ex.StackTrace}");
			}
		}

		private async UniTask HandleResumeAsync(UIView closedView, UIView previousView, bool immediate, CancellationToken ct = default)
		{
			try
			{
				switch (closedView.StackBehaviour)
				{
					case ViewStackBehaviour.PauseAndHideBelow:
						await previousView.InternalResumeShowAsync(ct, immediate);
						previousView.SetInteractable(true);
						ResumeFragments(previousView);
						previousView.OnResume();
						break;

					case ViewStackBehaviour.PauseOnlyBelow:
						previousView.SetInteractable(true);
						ResumeFragments(previousView);
						previousView.OnResume();
						break;

					case ViewStackBehaviour.HideBelow:
						// HideBelow hid the view and called OnPause, so we need to show it back
						// and resume children that were implicitly hidden along with the parent.
						await previousView.InternalResumeShowAsync(ct, immediate);
						previousView.SetInteractable(true);
						ResumeFragments(previousView);
						previousView.OnResume();
						break;

					case ViewStackBehaviour.DoNothing:
					default:
						break;
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Debug.LogError($"Error during resume: {ex.Message}\n{ex.StackTrace}");
			}
		}

		// =================================================================
		// PAUSE / RESUME — Fragments
		// =================================================================

		private async UniTask HandleFragmentStackBehaviourAsync(UIView newView, UIView previousFragment, CancellationToken ct = default)
		{
			try
			{
				switch (newView.StackBehaviour)
				{
					case ViewStackBehaviour.HideBelow:
					case ViewStackBehaviour.PauseAndHideBelow:
						previousFragment.OnPause();
						previousFragment.SetInteractable(false);
						bool parallel = previousFragment.PlayInParallelWithPrevious;
						if (parallel)
						{
							await UniTask.WhenAll(
								previousFragment.InternalPauseHideAsync(false, ct),
								newView.InternalShowAsync(ct)
							);
						}
						else
						{
							await previousFragment.InternalPauseHideAsync(false, ct);
							await newView.InternalShowAsync(ct);
						}

						break;

					case ViewStackBehaviour.PauseOnlyBelow:
						previousFragment.OnPause();
						previousFragment.SetInteractable(false);
						await newView.InternalShowAsync(ct);
						break;

					case ViewStackBehaviour.CloseBelow:
						if (previousFragment.PlayInParallelWithPrevious)
						{
							CloseInternalAsync(previousFragment, CloseContext.Normal, true, ct).Forget();
							await newView.InternalShowAsync(ct);
						}
						else
						{
							await CloseInternalAsync(previousFragment, CloseContext.Normal, true, ct);
							await newView.InternalShowAsync(ct);
						}

						break;

					case ViewStackBehaviour.DoNothing:
					default:
						await newView.InternalShowAsync(ct);
						break;
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Debug.LogError($"Error during fragment stack behaviour: {ex.Message}\n{ex.StackTrace}");
				if (!newView.IsVisible)
				{
					try
					{
						await newView.InternalShowAsync(ct);
					}
					catch (Exception showEx)
					{
						Debug.LogError($"Recovery show also failed for '{newView.name}': {showEx.Message}");
					}
				}
			}
		}

		private async UniTask ResumeFragmentAsync(UIView closedView, UIView previousFragment, bool immediate = false, CancellationToken ct = default)
		{
			try
			{
				previousFragment.SetContext(null);

				switch (closedView.StackBehaviour)
				{
					case ViewStackBehaviour.PauseAndHideBelow:
						await previousFragment.InternalResumeShowAsync(ct, immediate);
						previousFragment.SetInteractable(true);
						previousFragment.OnResume();
						break;

					case ViewStackBehaviour.HideBelow:
						// HideBelow hid the fragment and called OnPause, so show it back
						// and restore interactable + call OnResume for symmetry with pause.
						await previousFragment.InternalResumeShowAsync(ct, immediate);
						previousFragment.SetInteractable(true);
						previousFragment.OnResume();
						break;

					case ViewStackBehaviour.PauseOnlyBelow:
						previousFragment.SetInteractable(true);
						previousFragment.OnResume();
						break;

					case ViewStackBehaviour.DoNothing:
					default:
						break;
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Debug.LogError($"Error resuming fragment: {ex.Message}\n{ex.StackTrace}");
			}
		}

		// =================================================================
		// DESTROY / CLEANUP
		// =================================================================

		private async UniTask HideAndDestroyAsync(UIView view, ViewRecord record, CloseContext context,
		                                          bool immediate, CancellationToken ct = default)
		{
			try
			{
				await view.InternalHideAsync(immediate || context != CloseContext.Normal, ct);
			}
			catch (OperationCanceledException)
			{
				// External cancellation must NOT abort settlement: the view was already popped
				// from its stack, so skipping the registry/pool cleanup below would leave a
				// zombie (registered, active, off-stack). Settle it instead.
			}
			catch (Exception ex)
			{
				Debug.LogError($"Error hiding view '{view.name}': {ex.Message}");
			}

			bool shouldDestroy = ShouldDestroyView(record, context);

			if (!shouldDestroy)
			{
				// Static view with Normal close — hide it but keep it registered.
				// It stays in the hierarchy and in the history stack for proper navigation.
				// Close its children: dynamic children are destroyed, static children are hidden.
				CloseChildrenOfSurvivingView(view);

				// Reset runtime flags so it can be shown again
				view._overriddenStackBehaviour = null;
				view._overriddenChannel = null;
				view.gameObject.SetActive(false);

				// Keep in history stack - static views behave exactly like dynamic views
				// in terms of navigation. The only difference is they're not destroyed.
				// When re-shown, they'll be at the top of the stack and GoBack will work correctly.
				return;
			}

			// Dynamic view or ForceDestroy/ParentDestroyed context — destroy everything
			// Close all children first — they must be destroyed since parent is going away
			CloseChildrenImmediate(view, context);

			// Remove from parent's child list
			if (record.Parent != null && _viewRegistry.TryGetValue(record.Parent, out var parentRecord))
			{
				parentRecord.RemoveChild(view);
			}

			_viewRegistry.Remove(view);

			if (record.Instance.ReturnToPoolOnClose && record.IsDynamic)
			{
				_viewPool.Release(view);
			}
			else
			{
				view.InternalCleanup();
				Destroy(view.gameObject);
			}

			view._overriddenStackBehaviour = null;
			view._overriddenChannel = null;
		}

		private async UniTask DestroyViewAsync(UIView view, ViewRecord record, CloseContext context,
		                                       bool immediate, CancellationToken ct = default)
		{
			try
			{
				await view.InternalHideAsync(immediate, ct);
			}
			catch (OperationCanceledException)
			{
				// See HideAndDestroyAsync - cancellation must not skip settlement.
			}
			catch (Exception ex)
			{
				Debug.LogError($"Error hiding view '{view.name}': {ex.Message}");
			}

			CloseChildrenImmediate(view, context);

			if (record.Parent != null && _viewRegistry.TryGetValue(record.Parent, out var parentRecord))
			{
				parentRecord.RemoveChild(view);
			}

			_viewRegistry.Remove(view);
			view.InternalCleanup();
			Destroy(view.gameObject);
		}

		private void CloseChildrenImmediate(UIView parent, CloseContext context)
		{
			if (!_viewRegistry.TryGetValue(parent, out var record)) return;

			var childContext = context == CloseContext.Normal ? CloseContext.ParentDestroyed : context;

			for (int i = record.Children.Count - 1; i >= 0; i--)
			{
				var child = record.Children[i];
				if (child == null) continue;

				if (_viewRegistry.TryGetValue(child, out var childRecord))
				{
					// Close children recursively, immediate (no animation) to prevent
					// accessing destroyed GameObjects during Unity's automatic cleanup
					CloseChildrenImmediate(child, childContext);

					bool shouldDestroy = ShouldDestroyView(childRecord, childContext);

					// InternalCleanup runs full lifecycle: OnPrepareHide → OnHide → UnRegisterResources → NullifyContext
					// It is idempotent, safe if hide already ran.
					child.InternalCleanup();
					_viewRegistry.Remove(child);

					// Remove from any history stack
					if (child.ParentView != null && _historyStacks.TryGetValue(child.ParentView, out var history))
					{
						RemoveFromStack(child, history);
					}

					if (shouldDestroy)
					{
						if (childRecord.IsDynamic && child.ReturnToPoolOnClose)
						{
							_viewPool.Release(child);
						}
						// else: Unity will destroy children when parent is destroyed
					}
				}
			}

			record.Children.Clear();

			// Clean up history stack and pending show tasks for this parent
			_historyStacks.Remove(parent);
			_pendingShowTasks.Remove(parent);
		}

		/// <summary>
		/// Closes children of a static view that is being hidden (not destroyed).
		/// Dynamic children are destroyed. Static children are hidden and reset (they survive in hierarchy).
		/// </summary>
		private void CloseChildrenOfSurvivingView(UIView parent)
		{
			if (!_viewRegistry.TryGetValue(parent, out var record)) return;

			for (int i = record.Children.Count - 1; i >= 0; i--)
			{
				var child = record.Children[i];
				if (child == null) continue;

				if (!_viewRegistry.TryGetValue(child, out var childRecord)) continue;

				if (childRecord.IsDynamic)
				{
					// Dynamic children are destroyed — recurse with ParentDestroyed
					CloseChildrenImmediate(child, CloseContext.ParentDestroyed);
					child.InternalCleanup();
					_viewRegistry.Remove(child);

					if (child.ParentView != null && _historyStacks.TryGetValue(child.ParentView, out var history))
					{
						RemoveFromStack(child, history);
					}

					record.RemoveChild(child);

					// Null check: child could be invalid after InternalCleanup in edge cases
					if (child != null && child.gameObject != null)
					{
						if (child.ReturnToPoolOnClose)
						{
							_viewPool.Release(child);
						}
						else
						{
							Destroy(child.gameObject);
						}
					}
				}
				else
				{
					// Static children survive — just hide them and recurse for their children
					CloseChildrenOfSurvivingView(child);
					child.InternalCleanup();
					child.gameObject.SetActive(false);

					if (child.ParentView != null && _historyStacks.TryGetValue(child.ParentView, out var history))
					{
						RemoveFromStack(child, history);
					}
				}
			}

			_historyStacks.Remove(parent);
			_pendingShowTasks.Remove(parent);
		}

		private bool ShouldDestroyView(ViewRecord record, CloseContext context)
		{
			if (record.IsDynamic) return true;
			return context == CloseContext.ParentDestroyed || context == CloseContext.ForceDestroy;
		}

		// =================================================================
		// HELPERS
		// =================================================================

		/// <summary>
		/// Finds the view directly below the given view in a history stack.
		/// Returns null if the view is at the bottom or not found.
		/// Does NOT modify the stack.
		/// </summary>
		private static UIView FindBelowInStack(UIView target, Stack<UIView> stack)
		{
			// ToArray: index 0 = top of stack, last index = bottom
			var items = stack.ToArray();
			for (int i = 0; i < items.Length; i++)
			{
				if (items[i] == target)
				{
					// The view below is at index i+1 (deeper in the stack)
					return (i + 1 < items.Length) ? items[i + 1] : null;
				}
			}

			return null;
		}

		/// <summary>
		/// Checks whether a view is still being covered (hidden or blocked) by any other view
		/// above it in the remaining history stack.
		///
		/// This is needed when removing a mid-stack fragment: even though the removed fragment was
		/// hiding/blocking the one below, another fragment further up in the stack might also be
		/// covering it, in which case we should NOT resume.
		///
		/// We walk from the target's position upward to the top. If any view above it has
		/// HideBelow, PauseAndHideBelow, or PauseOnlyBelow, then it is still covered.
		/// </summary>
		private static bool IsViewCoveredByAnythingAbove(UIView target, Stack<UIView> stack)
		{
			// ToArray: index 0 = top of stack, last index = bottom
			var items = stack.ToArray();

			int targetIndex = -1;
			for (int i = 0; i < items.Length; i++)
			{
				if (items[i] == target)
				{
					targetIndex = i;
					break;
				}
			}

			if (targetIndex < 0)
			{
				return false; // View not in stack, nothing covers it
			}

			// Check all views above the target (from just above it to the top of stack)
			for (int i = targetIndex - 1; i >= 0; i--)
			{
				var above = items[i];
				if (above.StackBehaviour is ViewStackBehaviour.HideBelow
					or ViewStackBehaviour.PauseAndHideBelow
					or ViewStackBehaviour.PauseOnlyBelow)
				{
					return true; // Something above is still covering this view
				}
			}

			return false;
		}

		/// <summary>
		/// Resumes a fragment that was previously hidden or blocked by a fragment that has now been
		/// removed from the middle of the stack.
		/// Applies the correct resume behaviour based on how the fragment was originally affected.
		/// </summary>
		private async UniTask ResumeFragmentFromMidStackAsync(UIView fragment, ViewStackBehaviour removedViewBehaviour,
		                                                      bool immediate = false, CancellationToken ct = default)
		{
			try
			{
				fragment.SetContext(null);

				switch (removedViewBehaviour)
				{
					case ViewStackBehaviour.HideBelow:
					case ViewStackBehaviour.PauseAndHideBelow:
						// Fragment was fully hidden — show it back and restore interactivity
						await fragment.InternalResumeShowAsync(ct, immediate);
						fragment.SetInteractable(true);
						fragment.OnResume();
						break;

					case ViewStackBehaviour.PauseOnlyBelow:
						// Fragment was only input-blocked — restore interactivity
						fragment.SetInteractable(true);
						fragment.OnResume();
						break;
				}
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Debug.LogError($"Error during mid-stack fragment resume for '{fragment.name}': {ex.Message}\n{ex.StackTrace}");
			}
		}

		/// <summary>
		/// Finds the best parent view for a fragment that has no explicit parent.
		/// Prefers the topmost screen across all channel stacks (by sorting order).
		/// </summary>
		private UIView FindBestParentView(UIChannel? preferredChannel = null)
		{
			UIView bestCandidate = null;
			// Initialize below any real channel so HUD (0) can be selected as fallback.
			UIChannel bestSortOrder = (UIChannel)(-1);

			foreach ((UIChannel effectiveOrder, Stack<UIView> value) in _channelStacks)
			{
				if (value.Count == 0) continue;

				var topView = value.Peek();

				// If a preferred channel is requested, only consider that channel.
				if (preferredChannel.HasValue && effectiveOrder != preferredChannel.Value)
					continue;

				// Prefer higher sort order (overlays > menus > HUD)
				if (effectiveOrder > bestSortOrder)
				{
					bestSortOrder = effectiveOrder;
					bestCandidate = topView;
				}
			}

			return bestCandidate;
		}

		private void PauseFragments(UIView parentView)
		{
			if (parentView == null || parentView.gameObject == null) return;
			if (!_historyStacks.TryGetValue(parentView, out var stack)) return;
			foreach (var fragment in stack)
			{
				if (fragment != null && fragment.gameObject != null)
					fragment.OnPause();
			}
		}

		private void ResumeFragments(UIView parentView)
		{
			if (parentView == null || parentView.gameObject == null) return;
			if (!_historyStacks.TryGetValue(parentView, out var stack)) return;
			foreach (var fragment in stack)
			{
				if (fragment != null && fragment.gameObject != null)
					fragment.OnResume();
			}
		}

		/// <summary>
		/// O(1) prefab lookup by (Type, ViewId). Caches the lookup dictionary on first access.
		/// </summary>
		private TView FindPrefab<TView>(Type type, string viewId) where TView : UIView
		{
			if (_prefabLookup == null)
			{
				_prefabLookup = new Dictionary<(Type, string), UIView>();
				foreach (var view in _repository.Views)
				{
					if (view != null)
						_prefabLookup[(view.GetType(), view.ViewId ?? string.Empty)] = view;
				}
			}

			return _prefabLookup.TryGetValue((type, viewId), out var prefab) ? prefab as TView : null;
		}

		/// <summary>
		/// Removes ghost entries from _viewRegistry, _historyStacks, and _pendingShowTasks
		/// where the UIView key has been destroyed by Unity (fake-null).
		/// Should be called periodically or on scene transitions.
		/// </summary>
		internal void CleanupDestroyedViews()
		{
			// Purge dead keys from _viewRegistry
			var deadKeys = new List<UIView>();
			foreach (var key in _viewRegistry.Keys)
			{
				if (key == null) deadKeys.Add(key);
			}

			foreach (var key in deadKeys)
			{
				_viewRegistry.Remove(key);
			}

			// Purge dead keys from _historyStacks
			var deadHistoryKeys = new List<UIView>();
			foreach (var key in _historyStacks.Keys)
			{
				if (key == null) deadHistoryKeys.Add(key);
			}

			foreach (var key in deadHistoryKeys)
			{
				_historyStacks.Remove(key);
			}

			// Purge dead keys from _pendingShowTasks
			var deadPendingKeys = new List<UIView>();
			foreach (var key in _pendingShowTasks.Keys)
			{
				if (key == null) deadPendingKeys.Add(key);
			}

			foreach (var key in deadPendingKeys)
			{
				_pendingShowTasks.Remove(key);
			}
		}

		/// <summary>
		/// Recomputes canvas sorting orders for every screen in a channel stack after a
		/// pop/mid-stack removal, keeping depths contiguous so a later push can't collide
		/// with a survivor's baked-in order. Stack bottom is depth 1 (matches push-time
		/// channel.Initialize(_uiCamera, stack.Count)).
		/// </summary>
		private static void RecomputeChannelSorting(Stack<UIView> stack)
		{
			if (stack == null || stack.Count == 0) return;

			var arr = stack.ToArray(); // top-first
			for (int i = arr.Length - 1, depth = 1; i >= 0; i--, depth++)
			{
				var v = arr[i];
				if (v != null && v.HasChannel)
				{
					v.Channel.UpdateSortingOrder(depth);
				}
			}
		}

		private static void RemoveFromStack<T>(T item, Stack<T> stack) where T : class
		{
			var temp = new Stack<T>();
			bool found = false;

			while (stack.Count > 0)
			{
				var current = stack.Pop();
				if (current == item && !found)
				{
					found = true;
				}
				else
				{
					temp.Push(current);
				}
			}

			while (temp.Count > 0)
			{
				stack.Push(temp.Pop());
			}
		}

#if UNITY_EDITOR
		[Button("Show View By Index")]
		private void ShowViewByIndex(int index)
		{
			if (_repository == null || index < 0 || index >= _repository.Views.Count) return;
			var prefab = _repository.Views[index];
			Show<UIView>(prefab.GetType());
		}
#endif
	}
}