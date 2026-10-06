using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Takes a registered view out of presentation: off its stack, hidden, its children
	/// cascaded, then pooled, destroyed, or — a static view on a normal close — hidden in
	/// place and kept registered. Whatever it uncovered is resumed according to the closed
	/// view's stack behaviour.
	/// Cancellation never skips settlement: a view already popped from its stack is settled
	/// regardless, or it would linger registered, active and off-stack.
	/// A close of a view that is already closing joins that close: it completes when the view
	/// has settled.
	/// </summary>
	internal sealed class ClosePipeline
	{
		private readonly ViewRegistry      _registry;
		private readonly ScreenStacks      _screens;
		private readonly FragmentHistories _histories;
		private readonly ViewFactory       _factory;

		public ClosePipeline(ViewRegistry registry, ScreenStacks screens, FragmentHistories histories, ViewFactory factory)
		{
			_registry = registry;
			_screens = screens;
			_histories = histories;
			_factory = factory;
		}

		public async UniTask CloseAsync(UIView view, CloseContext context, bool immediate, CancellationToken ct = default)
		{
			if (view == null || view.gameObject == null) return;

			if (!_registry.TryGet(view, out var record))
			{
				Debug.LogWarning($"Cannot close view '{view.name}': not registered (may have been closed via parent).");
				return;
			}

			if (record.IsClosing)
			{
				// The caller's token ends only its own wait, never the close in flight.
				await record.WhenCloseSettled().AttachExternalCancellation(ct);
				return;
			}

			record.IsClosing = true;
			try
			{
				if (view.HasChannel) await CloseScreenAsync(record, context, immediate, ct);
				else await CloseFragmentAsync(record, context, immediate, ct);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Debug.LogError($"Error closing '{view.name}': {ex.Message}\n{ex.StackTrace}");
			}
			finally
			{
				record.IsClosing = false;
				record.SignalCloseSettled();
			}
		}

		/// <summary>
		/// A registered view was destroyed outside the system: a direct Destroy, a scene unload.
		/// The view settles its own resources in OnDestroy, and its children do the same through
		/// theirs, so no cascade is needed here — only the bookkeeping goes. What the view
		/// covered comes back as if it had closed, a frame later, once the destroy is complete:
		/// a view below that goes in the same destroy is unregistered by then and left alone.
		/// Idempotent: system-driven closes have already removed everything.
		/// </summary>
		public void NotifyDestroyedExternally(UIView view)
		{
			ViewStack covered = null;
			UIView    below   = null;
			bool      screen  = false;

			ViewStackBehaviour behaviour = view.StackBehaviour;

			if (_registry.TryGet(view, out var record) && !record.IsClosing && !_factory.IsShuttingDown && StackPolicy.Pauses(behaviour))
			{
				covered = _screens.FindStackOf(view);
				screen = covered != null;
				if (!screen && record.Parent != null) _histories.TryGet(record.Parent, out covered);
				below = covered?.BelowOrNull(view);
			}

			_registry.Remove(view);
			_screens.RemoveEverywhere(view);
			_histories.RemoveEverywhere(view);
			_histories.Remove(view);

			if (below != null) ResumeAfterDestroyAsync(covered, below, behaviour, screen).Forget();
		}

		private async UniTaskVoid ResumeAfterDestroyAsync(ViewStack stack, UIView below, ViewStackBehaviour behaviour, bool screen)
		{
			await UniTask.NextFrame();

			if (below == null || !_registry.TryGet(below, out var record) || record.IsClosing) return;
			if (!stack.Contains(below) || stack.IsCoveredAbove(below)) return;

			// See CloseFragmentAsync: a resurfacing fragment keeps its data.
			if (!screen) below.SetContext(null);
			await ResumeBelowAsync(behaviour, below, immediate: false, cascade: screen, CancellationToken.None);
		}

		private async UniTask CloseScreenAsync(ViewRecord record, CloseContext context, bool immediate, CancellationToken ct)
		{
			UIView view = record.Instance;

			if (!_screens.TryGet(record.EffectiveChannel, out var stack))
			{
				await SettleAsync(record, context, immediate, ct);
				return;
			}

			// Not the top of its channel: nothing above it is affected, so it settles without
			// animation. The screen directly beneath may have been paused or hidden by this one;
			// it comes back only if nothing still above it covers it (see CloseFragmentAsync).
			if (stack.Count == 0 || stack.Peek() != view)
			{
				UIView             below     = stack.BelowOrNull(view);
				ViewStackBehaviour behaviour = view.StackBehaviour;
				_screens.Remove(stack, view);

				await SettleAsync(record, context, immediate: true, ct);

				if (below != null && _registry.Contains(below) && StackPolicy.Pauses(behaviour) && !stack.IsCoveredAbove(below))
				{
					await ResumeBelowAsync(behaviour, below, immediate, cascade: true, ct);
				}

				return;
			}

			_screens.Pop(stack);
			await SettleThenResumeAsync(record, context, immediate, stack.PeekOrNull(), cascade: true, ct);
		}

		private async UniTask CloseFragmentAsync(ViewRecord record, CloseContext context, bool immediate, CancellationToken ct)
		{
			UIView view   = record.Instance;
			UIView parent = record.Parent;

			if (parent == null || !_histories.TryGet(parent, out var history))
			{
				await SettleAsync(record, context, immediate, ct);
				return;
			}

			if (history.Count > 0 && history.Peek() == view && context == CloseContext.Normal)
			{
				history.Pop();
				UIView uncovered = history.PeekOrNull();

				// A resurfacing fragment keeps its data; one that never received any gets a
				// default, so a typed view does not see a null context (UIView<T>.SetContext).
				if (uncovered != null) uncovered.SetContext(null);

				await SettleThenResumeAsync(record, context, immediate, uncovered, cascade: false, ct);
				return;
			}

			// Mid-stack. The fragment directly beneath may have been paused or hidden by this
			// one; it comes back only if nothing still above it covers it.
			//   A → Shop(HideBelow) → Toast(DoNothing): closing Shop resumes A.
			//   A → B(HideBelow) → C(HideBelow) → Toast: closing B leaves A hidden under C.
			UIView             below     = history.BelowOrNull(view);
			ViewStackBehaviour behaviour = view.StackBehaviour;
			history.Remove(view);

			await SettleAsync(record, context, immediate, ct);

			if (below != null && _registry.Contains(below) && StackPolicy.Pauses(behaviour) && !history.IsCoveredAbove(below))
			{
				below.SetContext(null);
				await ResumeBelowAsync(behaviour, below, immediate, cascade: false, ct);
			}
		}

		/// <summary>
		/// Settles the view and resumes what it uncovered — together when the view below asks
		/// for it, else one after the other. The behaviour is captured first: settling clears
		/// the per-show override, and the resume must undo what the show actually did.
		/// </summary>
		private async UniTask SettleThenResumeAsync(ViewRecord record, CloseContext context, bool immediate, UIView below, bool cascade, CancellationToken ct)
		{
			ViewStackBehaviour behaviour = record.Instance.StackBehaviour;

			if (below == null)
			{
				await SettleAsync(record, context, immediate, ct);
				return;
			}

			if (!immediate && below.PlayInParallelWithPrevious)
			{
				await UniTask.WhenAll(
					SettleAsync(record, context, immediate, ct),
					ResumeBelowAsync(behaviour, below, immediate, cascade, ct));
				return;
			}

			await SettleAsync(record, context, immediate, ct);
			await ResumeBelowAsync(behaviour, below, immediate, cascade, ct);
		}

		/// <summary>
		/// Undoes what a view with <paramref name="behaviour"/> did to the one beneath it when
		/// it was shown. Screens cascade to the fragments in their history; fragments do not.
		/// </summary>
		private async UniTask ResumeBelowAsync(ViewStackBehaviour behaviour, UIView below, bool immediate, bool cascade, CancellationToken ct)
		{
			if (!StackPolicy.Pauses(behaviour)) return;
			if (below == null || below.gameObject == null) return;
			if (!_registry.Contains(below)) return;   // settled by a cascade while this close was in flight

			try
			{
				IViewLifecycle lifecycle = below.Lifecycle();

				if (StackPolicy.Hides(behaviour))
				{
					await lifecycle.ResumeAsync(immediate, ct);
				}

				if (cascade) ResumeFragments(below);
				lifecycle.Resume();
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Debug.LogError($"Error resuming '{below.name}': {ex.Message}\n{ex.StackTrace}");
			}
		}

		private void ResumeFragments(UIView parent)
		{
			if (!_histories.TryGet(parent, out var history)) return;

			foreach (var fragment in history)
			{
				if (fragment != null && fragment.gameObject != null)
					fragment.Lifecycle().Resume();
			}
		}

		/// <summary>
		/// Hides the view (children first when it asks for that), cascades to its children,
		/// then pools, destroys, or hides it in place. Only a static view on a normal close
		/// stays registered and in its parent's history.
		/// </summary>
		private async UniTask SettleAsync(ViewRecord record, CloseContext context, bool immediate, CancellationToken ct)
		{
			UIView         view      = record.Instance;
			IViewLifecycle lifecycle = view.Lifecycle();

			bool childrenFirst = !immediate && context == CloseContext.Normal &&
			                     record.Children.Count > 0 &&
			                     view.ChildCloseOrder != ChildCloseOrder.ParentFirst;

			if (childrenFirst)
			{
				try
				{
					await CloseChildrenAsync(record, view.ChildCloseOrder, ct);
				}
				catch (OperationCanceledException)
				{
					// each child settles in its own hide; the parent still settles below
				}
				catch (Exception ex)
				{
					Debug.LogError($"Error closing children of '{view.name}': {ex.Message}");
				}
			}

			try
			{
				await lifecycle.HideAsync(HideMode.Close, immediate || context != CloseContext.Normal, ct);
			}
			catch (OperationCanceledException)
			{
				// settle anyway — see the class summary
			}
			catch (Exception ex)
			{
				Debug.LogError($"Error hiding view '{view.name}': {ex.Message}");
			}

			// While the hide was in flight a parent may have closed and cascaded to this view;
			// it is settled already and must not be touched again.
			if (!_registry.Contains(view)) return;

			bool staysRegistered = record.IsStatic && context == CloseContext.Normal;
			bool pooled          = !staysRegistered && record.ShouldPool && _factory.CanPool(view);

			// Before the cascade, so the closes a view issues here still find its children registered.
			if (pooled) view.OnBeforePool();

			CascadeChildren(record, staysRegistered);
			lifecycle.OverrideStackBehaviour(null);

			if (staysRegistered)
			{
				record.ChannelOverride = null;
				view.gameObject.SetActive(false);
				return;
			}

			_registry.Remove(view);
			lifecycle.Teardown();

			if (pooled) Shelve(view);
			else ViewFactory.Destroy(view);
		}

		/// <summary>Prepares a settled view for reuse and hands it to the pool.</summary>
		private void Shelve(UIView view)
		{
			ResetForReuse(view);
			_factory.Release(view);
		}

		/// <summary>
		/// Resets a view on its way to the pool, and the static children that go with it: they
		/// stay in its hierarchy, and show again with it. Only views inside it, which also rules
		/// out a cycle; a static entry that points elsewhere is not reused with it.
		/// </summary>
		private static void ResetForReuse(UIView view)
		{
			if (view.TryGetComponent(out ViewHighlight highlight))
			{
				highlight.Restore();
			}

			view.OnReset();

			IReadOnlyList<StaticViewEntry> statics = view.StaticViews;
			for (int i = 0; i < statics.Count; i++)
			{
				UIView child = statics[i].View;
				if (child != null && child != view && child.transform.IsChildOf(view.transform))
				{
					ResetForReuse(child);
				}
			}
		}

		/// <summary>
		/// Settles the children of a view that is closing, deepest first. Dynamic children
		/// never outlive the close: pooled when they ask for it and their kind has room,
		/// destroyed otherwise. Static children are torn down; under a parent that stays
		/// registered they stay registered too, merely hidden; otherwise they are unregistered
		/// and left in the hierarchy for the parent's fate (reset and re-attached on reuse from
		/// the pool, or destroyed with it). The parent's own history and show gate go with the
		/// children.
		/// </summary>
		private void CascadeChildren(ViewRecord parent, bool parentStaysRegistered)
		{
			for (int i = parent.Children.Count - 1; i >= 0; i--)
			{
				UIView child = parent.Children[i];
				if (child == null || !_registry.TryGet(child, out var childRecord)) continue;

				bool staticSurvives = childRecord.IsStatic && parentStaysRegistered;
				bool pooled         = !staticSurvives && childRecord.ShouldPool && _factory.CanPool(child);

				if (pooled) child.OnBeforePool();

				CascadeChildren(childRecord, staticSurvives);
				child.Lifecycle().Teardown();
				_histories.Remove(parent.Instance, child);

				if (staticSurvives)
				{
					child.gameObject.SetActive(false);
					continue;
				}

				_registry.Remove(child);

				if (childRecord.IsStatic) continue;
				if (child == null || child.gameObject == null) continue;   // a teardown hook may have destroyed it

				if (pooled) Shelve(child);
				else ViewFactory.Destroy(child);
			}

			_histories.Remove(parent.Instance);
		}

		/// <summary>
		/// Animated, awaited close of a view's children for the children-first policy.
		/// Presentation only (a close hide per child) — the final settle (registry, pooling,
		/// destruction) still happens in the cascade after the parent hides, which is
		/// idempotent against hooks the animated hide already ran.
		/// </summary>
		private async UniTask CloseChildrenAsync(ViewRecord record, ChildCloseOrder order, CancellationToken ct)
		{
			// Snapshot — record.Children mutates as children settle during their closes.
			UIView[] children = record.Children.ToArray();

			if (order == ChildCloseOrder.ChildrenFirstSequential)
			{
				for (int i = children.Length - 1; i >= 0; i--)
				{
					if (children[i] != null)
						await CloseChildRecursivelyAsync(children[i], ct);
				}

				return;
			}

			var tasks = new UniTask[children.Length];
			for (int i = children.Length - 1, t = 0; i >= 0; i--, t++)
			{
				tasks[t] = children[i] != null ? CloseChildRecursivelyAsync(children[i], ct) : UniTask.CompletedTask;
			}

			await UniTask.WhenAll(tasks);
		}

		private async UniTask CloseChildRecursivelyAsync(UIView child, CancellationToken ct)
		{
			if (!_registry.TryGet(child, out var record)) return;

			if (record.Children.Count > 0 && child.ChildCloseOrder != ChildCloseOrder.ParentFirst)
			{
				await CloseChildrenAsync(record, child.ChildCloseOrder, ct);
			}

			await child.Lifecycle().HideAsync(HideMode.Close, false, ct);
		}
	}
}
