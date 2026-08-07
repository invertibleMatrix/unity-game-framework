using System.Threading;
using AK.Systems;
using Cysharp.Threading.Tasks;
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

		public override async UniTask PresentAsync(TutorialStepContext context, CancellationToken ct)
		{
			if (TargetId == null || !context.Targets.TryGet(TargetId, out var target) || target == null)
			{
				Debug.LogWarning($"[SpotlightTooltipStep] Target '{(TargetId != null ? TargetId.name : "null")}' is not registered — skipping presentation of '{name}'.");
				return;
			}

			var spotlight = context.UiSystem.Show<UIViewSpotlight>(onInit: s =>
				s.SetTargets(new[] { target }, animateSpotlight: true));

			var tooltip = context.UiSystem.Show<UIViewTooltip>(new UIViewTooltipContext(Title, Description, target, Position)
			{
				Icon = Icon,
				Offset = Offset,
				TapAnywhereToClose = false,
				CloseTime = 0f
			});

			spotlight.AttachFurniture(tooltip.RectTransform);

			await WaitForAdvanceAsync(context, spotlight, ct);

			if (tooltip != null) tooltip.Close();
			if (spotlight != null) spotlight.Close();
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
