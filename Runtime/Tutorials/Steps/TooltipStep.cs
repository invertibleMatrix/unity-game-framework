using System.Threading;
using AK.Systems;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Tutorials
{
	[CreateAssetMenu(fileName = "TooltipStep", menuName = "AK/Tutorials/TooltipStep")]
	public class TooltipStep : TutorialStep
	{
		public string Title;

		[TextArea(2, 4)] public string Description;

		public Sprite Icon;

		[Tooltip("UITargetId of the element to spotlight and anchor the tooltip to.")]
		public UITargetId TargetId;

		[Tooltip("Tooltip placement relative to the target. Auto resolves from available space.")]
		public UIViewTooltip.TooltipPosition Position = UIViewTooltip.TooltipPosition.Auto;

		[Tooltip("Extra offset in canvas units applied to the tooltip position.")]
		public Vector2 Offset;

		public float CloseTime = 3f;

		public string TooltipId;

		public override async UniTask PresentAsync(TutorialStepContext context, CancellationToken ct)
		{
			if (TargetId == null || !context.Targets.TryGet(TargetId, out var target) || target == null)
			{
				Debug.LogWarning(
					$"[SpotlightTooltipStep] Target '{(TargetId != null ? TargetId.name : "null")}' is not registered — skipping presentation of '{name}'.");
				return;
			}

			var tooltip = context.UiSystem.Show<UIViewTooltip>(new UIViewTooltipContext(Title, Description, target, Position)
			{
				Icon = Icon,
				Offset = Offset,
				TapAnywhereToClose = false,
				CloseTime = CloseTime
			}, viewId: TooltipId);

			if (CloseTime > 0)
			{
				await UniTask.WaitForSeconds(CloseTime, cancellationToken: ct);
				await tooltip.CloseAsync(ct: ct);
			}
		}
	}
}