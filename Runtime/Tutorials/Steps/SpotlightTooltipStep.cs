using System.Threading;
using AK.Systems;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// Spotlight over the step's registered target with a tooltip riding the furniture
	/// layer. Dim-tap advances; the tooltip stays open until the step completes.
	/// </summary>
	[CreateAssetMenu(fileName = "SpotlightTooltipStep", menuName = "AK/Tutorials/Spotlight Tooltip Step")]
	public class SpotlightTooltipStep : TutorialStep
	{
		public string Title;

		[TextArea(2, 4)]
		public string Description;

		public Sprite Icon;
		
		[Tooltip("UITargetId of the element to spotlight and anchor the tooltip to.")]
		public UITargetId TargetId;

		[Tooltip("Tooltip placement relative to the target. Auto resolves from available space.")]
		public UIViewTooltip.TooltipPosition Position = UIViewTooltip.TooltipPosition.Auto;

		[Tooltip("Extra offset in canvas units applied to the tooltip position.")]
		public Vector2 Offset;

		[Header("Spotlight")]
		[Tooltip("Extra padding around the spotlight hole (screen pixels).")]
		public float SpotlightPadding = 20f;

		[Tooltip("Feather/softness of the spotlight rim (screen pixels).")]
		public float SpotlightFeather = 15f;

		[Tooltip("Seconds for the spotlight iris-in. 0 = instant.")]
		public float SpotlightIntroDuration = 0.6f;

		public Ease SpotlightIntroEase = Ease.OutCubic;

		public override async UniTask PresentAsync(TutorialStepContext context, CancellationToken ct)
		{
			await base.PresentAsync(context, ct);

			var target = await WaitForTargetAsync(context, TargetId, ct);
			if (target == null)
			{
				Debug.LogError($"[SpotlightTooltipStep] Target '{(TargetId != null ? TargetId.name : "null")}' not registered within {TargetWaitTimeout:0.#}s — declining presentation of '{name}'.");
				throw new TutorialStepDeclinedException($"Step '{name}': target '{(TargetId != null ? TargetId.name : "null")}' not registered.");
			}

			var spotlight = context.UiSystem.Show<UIViewSpotlight>(ShowOptions.With(new UIViewSpotlightContext
				{
					Padding = SpotlightPadding,
					Feather = SpotlightFeather,
					IntroDuration = SpotlightIntroDuration,
					IntroEase = SpotlightIntroEase
				}), s => s.SetTargets(new[] { target }, animateSpotlight: true));

			var tooltip = context.UiSystem.Show<UIViewTooltip>(ShowOptions.With(new UIViewTooltipContext(Title, Description, target, Position)
			{
				Icon = Icon,
				Offset = Offset,
				TapAnywhereToClose = false,
				CloseTime = 0f
			}));

			spotlight.AttachFurniture(tooltip.RectTransform);

			// Presentation is live: the spotlight now governs input (dim blocks,
			// hole passes clicks to the target), so the gate opens and the
			// spotlighted control is clickable.
			context.InputGate.Release();

			try
			{
				await WaitForAdvanceAsync(context, spotlight, ct);
			}
			finally
			{
				// Cancellation (view destroyed mid-step) must not leak the dim —
				// a stray full-screen raycast view soft-locks the game.
				if (tooltip != null) tooltip.Close();
				if (spotlight != null) spotlight.Close();
			}
		}

		// UITarget registers in Start(), which Unity runs before the next Update — a
		// step presenting in the same frame its host view activates (checkpoint
		// chains hop surfaces like this) must poll briefly instead of failing on
		// the first lookup. Bounded: the input gate is held until presentation, so
		// an unregistered target must never hang the game.
		protected const float TargetWaitTimeout   = 3f;
		protected const float TargetPollInterval  = 0.1f;

		protected async UniTask<RectTransform> WaitForTargetAsync(TutorialStepContext context, UITargetId id, CancellationToken ct)
		{
			if (id == null)
			{
				return null;
			}

			float deadline = Time.realtimeSinceStartup + TargetWaitTimeout;
			while (true)
			{
				if (context.Targets.TryGet(id, out var target) && target != null)
				{
					return target;
				}

				if (Time.realtimeSinceStartup >= deadline)
				{
					return null;
				}

				await UniTask.WaitForSeconds(TargetPollInterval, cancellationToken: ct);
			}
		}

		// The advance seam: base completes on dim-tap; game subclasses override to
		// complete on game facts instead (e.g. the spotlighted button being pressed).
		protected virtual async UniTask WaitForAdvanceAsync(TutorialStepContext context, UIViewSpotlight spotlight, CancellationToken ct)
		{
			var completion = new UniTaskCompletionSource();

			void OnTapped() => completion.TrySetResult();

			spotlight.BackgroundTapped += OnTapped;

			try
			{
				await completion.Task.AttachExternalCancellation(ct);
			}
			finally
			{
				spotlight.BackgroundTapped -= OnTapped;
			}
		}
	}
}
