using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Turns a show request into a presented view: resolves what to show (a static
	/// registration, a template clone, or a repository prefab), spawns and registers it,
	/// puts it on its channel stack or its parent's history, applies its stack behaviour to
	/// whatever it lands on, and plays the entrance. Preparation is synchronous — the view
	/// exists and is registered when a show call returns; only the presentation is awaited.
	/// </summary>
	internal sealed class ShowPipeline
	{
		private readonly IViewHost         _host;
		private readonly ViewRegistry      _registry;
		private readonly ScreenStacks      _screens;
		private readonly FragmentHistories _histories;
		private readonly ViewFactory       _factory;
		private readonly ClosePipeline     _closer;

		public ShowPipeline(IViewHost host, ViewRegistry registry, ScreenStacks screens, FragmentHistories histories,
		                    ViewFactory factory, ClosePipeline closer)
		{
			_host = host;
			_registry = registry;
			_screens = screens;
			_histories = histories;
			_factory = factory;
			_closer = closer;
		}

		// =================================================================
		// ENTRY POINTS
		// =================================================================

		/// <summary>
		/// Prepares a view of <paramref name="type"/> and returns it with its presentation
		/// task. The caller decides whether to await the presentation or fire and forget.
		/// </summary>
		public (TView view, UniTask presentation) Show<TView>(Type type, in ShowOptions options, Action<TView> onInit)
			where TView : UIView
		{
			var key = new ViewKey(type, options.ViewId);

			UIContext           context         = options.Context;
			UIView              parent          = options.Parent;
			UIChannel?          channelOverride = options.Channel;
			ViewStackBehaviour? stackBehaviour  = options.StackBehaviour;
			bool                immediate       = options.IsImmediate;
			bool                serialized      = options.IsSerialized;

			// Static registrations take precedence over the repository:
			//   • Explicit parent → reuse only a static already registered under THAT parent.
			//   • No parent       → reuse any static for the kind and re-route it onto its
			//                       own registered host parent (its effective parent).
			// Views mid-close are excluded so we never reuse a view that is tearing down.
			ViewRecord staticRecord = _registry.FindStatic(key, parent);

			if (staticRecord != null && staticRecord.Instance is TView staticView)
			{
				UIView host = parent ?? staticRecord.Parent;

				// Templates never show or get reused themselves — they are clone sources.
				if (staticView.IsTemplate)
				{
					return ShowClone(staticView, host, context, stackBehaviour, onInit, immediate, serialized);
				}

				onInit?.Invoke(staticView);
				return (staticView, serialized
					? ShowRegisteredAsync(staticView, host, context, stackBehaviour, immediate)
					: ShowRegisteredParallelAsync(staticView, host, context));
			}

			if (!_factory.TryGetPrefab(key, out ViewFactory.PrefabEntry entry) || entry.Prefab is not TView prefab)
			{
				Debug.LogError($"View prefab of type {type.Name} with ID '{key.ViewId}' not found in UIViewRepository.");
				return (null, UniTask.CompletedTask);
			}

			bool isScreen = entry.IsScreen;

			if (!isScreen && parent == null)
			{
				parent = _screens.FindBestHost(channelOverride);
				if (parent == null)
				{
					Debug.LogError($"Cannot show fragment '{type.Name}': no suitable parent found.");
					return (null, UniTask.CompletedTask);
				}
			}

			// A dynamic instance already open on this parent is closed-and-replaced when
			// multiple instances are not allowed. The close is sequenced BEFORE the new show:
			// otherwise the old close's resume-of-previous could run after the new show paused
			// that same view, leaving it visible and interactable on top of the new one.
			UIView replaced = prefab.AllowMultipleInstances ? null : _registry.FindDynamic(key, parent)?.Instance;

			TView view = _factory.Spawn(prefab, isScreen, parent);
			Prepare(view, parent, context, stackBehaviour, onInit,
			        new ViewRecord(view, parent, isStatic: false) { ChannelOverride = channelOverride });

			UniTask presentation = isScreen
				? PresentScreenAsync(view, replaced, channelOverride, immediate)
				: PresentFragmentAsync(view, replaced, parent, immediate, serialized);

			return (view, presentation);
		}

		/// <summary>Shows a view that is already registered: a static, a paused fragment, a pooled reuse.</summary>
		public UniTask ShowExistingAsync(UIView view, in ShowOptions options, CancellationToken ct = default)
		{
			if (!TryResolveExisting(view, out var record)) return UniTask.CompletedTask;

			if (options.IsSerialized || options.IsImmediate)
			{
				return ShowRegisteredAsync(view, record.Parent, options.Context, options.StackBehaviour, options.IsImmediate, ct);
			}

			return ShowRegisteredParallelAsync(view, record.Parent, options.Context, ct);
		}

		/// <summary>
		/// Every ShowOnStart child is pushed onto the parent's history synchronously, then all
		/// entrances run at once. Going through the per-parent show gate instead would make
		/// child N wait for child N-1's animation.
		/// </summary>
		public void ShowStaticChildren(UIView parent)
		{
			IReadOnlyList<StaticViewEntry> entries = parent.StaticViews;
			if (entries.Count == 0) return;

			ViewStack history = _histories.GetOrCreate(parent);

			for (int i = 0; i < entries.Count; i++)
			{
				StaticViewEntry entry = entries[i];
				UIView          child = entry.View;
				if (child == null || !entry.ShowOnStart) continue;
				if (child.IsTemplate) continue;
				if (!_registry.Contains(child)) continue;

				// Hidden until its entrance so an active fragment never flashes first.
				child.Lifecycle().Conceal();

				if (entry.ShowOnStartDelay > 0f)
				{
					ShowStaticChildDelayedAsync(parent, child, entry.ShowOnStartDelay).Forget();
					continue;
				}

				history.MoveToTop(child);
				child.Lifecycle().ShowAsync(false, default).Forget();
			}
		}

		// =================================================================
		// PREPARATION
		// =================================================================

		/// <summary>Injects, binds and registers a spawned view and its static children, without presenting it.</summary>
		private void Prepare<TView>(TView view, UIView parent, UIContext context, ViewStackBehaviour? stackBehaviour,
		                            Action<TView> onInit, ViewRecord record)
			where TView : UIView
		{
			IViewLifecycle lifecycle = view.Lifecycle();
			lifecycle.Attach(_host, parent, _factory.Container);
			lifecycle.OverrideStackBehaviour(stackBehaviour);
			lifecycle.Conceal();

			onInit?.Invoke(view);
			view.SetContext(context);

			_registry.Add(record);
			lifecycle.AttachStaticChildren(_factory.Container);
		}

		/// <summary>
		/// Clones a template and prepares the clone exactly like a prefab-spawned dynamic
		/// fragment under the same parent. Clones register as dynamic, so every show re-finds
		/// the template — unlimited clones, and the template itself never presents.
		/// </summary>
		private (TView view, UniTask presentation) ShowClone<TView>(TView template, UIView parent, UIContext context,
		                                                            ViewStackBehaviour? stackBehaviour, Action<TView> onInit,
		                                                            bool immediate, bool serialized)
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

			TView clone = _factory.Clone(template);
			Prepare(clone, parent, context, stackBehaviour, onInit, new ViewRecord(clone, parent, isStatic: false, isClone: true));

			return (clone, PresentFragmentAsync(clone, null, parent, immediate, serialized));
		}

		private static void Prime(UIView view, UIContext context, ViewStackBehaviour? stackBehaviour)
		{
			IViewLifecycle lifecycle = view.Lifecycle();
			lifecycle.OverrideStackBehaviour(stackBehaviour);
			lifecycle.Conceal();
			view.SetContext(context);
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

			if (!_registry.TryGet(view, out record))
			{
				Debug.LogError($"Cannot show view '{view.name}': not registered.", view);
				return false;
			}

			return true;
		}

		// =================================================================
		// PRESENTATION
		// =================================================================

		/// <summary>Pushes a fresh screen onto its channel and shows it over the screen beneath.</summary>
		private async UniTask PresentScreenAsync(UIView view, UIView replaced, UIChannel? channelOverride, bool immediate,
		                                         CancellationToken ct = default)
		{
			if (replaced != null) await CloseReplacedAsync(replaced);

			ViewStack stack = _screens.Push(view, channelOverride ?? view.Channel.SortOrder);
			await ShowOverAsync(view, stack.PeekBelowTopOrNull(), immediate, cascade: true, ct);
		}

		/// <summary>
		/// Shows a fresh fragment on its parent. Serialized shows queue behind the parent's
		/// gate and negotiate with the fragment beneath; parallel shows push history
		/// synchronously and animate at once, skipping both.
		/// </summary>
		private async UniTask PresentFragmentAsync(UIView view, UIView replaced, UIView parent, bool immediate, bool serialized,
		                                           CancellationToken ct = default)
		{
			if (replaced != null) await CloseReplacedAsync(replaced);

			if (!serialized)
			{
				view.Lifecycle().Conceal();
				_histories.GetOrCreate(parent).MoveToTop(view);
				await view.Lifecycle().ShowAsync(immediate, ct);
				return;
			}

			// Join the queue before the first await so shows issued in one tick line up in call order.
			using FragmentHistories.GateScope gate = _histories.EnterGate(parent);
			try
			{
				await gate.WaitForTurnAsync(ct);

				// A parent that closed while this show was queued has already settled the view.
				if (_registry.Contains(view))
				{
					await ShowInHistoryAsync(view, parent, immediate, ct);
				}

				gate.Complete();
			}
			catch (Exception ex)
			{
				gate.Fail(ex);
				throw;
			}
		}

		/// <summary>Serialized show of a registered view: behind the parent's gate, negotiating with the fragment beneath.</summary>
		private async UniTask ShowRegisteredAsync(UIView view, UIView parent, UIContext context, ViewStackBehaviour? stackBehaviour,
		                                          bool immediate, CancellationToken ct = default)
		{
			if (parent == null)
			{
				Prime(view, context, stackBehaviour);
				await view.Lifecycle().ShowAsync(immediate, ct);
				return;
			}

			using FragmentHistories.GateScope gate = _histories.EnterGate(parent);
			try
			{
				await gate.WaitForTurnAsync(ct);

				if (_registry.Contains(view))
				{
					Prime(view, context, stackBehaviour);
					await ShowInHistoryAsync(view, parent, immediate, ct);
				}

				gate.Complete();
			}
			catch (Exception ex)
			{
				gate.Fail(ex);
				throw;
			}
		}

		/// <summary>
		/// Parallel show of a registered fragment: history is pushed synchronously and the
		/// entrance runs at once, like ShowStaticChildren does for ShowOnStart children. For
		/// independent siblings with no stack behaviour between them.
		/// </summary>
		private UniTask ShowRegisteredParallelAsync(UIView view, UIView parent, UIContext context, CancellationToken ct = default)
		{
			if (parent == null)
			{
				return ShowRegisteredAsync(view, null, context, null, false, ct);
			}

			view.Lifecycle().Conceal();
			view.SetContext(context);
			_histories.GetOrCreate(parent).MoveToTop(view);

			return view.Lifecycle().ShowAsync(false, ct);
		}

		/// <summary>Puts the view on top of its parent's history and shows it over whatever was there.</summary>
		private UniTask ShowInHistoryAsync(UIView view, UIView parent, bool immediate, CancellationToken ct)
		{
			ViewStack history = _histories.GetOrCreate(parent);

			UIView below = history.PeekOrNull();
			if (below == view) below = null;

			history.MoveToTop(view);
			view.Lifecycle().Conceal();

			return ShowOverAsync(view, below, immediate, cascade: false, ct);
		}

		/// <summary>
		/// Applies the view's stack behaviour to the one beneath and plays the entrance —
		/// together when the view below asks for it, else pause first.
		/// </summary>
		private async UniTask ShowOverAsync(UIView view, UIView below, bool immediate, bool cascade, CancellationToken ct)
		{
			IViewLifecycle lifecycle = view.Lifecycle();

			if (below == null)
			{
				await lifecycle.ShowAsync(immediate, ct);
				return;
			}

			UniTask pause = PauseBelowAsync(view, below, immediate, cascade, ct);

			if (!immediate && below.PlayInParallelWithPrevious)
			{
				await UniTask.WhenAll(pause, lifecycle.ShowAsync(immediate, ct));
				return;
			}

			await pause;
			await lifecycle.ShowAsync(immediate, ct);
		}

		/// <summary>
		/// What a view landing on <paramref name="below"/> does to it. Screens cascade the
		/// pause to the fragments in their history; fragments do not. Failures are logged and
		/// never block the show.
		/// </summary>
		private async UniTask PauseBelowAsync(UIView above, UIView below, bool immediate, bool cascade, CancellationToken ct)
		{
			ViewStackBehaviour behaviour = above.StackBehaviour;

			try
			{
				if (StackPolicy.Closes(behaviour))
				{
					await _closer.CloseAsync(below, CloseContext.Normal, immediate: true, ct);
					return;
				}

				if (!StackPolicy.Pauses(behaviour)) return;

				IViewLifecycle lifecycle = below.Lifecycle();
				lifecycle.Pause();
				if (cascade) PauseFragments(below);

				if (!StackPolicy.Hides(behaviour)) return;

				if (immediate) lifecycle.Conceal();
				else await lifecycle.HideAsync(HideMode.Pause, false, ct);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Debug.LogError($"Error pausing '{below.name}' under '{above.name}': {ex.Message}\n{ex.StackTrace}");
			}
		}

		private void PauseFragments(UIView parent)
		{
			if (!_histories.TryGet(parent, out var history)) return;

			foreach (var fragment in history)
			{
				if (fragment != null && fragment.gameObject != null)
					fragment.OnPause();
			}
		}

		/// <summary>Closes the instance being replaced before the new show starts. Close failures are logged but never block the show.</summary>
		private async UniTask CloseReplacedAsync(UIView replaced)
		{
			try
			{
				await _closer.CloseAsync(replaced, CloseContext.Normal, false);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		/// <summary>
		/// Waits out a ShowOnStart delay, then shows the fragment through the parallel path.
		/// The armed token is cancelled by every close and teardown path on the view, and the
		/// post-delay guards catch a parent that went away or a fragment someone else showed
		/// during the wait — so a delayed start never pops a fragment back open after it was closed.
		/// </summary>
		private async UniTask ShowStaticChildDelayedAsync(UIView parent, UIView child, float delay)
		{
			CancellationToken ct = child.Lifecycle().ArmDelayedStart();

			try
			{
				await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			if (child == null || child.gameObject == null) return;
			if (parent == null || parent.gameObject == null) return;
			if (_registry.IsClosing(child) || _registry.IsClosing(parent)) return;
			if (child.IsVisible) return;
			if (!_registry.Contains(child)) return;

			await ShowRegisteredParallelAsync(child, parent, null, ct);
		}
	}
}
