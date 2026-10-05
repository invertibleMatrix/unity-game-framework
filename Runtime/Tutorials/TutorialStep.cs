using System.Collections.Generic;
using System.Threading;
using AK.Core.Extensions;
using AK.CoreDomain.Facts;
using AK.Kernel.Timing;
using AK.Systems;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// One step of a tutorial: the conditions that must be met before it presents,
	/// plus its presentation. The base assumes nothing about presentation — subclass
	/// assets carry whatever data their presentation needs (SpotlightTooltipStep,
	/// FragmentStep, or game-specific subclasses).
	/// </summary>
	public abstract class TutorialStep : ScriptableObject
	{
		[Tooltip("Facts that must have occurred before this step can present.")]
		public List<FactCondition> Conditions = new();

		[Tooltip("Seconds to wait after this step's conditions are met before it presents. Unscaled: the wait also ends in a game paused at timeScale 0.")]
		public float StartDelay;

		[Tooltip("When true (default), all input is blocked from the moment the step starts — through StartDelay — until the step's presentation goes live. Set false for steps where an intermediate UI must stay interactive between conditions-met and presentation.")]
		public bool BlockInputUntilPresented = true;

		/// <summary>
		/// Presents the step and completes when the step is done. The base waits out
		/// <see cref="StartDelay"/>. A presentation that governs input itself releases
		/// <see cref="TutorialStepContext.InputHold"/> once it is up.
		/// </summary>
		public virtual UniTask PresentAsync(TutorialStepContext context, CancellationToken ct)
		{
			return WaitForStartDelayAsync(ct);
		}

		/// <summary>Waits out <see cref="StartDelay"/>, for overrides that don't call the base.</summary>
		protected UniTask WaitForStartDelayAsync(CancellationToken ct)
		{
			return WaitAsync(StartDelay, ct);
		}

		/// <summary>
		/// Waits <paramref name="seconds"/> of unscaled time; zero or less doesn't wait. Tutorials
		/// run on unscaled time, so a game paused at timeScale 0 can't freeze a step that is
		/// holding input, and time spent in the background doesn't count
		/// (<see cref="TimeDomainExt.Delay"/>). Cancellation ends the wait at once.
		/// </summary>
		protected static UniTask WaitAsync(float seconds, CancellationToken ct)
		{
			if (!(seconds > 0f)) return UniTask.CompletedTask;

			return TimeDomain.Unscaled.Delay(seconds, ct, cancelImmediately: true);
		}

		/// <summary>
		/// True when the UI this step presents on is registered. Consulted with the fact
		/// conditions before a step is considered due, so a step never waits for its
		/// surface: the surface that registers the target requests a checkpoint when it
		/// does, and that checkpoint is what presents the step.
		/// </summary>
		public virtual bool IsTargetPresent(TutorialStepContext context) => true;

		/// <summary>
		/// Registry lookup for <see cref="IsTargetPresent"/> overrides. A null id reports
		/// present so the step still reaches PresentAsync, which logs the misconfiguration
		/// and skips — an unassigned target must fail loudly, not stall the tutorial silently.
		/// </summary>
		protected static bool IsTargetRegistered(TutorialStepContext context, UITargetId targetId)
		{
			if (targetId == null)
			{
				return true;
			}

			return context.Targets != null && context.Targets.TryGet(targetId, out var target) && target != null;
		}

		/// <summary>
		/// Resolves the step's target, declining when it is not registered: due-time gating
		/// makes an absent target here a race (unregistered between the due check and the
		/// start delay), not a wait. The runner leaves progress untouched and the next
		/// checkpoint retries.
		/// </summary>
		protected RectTransform ResolveRegisteredTarget(TutorialStepContext context, UITargetId targetId)
		{
			if (context.Targets != null && context.Targets.TryGet(targetId, out var target) && target != null)
			{
				return target;
			}

			throw new TutorialStepDeclinedException($"Step '{name}': target '{(targetId != null ? targetId.name : "null")}' is not registered.");
		}

		// A target registers when its UITarget is enabled, so a step presenting the moment its
		// host view activates finds it. One a game registers itself can land a little later (a
		// list item spawned after its view shows), so a step polls briefly instead of failing on
		// the first lookup. Bounded, and on unscaled time: the input gate is held until
		// presentation, so an unregistered target must never hang the game, paused or not.
		protected const float TargetWaitTimeout  = 3f;
		protected const float TargetPollInterval = 0.1f;

		/// <summary>
		/// The target once it has registered, or null when <paramref name="id"/> is null or the
		/// target hasn't registered within <see cref="TargetWaitTimeout"/> seconds.
		/// </summary>
		protected static async UniTask<RectTransform> WaitForTargetAsync(TutorialStepContext context, UITargetId id, CancellationToken ct)
		{
			if (id == null || context.Targets == null)
			{
				return null;
			}

			// Adds up its polls instead of reading a clock, so it runs on unscaled time like every
			// step wait, and time spent in the background doesn't count.
			double waited = 0d;
			while (true)
			{
				if (context.Targets.TryGet(id, out var target) && target != null)
				{
					return target;
				}

				if (waited >= TargetWaitTimeout)
				{
					return null;
				}

				await WaitAsync(TargetPollInterval, ct);
				waited += TargetPollInterval;
			}
		}

		/// <summary>
		/// Completes when <paramref name="view"/>'s current show ends: the view starts closing, is
		/// destroyed, or has been reused from the pool for another show. Cancellation ends the
		/// wait at once.
		/// </summary>
		protected static UniTask WaitUntilClosedAsync(UIView view, CancellationToken ct)
		{
			if (view == null) return UniTask.CompletedTask;

			// A pooled view drops its context when it is put away, so a reuse can't pass for this show.
			UIContext shown = view.Context;
			return UniTask.WaitUntil(
				() => view == null || view.Context != shown || view.State is ViewState.Hiding or ViewState.Hidden,
				cancellationToken: ct, cancelImmediately: true);
		}
	}
}
