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

		[Tooltip("Tooltip variant (ViewId). Empty shows the default tooltip.")]
		public string TooltipId;

		[Header("Spotlight")]
		[Tooltip("Extra padding around the spotlight hole (screen pixels).")]
		public float SpotlightPadding = 20f;

		[Tooltip("Feather/softness of the spotlight rim (screen pixels).")]
		public float SpotlightFeather = 15f;

		[Tooltip("Seconds for the spotlight iris-in. 0 = instant.")]
		public float SpotlightIntroDuration = 0.6f;

		public Ease SpotlightIntroEase = Ease.OutCubic;

		[Tooltip("Seconds after the tooltip appears before dim-tap or the hole can advance. Mash taps in this window are ignored; the player must tap again after.")]
		public float DismissHoldDuration = 1.5f;

		[Tooltip("Tint the dim with DimColor instead of the spotlight prefab's color.")]
		public bool OverrideDimColor;

		public Color DimColor = new(0f, 0f, 0f, 0.96f);

		/// <summary>
		/// When false (simple tap-anywhere steps), the hole still looks open but
		/// consumes the tap and dismisses. Advance-on-fact / pointer steps override
		/// so the spotlighted control is clickable.
		/// </summary>
		protected virtual bool PassClicksThroughHole => false;

		public override async UniTask PresentAsync(TutorialStepContext context, CancellationToken ct)
		{
			await base.PresentAsync(context, ct);

			var target = await WaitForTargetAsync(context, TargetId, ct);
			if (target == null)
			{
				Debug.LogError($"[SpotlightTooltipStep] Target '{(TargetId != null ? TargetId.name : "null")}' not registered within {TargetWaitTimeout:0.#}s — declining presentation of '{name}'.");
				throw new TutorialStepDeclinedException($"Step '{name}': target '{(TargetId != null ? TargetId.name : "null")}' not registered.");
			}

			var spotlight = context.UiSystem.Show<UIViewSpotlight>(
				ShowOptions.With(CreateSpotlightContext()),
				s => s.SetTargets(new[] { target }, animateSpotlight: true));

			var tooltip = context.UiSystem.Show<UIViewTooltip>(ShowOptions.Variant(TooltipId, new UIViewTooltipContext(Title, Description, target, Position)
			{
				Icon = Icon,
				Offset = Offset,
				TapAnywhereToClose = false,
				CloseTime = 0f
			}));

			spotlight.AttachFurniture(tooltip.RectTransform);

			// Presentation is live: the spotlight now governs input. Simple steps
			// keep the hole closed so tap-anywhere dismisses; advance steps open it.
			// The spotlight holds input itself while its intro plays.
			context.InputHold.Release();

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

		protected UIViewSpotlightContext CreateSpotlightContext()
		{
			return new UIViewSpotlightContext
			{
				Padding = SpotlightPadding,
				Feather = SpotlightFeather,
				IntroDuration = SpotlightIntroDuration,
				IntroEase = SpotlightIntroEase,
				DismissHoldDuration = DismissHoldDuration,
				PassClicksThroughHole = PassClicksThroughHole,
				DimColor = OverrideDimColor ? DimColor : null,
			};
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
