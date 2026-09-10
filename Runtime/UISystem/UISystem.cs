using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core.Collections;
using AK.Systems.UI;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
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

		[SerializeField]
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
		private readonly Dictionary<UIChannel, ViewStack> _channelStacks = new();

		/// <summary>
		/// Per-parent history stacks for fragments (views without UIChannel).
		/// </summary>
		private readonly Dictionary<UIView, ViewStack> _historyStacks = new();

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
		/// Registered views grouped by kind. Each bucket is an intrusive singly linked list of
		/// records threaded through <see cref="ViewRecord.NextOfKind"/>, so "find the static
		/// instance of X under parent P" or "is a dynamic X already open on P" walks only the
		/// instances of that one kind instead of the whole registry.
		/// </summary>
		private readonly Dictionary<ViewKey, ViewRecord> _recordsByKind = new();

		/// <summary>
		/// Lookup cache for fast prefab resolution by (Type, ViewId).
		/// Built lazily on first access and invalidated when repository changes.
		/// </summary>
		private Dictionary<ViewKey, PrefabEntry> _prefabLookup;

		/// <summary>
		/// Guards against double-close on screens (like V1's _closingScreens).
		/// </summary>
		private readonly HashSet<UIView> _closingViews = new();

		private ViewPool _viewPool;

		// =================================================================
		// VIEW RECORD
		// =================================================================

		private sealed class ViewRecord
		{
			public UIView       Instance  { get; }
			public UIView       Parent    { get; }
			public bool         IsStatic  { get; }
			public bool         IsDynamic => !IsStatic;
			public List<UIView> Children  { get; } = new();

			/// <summary>Kind under which this record is indexed. Captured at registration —
			/// pooled instances reset their ViewId on release, so the live value can drift.</summary>
			public ViewKey Kind;

			/// <summary>Next record of the same kind in the registry index.</summary>
			public ViewRecord NextOfKind;

			public ViewRecord(UIView instance, UIView parent, bool isStatic)
			{
				Instance = instance;
				Parent = parent;
				IsStatic = isStatic;
				Kind = ViewKey.Of(instance);
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

		private readonly struct PrefabEntry
		{
			public readonly UIView Prefab;
			public readonly bool   IsScreen;

			public PrefabEntry(UIView prefab, bool isScreen)
			{
				Prefab = prefab;
				IsScreen = isScreen;
			}
		}

		// =================================================================
		// LIFECYCLE
		// =================================================================

		private void Awake()
		{
			EnsureInitialized();
		}

		/// <summary>
		/// Awake's body, callable from edit-mode tests where Unity does not run Awake.
		/// Idempotent.
		/// </summary>
		internal void EnsureInitialized()
		{
			if (_viewPool != null) return;

			_viewPool = new ViewPool(_viewsContainer);

			// Ensure the default channel stack always exists
			_channelStacks[UIChannel.HUD] = new ViewStack();

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
			_viewPool?.Clear();
		}

		private static void DestroyViewObject(GameObject go)
		{
			if (Application.isPlaying) Destroy(go);
			else DestroyImmediate(go);
		}

		// =================================================================
		// IUIViewSystem — SHOW (Fire-and-Forget)
		// =================================================================

		public TView Show<TView>(UIContext context = null, UIView parent = null, string viewId = "",
		                         UIChannel? channelOverride = null, ViewStackBehaviour? stackBehaviour = null, Action<TView> onInit = null,
		                         bool waitForPrevious = false)
			where TView : UIView
		{
			return Show(typeof(TView), context, parent, viewId, channelOverride, stackBehaviour, onInit, waitForPrevious);
		}

		public TView Show<TView>(Type type, UIContext context = null, UIView parent = null, string viewId = "",
		                         UIChannel? channelOverride = null, ViewStackBehaviour? stackBehaviour = null, Action<TView> onInit = null,
		                         bool waitForPrevious = false)
			where TView : UIView
		{
			var (view, animationTask) = PrepareAndRegisterView(type, context, parent, viewId, channelOverride, stackBehaviour, onInit, waitForPrevious: waitForPrevious);
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
		                                             bool waitForPrevious = false,
		                                             CancellationToken ct = default)
			where TView : UIView
		{
			return await ShowAsync(typeof(TView), context, parent, viewId, channelOverride, stackBehaviour, onInit, waitForPrevious, ct);
		}

		public async UniTask<TView> ShowAsync<TView>(Type type, UIContext context = null, UIView parent = null, string viewId = "",
		                                             UIChannel? channelOverride = null, ViewStackBehaviour? stackBehaviour = null,
		                                             Action<TView> onInit = null,
		                                             bool waitForPrevious = false,
		                                             CancellationToken ct = default)
			where TView : UIView
		{
			var (view, animationTask) = PrepareAndRegisterView(type, context, parent, viewId, channelOverride, stackBehaviour, onInit, waitForPrevious: waitForPrevious);
			if (view == null) return null;

			// Await the full animation pipeline — this is the key UniTask advantage.
			// Exceptions propagate with full async stack traces.
			await animationTask.AttachExternalCancellation(ct);
			return view;
		}

		// =================================================================
		// IUIViewSystem — CLOSE
		// =================================================================

		public void Close(UIView view, Action onClose = null)
		{
			CloseThenNotifyAsync(view, immediate: false, onClose).Forget();
		}

		public UniTask CloseAsync(UIView view, CancellationToken ct = default)
		{
			return CloseInternalAsync(view, CloseContext.Normal, false, ct);
		}

		public void CloseImmediate(UIView view, Action onClose = null)
		{
			CloseThenNotifyAsync(view, immediate: true, onClose).Forget();
		}

		private async UniTask CloseThenNotifyAsync(UIView view, bool immediate, Action onClose)
		{
			// Only fire the callback when the close will actually do something - a double-close
			// or unregistered view early-returns inside CloseInternalAsync and "closed" nothing.
			bool willClose = view != null && _viewRegistry.ContainsKey(view) && !_closingViews.Contains(view);
			await CloseInternalAsync(view, CloseContext.Normal, immediate);
			if (willClose) onClose?.Invoke();
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
				toast.Init(0, text);
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
			return GoBackInternalAsync(parentView, ct: ct);
		}

		// =================================================================
		// IUIViewSystem — QUERY
		// =================================================================

		public TView GetView<TView>(string viewId = "") where TView : UIView
		{
			var key = new ViewKey(typeof(TView), viewId);

			// Screens on a channel stack first (top-most within a channel), then any registered
			// instance that is not tearing down.
			foreach (var stack in _channelStacks.Values)
			{
				foreach (var view in stack)
				{
					if (view != null && view.GetType() == key.Type && view.ViewId == key.ViewId)
						return view as TView;
				}
			}

			for (var record = FirstOfKind(key); record != null; record = record.NextOfKind)
			{
				UIView instance = record.Instance;
				if (instance != null && !_closingViews.Contains(instance))
					return instance as TView;
			}

			return null;
		}

		// =================================================================
		// STATIC FRAGMENT REGISTRATION (Internal — not on IUIViewSystem)
		// =================================================================

		internal void RegisterStaticView(UIView view, UIView parent)
		{
			if (_viewRegistry.ContainsKey(view))
			{
				Debug.LogWarning($"Static view '{view.name}' is already registered.", view);
				return;
			}

			var record = new ViewRecord(view, parent, isStatic: true);

			if (parent != null && _viewRegistry.TryGetValue(parent, out var parentRecord))
			{
				parentRecord.AddChild(view);
			}

			Register(record);
		}

		internal void ShowExistingView(UIView view, UIContext context = null,
		                               ViewStackBehaviour? stackBehaviour = null)
		{
			if (!TryResolveExisting(view, out var record)) return;

			ShowRegisteredViewAsync(view, record.Parent, context, stackBehaviour).Forget();
		}

		internal UniTask ShowExistingViewAsync(UIView view, UIContext context = null,
		                                       ViewStackBehaviour? stackBehaviour = null, CancellationToken ct = default)
		{
			if (!TryResolveExisting(view, out var record)) return UniTask.CompletedTask;

			return ShowRegisteredViewAsync(view, record.Parent, context, stackBehaviour, ct: ct);
		}

		/// <summary>
		/// Shows a registered fragment without waiting for pending sibling shows, like
		/// ShowStaticChildrenBatch does for ShowOnStart children. For independent sibling
		/// pops with no stack behaviour between them — history is pushed synchronously,
		/// then the animation runs. Returns the show task for awaiters.
		/// </summary>
		internal UniTask ShowExistingViewParallel(UIView view, UIContext context = null, CancellationToken ct = default)
		{
			if (!TryResolveExisting(view, out var record)) return UniTask.CompletedTask;

			UIView parent = record.Parent;
			if (parent == null)
			{
				return ShowRegisteredViewAsync(view, null, context, null, ct: ct);
			}

			view.PrepareForShowAnimation();
			view.SetContext(context);
			GetOrCreateHistory(parent).MoveToTop(view);

			return view.InternalShowAsync(ct);
		}

		private bool TryResolveExisting(UIView view, out ViewRecord record)
		{
			record = null;

			if (view == null || view.gameObject == null)
			{
				Debug.LogError("Cannot show view: view or its GameObject is null.");
				return false;
			}

			if (view.IsTemplate)
			{
				Debug.LogWarning($"'{view.name}' is a template — call ShowFragment<{view.GetType().Name}>() to spawn a clone; the template itself never shows.", view);
				return false;
			}

			if (!_viewRegistry.TryGetValue(view, out record))
			{
				Debug.LogError($"Cannot show view '{view.name}': not registered.", view);
				return false;
			}

			return true;
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

			ViewStack history = GetOrCreateHistory(parent);

			for (int i = 0; i < entries.Count; i++)
			{
				StaticViewEntry entry = entries[i];
				if (entry.View == null || !entry.ShowOnStart) continue;
				if (entry.View.IsTemplate) continue;   // templates are clone sources, never presented
				if (!_viewRegistry.ContainsKey(entry.View)) continue;

				if (entry.ShowOnStartDelay > 0f)
				{
					// Hide it for the wait so an active fragment doesn't flash before its entrance.
					entry.View.MoveContentOffScreen();
					ShowStaticChildDelayedAsync(parent, entry.View, entry.ShowOnStartDelay).Forget();
					continue;
				}

				// Prepare and push onto history stack synchronously (no stack behaviour between siblings)
				entry.View.PrepareForShowAnimation();
				history.MoveToTop(entry.View);

				// Each child animates independently; nothing awaits the batch as a whole.
				entry.View.InternalShowAsync().Forget();
			}
		}

		/// <summary>
		/// Waits out a ShowOnStart delay, then shows the fragment through the parallel path.
		/// The token armed by BeginDelayedStart is cancelled by every close/hide/destroy
		/// path on the view, and the post-delay guards catch a parent that went away or a
		/// fragment someone else showed during the wait — so a delayed start never pops
		/// a fragment back open after it was closed.
		/// </summary>
		private async UniTask ShowStaticChildDelayedAsync(UIView parent, UIView view, float delay)
		{
			CancellationToken ct = view.BeginDelayedStart();

			try
			{
				await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
			}
			catch (OperationCanceledException)
			{
				return; // closed, hidden, or destroyed before the delay elapsed
			}

			// Re-validate after the wait — state may have changed mid-delay.
			if (view == null || view.gameObject == null) return;
			if (parent == null || parent.gameObject == null) return;
			if (_closingViews.Contains(view) || _closingViews.Contains(parent)) return;
			if (view.IsVisible) return;   // shown by someone else during the delay

			await ShowExistingViewParallel(view, ct: ct);
		}

		internal bool IsViewRegistered(UIView view)
		{
			return view != null && view.gameObject != null && _viewRegistry.ContainsKey(view);
		}

		// =================================================================
		// REGISTRY INDEX
		// =================================================================

		private ViewRecord FirstOfKind(ViewKey key)
		{
			return _recordsByKind.TryGetValue(key, out var head) ? head : null;
		}

		private void Register(ViewRecord record)
		{
			_viewRegistry.Add(record.Instance, record);

			if (_recordsByKind.TryGetValue(record.Kind, out var head))
			{
				record.NextOfKind = head;
			}

			_recordsByKind[record.Kind] = record;
		}

		private void Unregister(UIView view)
		{
			if (!_viewRegistry.TryGetValue(view, out var record)) return;

			_viewRegistry.Remove(view);

			if (!_recordsByKind.TryGetValue(record.Kind, out var head)) return;

			if (ReferenceEquals(head, record))
			{
				if (record.NextOfKind == null) _recordsByKind.Remove(record.Kind);
				else _recordsByKind[record.Kind] = record.NextOfKind;
			}
			else
			{
				for (var current = head; current.NextOfKind != null; current = current.NextOfKind)
				{
					if (ReferenceEquals(current.NextOfKind, record))
					{
						current.NextOfKind = record.NextOfKind;
						break;
					}
				}
			}

			record.NextOfKind = null;
		}

		/// <summary>
		/// Static instance of a kind that is not tearing down. With a parent, only one
		/// registered under that parent; without, any static of the kind (it re-routes onto
		/// its own host).
		/// </summary>
		private ViewRecord FindStaticRecord(ViewKey key, UIView parent)
		{
			for (var record = FirstOfKind(key); record != null; record = record.NextOfKind)
			{
				if (!record.IsStatic) continue;
				if (parent != null && record.Parent != parent) continue;

				UIView instance = record.Instance;
				if (instance != null && !_closingViews.Contains(instance))
				{
					return record;
				}
			}

			return null;
		}

		/// <summary>Dynamic instance of a kind already open under <paramref name="parent"/>, not tearing down.</summary>
		private ViewRecord FindDynamicRecord(ViewKey key, UIView parent)
		{
			for (var record = FirstOfKind(key); record != null; record = record.NextOfKind)
			{
				if (record.IsStatic || record.Parent != parent) continue;

				UIView instance = record.Instance;
				if (instance != null && !_closingViews.Contains(instance))
				{
					return record;
				}
			}

			return null;
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
			bool immediate = false, bool waitForPrevious = false)
			where TView : UIView
		{
			var key = new ViewKey(type, viewId);

			// Static registrations take precedence over the repository:
			//   • Explicit parent → reuse only a static already registered under THAT parent.
			//   • No parent       → reuse any static for the kind and re-route it onto its
			//                       own registered host parent (its effective parent).
			// Views mid-close are excluded so we never reuse a view that is tearing down.
			ViewRecord staticRecord = FindStaticRecord(key, parent);

			if (staticRecord != null && staticRecord.Instance is TView staticView)
			{
				// Templates never show or get reused themselves — they are clone sources.
				if (staticView.IsTemplate)
				{
					return PrepareCloneFromTemplate(staticView, parent ?? staticRecord.Parent, context,
					                                stackBehaviour, onInit, immediate, waitForPrevious);
				}

				UIView effectiveParent = parent ?? staticRecord.Parent;
				onInit?.Invoke(staticView);
				return (staticView, waitForPrevious
					? ShowRegisteredViewAsync(staticView, effectiveParent, context, stackBehaviour, immediate)
					: ShowExistingViewParallel(staticView, context));
			}

			// --- Find prefab (repository fallback) ---
			if (!TryFindPrefab(key, out PrefabEntry prefabEntry) || prefabEntry.Prefab is not TView prefab)
			{
				Debug.LogError($"View prefab of type {type.Name} with ID '{key.ViewId}' not found in UIViewRepository.");
				return (null, UniTask.CompletedTask);
			}

			bool isScreen = prefabEntry.IsScreen;

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
			// When replacing, the close is sequenced BEFORE the new show pipeline: otherwise
			// the old close's resume-of-previous can run after the new show paused that same
			// view, leaving it visible/interactable on top of the new view.
			UIView replaced = prefab.AllowMultipleInstances ? null : FindDynamicRecord(key, parent)?.Instance;

			// --- Instantiate or get from pool ---
			Transform spawnParent = isScreen
				? _viewsContainer
				: (parent != null ? parent.FragmentContainer : _viewsContainer);

			TView newView = _viewPool.Get(prefab, spawnParent);

			if (key.ViewId.Length != 0 && string.IsNullOrEmpty(newView.ViewId))
			{
				newView.SetViewId(key.ViewId);
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
			Register(record);

			if (parent != null && _viewRegistry.TryGetValue(parent, out var parentRecord))
			{
				parentRecord.AddChild(newView);
			}

			// --- Initialize static children ---
			newView.InitializeStaticChildren(_diContainer);

			// --- Build the animation pipeline (but don't start it yet) ---
			UniTask animTask;
			if (replaced != null)
			{
				animTask = isScreen
					? ReplaceThenShowScreenAsync(replaced, newView, channelOverride, immediate)
					: ReplaceThenShowFragmentAsync(replaced, newView, parent, immediate, waitForPrevious);
			}
			else
			{
				animTask = isScreen
					? RunScreenShowAsync(newView, channelOverride, immediate)
					: RunFragmentShowAsync(newView, parent, immediate, waitForPrevious: waitForPrevious);
			}

			return (newView, animTask);
		}

		/// <summary>
		/// Clones a template static fragment and prepares the clone exactly like a
		/// prefab-spawned dynamic instance: injected, registered under the same parent,
		/// shown through the standard fragment pipeline. Clones register as dynamic, so
		/// every ShowFragment call re-finds the template — unlimited clones, and the
		/// template itself never presents.
		/// </summary>
		private (TView view, UniTask animationTask) PrepareCloneFromTemplate<TView>(
			TView template, UIView parent, UIContext context, ViewStackBehaviour? stackBehaviour,
			Action<TView> onInit, bool immediate, bool waitForPrevious)
			where TView : UIView
		{
			if (parent == null)
			{
				Debug.LogError($"Cannot clone template '{template.name}': no parent resolved.");
				return (null, UniTask.CompletedTask);
			}

			if (template.ReturnToPoolOnClose)
			{
				Debug.LogWarning($"Template '{template.name}' has ReturnToPoolOnClose — pooling is prefab-based, so clones destroy on close instead.", template);
			}

			// The template sits in its own layout slot — clones spawn right there.
			// Starts inactive like the template; the show pipeline activates it.
			var cloneGo = Instantiate(template.gameObject, template.transform.parent);
			cloneGo.name = template.name;
			var clone = cloneGo.GetComponent<TView>();

			clone._suppressPooling = true;
			clone.Inject(_diContainer);
			clone.InternalInitialize(this, parent);
			clone._overriddenStackBehaviour = stackBehaviour;
			clone.MoveContentOffScreen();

			onInit?.Invoke(clone);
			clone.SetContext(context);

			var record = new ViewRecord(clone, parent, isStatic: false);
			Register(record);

			if (_viewRegistry.TryGetValue(parent, out var parentRecord))
			{
				parentRecord.AddChild(clone);
			}

			clone.InitializeStaticChildren(_diContainer);

			return (clone, RunFragmentShowAsync(clone, parent, immediate, waitForPrevious: waitForPrevious));
		}

		/// <summary>
		/// Closes the instance being replaced before the new screen's show pipeline starts
		/// (see PrepareAndRegisterView). Close failures are logged but never block the show.
		/// </summary>
		private async UniTask ReplaceThenShowScreenAsync(UIView replaced, UIView newView, UIChannel? channelOverride, bool immediate)
		{
			await CloseReplacedAsync(replaced);
			await RunScreenShowAsync(newView, channelOverride, immediate);
		}

		private async UniTask ReplaceThenShowFragmentAsync(UIView replaced, UIView newView, UIView parent, bool immediate, bool waitForPrevious)
		{
			await CloseReplacedAsync(replaced);
			await RunFragmentShowAsync(newView, parent, immediate, waitForPrevious: waitForPrevious);
		}

		private async UniTask CloseReplacedAsync(UIView replaced)
		{
			try
			{
				await CloseInternalAsync(replaced, CloseContext.Normal, false);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
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
			UIChannel sortOrder = overrideChannel ?? channel.SortOrder;

			ViewStack stack = GetOrCreateChannelStack(sortOrder);
			stack.Push(newView);

			channel.Initialize(_uiCamera, stack.Count);

			UIView previousView = stack.PeekBelowTopOrNull();

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
		private async UniTask RunFragmentShowAsync(UIView newView, UIView parent, bool immediate = false, CancellationToken ct = default,
		                                           bool waitForPrevious = true)
		{
			if (!waitForPrevious)
			{
				// Independent sibling: push history synchronously and animate immediately,
				// skipping the per-parent gate and stack-behaviour negotiation.
				newView.PrepareForShowAnimation();
				GetOrCreateHistory(parent).MoveToTop(newView);

				await newView.InternalShowAsync(ct, immediate);
				return;
			}

			// Wait for any pending show on this parent to complete first.
			// This serializes rapid Show calls (e.g., double-tap) so they don't fight.
			await WaitForPendingShow(parent);

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
				ReleasePendingShow(parent, completionSource);
			}
		}

		private async UniTask RunFragmentShowInternalAsync(UIView newView, UIView parent, bool immediate, CancellationToken ct)
		{
			ViewStack history = GetOrCreateHistory(parent);

			// Capture previous fragment before pushing
			UIView previousFragment = history.PeekOrNull();
			if (previousFragment == newView) previousFragment = null;

			// Remove if already in history (bring to top)
			history.MoveToTop(newView);

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
			if (parent == null)
			{
				await ShowRegisteredViewInternalAsync(view, null, context, stackBehaviour, immediate, ct);
				return;
			}

			await WaitForPendingShow(parent);

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
				ReleasePendingShow(parent, completionSource);
			}
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

			ViewStack history = GetOrCreateHistory(parent);

			UIView previousView = history.PeekOrNull();
			if (previousView == view) previousView = null;

			history.MoveToTop(view);

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

		private async UniTask WaitForPendingShow(UIView parent)
		{
			if (!_pendingShowTasks.TryGetValue(parent, out var pending)) return;

			try
			{
				await pending.Task;
			}
			catch
			{
				/* swallow — we only care about sequencing */
			}
		}

		/// <summary>Drops the gate only if it is still ours — a later show may have replaced it.</summary>
		private void ReleasePendingShow(UIView parent, UniTaskCompletionSource ours)
		{
			if (_pendingShowTasks.TryGetValue(parent, out var existing) && ReferenceEquals(existing, ours))
				_pendingShowTasks.Remove(parent);
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
			if (!_channelStacks.TryGetValue(EffectiveChannel(view), out var stack))
			{
				await DestroyViewAsync(view, record, context, immediate, ct);
				return;
			}

			// CASE 1: Not at the top — remove from stack, destroy immediately
			if (stack.Count == 0 || stack.Peek() != view)
			{
				if (stack.Remove(view))
				{
					RecomputeChannelSorting(stack);
				}

				await DestroyViewAsync(view, record, context, true, ct);
				return;
			}

			// CASE 2: At the top — play close animation, then resume previous
			stack.Pop();
			RecomputeChannelSorting(stack);
			_closingViews.Add(view);

			UIView previousView = stack.PeekOrNull();

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
					UIView fragmentBelow = history.BelowOrNull(view);

					history.Remove(view);

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

						if (wasHiddenOrBlocked && !history.IsCoveredAbove(fragmentBelow))
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
			UIView previousView = history.PeekOrNull();

			bool parallel = !immediate &&
			                previousView != null &&
			                previousView.PlayInParallelWithPrevious;

			try
			{
				if (parallel)
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
					case ViewStackBehaviour.HideBelow:
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
					case ViewStackBehaviour.HideBelow:
						// Both hid the view and called OnPause, so show it back and resume
						// children that were implicitly hidden along with the parent.
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
					case ViewStackBehaviour.HideBelow:
						// Both hid the fragment and called OnPause, so show it back
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

		/// <summary>
		/// Drops all bookkeeping for a view destroyed outside the close pipeline (scene
		/// unload, direct Destroy). The view settles its own resources in OnDestroy;
		/// children do the same via their own OnDestroy — no cascade needed here.
		/// Idempotent: system-driven closes have already removed everything.
		/// </summary>
		internal void HandleViewDestroyedExternally(UIView view)
		{
			if (_viewRegistry.TryGetValue(view, out var record))
			{
				if (record.Parent != null && _viewRegistry.TryGetValue(record.Parent, out var parentRecord))
				{
					parentRecord.RemoveChild(view);
				}

				Unregister(view);
			}

			_closingViews.Remove(view);

			foreach (var stack in _channelStacks.Values)
			{
				stack.Remove(view);
			}

			foreach (var history in _historyStacks.Values)
			{
				history.Remove(view);
			}
		}

		private async UniTask HideAndDestroyAsync(UIView view, ViewRecord record, CloseContext context,
		                                          bool immediate, CancellationToken ct = default)
		{
			// Children-first close policy: only on graceful, animated closes with children
			// present. Teardown cascades (ParentDestroyed) always settle immediately.
			bool childrenFirst = !immediate && context == CloseContext.Normal &&
			                     record.Children.Count > 0 &&
			                     view.ChildCloseOrder != ChildCloseOrder.ParentFirst;

			if (childrenFirst)
			{
				try
				{
					await CloseChildrenAsync(view, view.ChildCloseOrder, immediate, ct);
				}
				catch (OperationCanceledException)
				{
					// Cancelled mid-cascade — each child self-settles in its own hide;
					// fall through to the parent's hide/settle.
				}
				catch (Exception ex)
				{
					Debug.LogError($"Error closing children of '{view.name}': {ex.Message}");
				}
			}

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

			Unregister(view);

			if (record.Instance.ReturnToPoolOnClose && record.IsDynamic)
			{
				_viewPool.Release(view);
			}
			else
			{
				view.InternalCleanup();
				DestroyViewObject(view.gameObject);
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

			Unregister(view);
			view.InternalCleanup();
			DestroyViewObject(view.gameObject);
		}

		/// <summary>
		/// Animated, awaited close of a view's children for the children-first policy.
		/// Presentation only (InternalHideAsync per child) — final settle (registry,
		/// pooling, destruction) still happens in the post-parent cascade, which is
		/// idempotent against hooks the animated hide already ran.
		/// </summary>
		private async UniTask CloseChildrenAsync(UIView parent, ChildCloseOrder order, bool immediate, CancellationToken ct)
		{
			if (!_viewRegistry.TryGetValue(parent, out var record)) return;

			// Snapshot — record.Children mutates as children settle during their closes.
			// A plain array, not a rented list: the sequential branch awaits between uses.
			UIView[] children = record.Children.ToArray();

			if (order == ChildCloseOrder.ChildrenFirstSequential)
			{
				for (int i = children.Length - 1; i >= 0; i--)
				{
					var child = children[i];
					if (child != null)
					{
						await CloseChildRecursivelyAsync(child, immediate, ct);
					}
				}
			}
			else
			{
				var tasks = new UniTask[children.Length];
				for (int i = children.Length - 1, t = 0; i >= 0; i--, t++)
				{
					var child = children[i];
					tasks[t] = child != null ? CloseChildRecursivelyAsync(child, immediate, ct) : UniTask.CompletedTask;
				}

				await UniTask.WhenAll(tasks);
			}
		}

		private async UniTask CloseChildRecursivelyAsync(UIView child, bool immediate, CancellationToken ct)
		{
			if (!_viewRegistry.TryGetValue(child, out var childRecord)) return;

			// A child that is itself children-first closes its own children first —
			// deep hierarchies compose level by level through this recursion.
			if (!immediate && childRecord.Children.Count > 0 &&
			    child.ChildCloseOrder != ChildCloseOrder.ParentFirst)
			{
				await CloseChildrenAsync(child, child.ChildCloseOrder, immediate, ct);
			}

			await child.InternalHideAsync(immediate, ct);
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
					Unregister(child);

					// Remove from any history stack
					if (child.ParentView != null && _historyStacks.TryGetValue(child.ParentView, out var history))
					{
						history.Remove(child);
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
					Unregister(child);

					if (child.ParentView != null && _historyStacks.TryGetValue(child.ParentView, out var history))
					{
						history.Remove(child);
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
							DestroyViewObject(child.gameObject);
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
						history.Remove(child);
					}
				}
			}

			_historyStacks.Remove(parent);
			_pendingShowTasks.Remove(parent);
		}

		private static bool ShouldDestroyView(ViewRecord record, CloseContext context)
		{
			if (record.IsDynamic) return true;
			return context == CloseContext.ParentDestroyed || context == CloseContext.ForceDestroy;
		}

		// =================================================================
		// HELPERS
		// =================================================================

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

			foreach ((UIChannel effectiveOrder, ViewStack stack) in _channelStacks)
			{
				if (stack.Count == 0) continue;

				// If a preferred channel is requested, only consider that channel.
				if (preferredChannel.HasValue && effectiveOrder != preferredChannel.Value)
					continue;

				// Prefer higher sort order (overlays > menus > HUD)
				if (effectiveOrder > bestSortOrder)
				{
					bestSortOrder = effectiveOrder;
					bestCandidate = stack.Peek();
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
		/// The channel stack a screen was pushed onto: its override when one was supplied at
		/// show time, else the channel component's sort order.
		/// </summary>
		private static UIChannel EffectiveChannel(UIView screen)
		{
			return screen._overriddenChannel ?? screen.Channel.SortOrder;
		}

		private ViewStack GetOrCreateChannelStack(UIChannel channel)
		{
			if (!_channelStacks.TryGetValue(channel, out var stack))
			{
				stack = new ViewStack();
				_channelStacks[channel] = stack;
			}

			return stack;
		}

		private ViewStack GetOrCreateHistory(UIView parent)
		{
			if (!_historyStacks.TryGetValue(parent, out var history))
			{
				history = new ViewStack();
				_historyStacks[parent] = history;
			}

			return history;
		}

		/// <summary>
		/// O(1) prefab lookup by kind. Caches the lookup dictionary on first access, along
		/// with whether each prefab is a screen so the show path never re-queries components.
		/// </summary>
		private bool TryFindPrefab(ViewKey key, out PrefabEntry entry)
		{
			if (_prefabLookup == null)
			{
				_prefabLookup = new Dictionary<ViewKey, PrefabEntry>();
				IReadOnlyList<UIView> views = _repository.Views;
				for (int i = 0; i < views.Count; i++)
				{
					UIView view = views[i];
					if (view != null)
						_prefabLookup[ViewKey.Of(view)] = new PrefabEntry(view, view.GetComponent<UIViewChannel>() != null);
				}
			}

			return _prefabLookup.TryGetValue(key, out entry);
		}

		/// <summary>
		/// Removes ghost entries from _viewRegistry, _historyStacks, and _pendingShowTasks
		/// where the UIView key has been destroyed by Unity (fake-null).
		/// Should be called periodically or on scene transitions.
		/// </summary>
		internal void CleanupDestroyedViews()
		{
			using PooledList<UIView> dead = ListPool<UIView>.Rent();

			foreach (var key in _viewRegistry.Keys)
			{
				if (key == null) dead.Add(key);
			}

			for (int i = 0; i < dead.Count; i++) Unregister(dead[i]);
			dead.List.Clear();

			foreach (var key in _historyStacks.Keys)
			{
				if (key == null) dead.Add(key);
			}

			for (int i = 0; i < dead.Count; i++) _historyStacks.Remove(dead[i]);
			dead.List.Clear();

			foreach (var key in _pendingShowTasks.Keys)
			{
				if (key == null) dead.Add(key);
			}

			for (int i = 0; i < dead.Count; i++) _pendingShowTasks.Remove(dead[i]);
		}

		/// <summary>Number of registered views. Diagnostics and tests.</summary>
		internal int RegisteredViewCount => _viewRegistry.Count;

		/// <summary>Registered views of a kind, walking the kind index. Diagnostics and tests.</summary>
		internal int CountOfKind(Type type, string viewId = "")
		{
			int count = 0;
			for (var record = FirstOfKind(new ViewKey(type, viewId)); record != null; record = record.NextOfKind)
			{
				count++;
			}

			return count;
		}

		/// <summary>
		/// Recomputes canvas sorting orders for every screen in a channel stack after a
		/// pop/mid-stack removal, keeping depths contiguous so a later push can't collide
		/// with a survivor's baked-in order. Stack bottom is depth 1 (matches push-time
		/// channel.Initialize(_uiCamera, stack.Count)).
		/// </summary>
		private static void RecomputeChannelSorting(ViewStack stack)
		{
			if (stack == null) return;

			for (int i = 0; i < stack.Count; i++)
			{
				var v = stack[i];
				if (v != null && v.HasChannel)
				{
					v.Channel.UpdateSortingOrder(i + 1);
				}
			}
		}

#if UNITY_EDITOR
		private void ShowViewByIndex(int index)
		{
			if (_repository == null || index < 0 || index >= _repository.Views.Count) return;
			var prefab = _repository.Views[index];
			Show<UIView>(prefab.GetType());
		}
#endif
	}
}
