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

		[Tooltip("Seconds before the tooltip closes by itself. Zero or less keeps it open until the player taps anywhere.")]
		public float CloseTime = 3f;

		public string TooltipId;

		public override async UniTask PresentAsync(TutorialStepContext context, CancellationToken ct)
		{
			await base.PresentAsync(context, ct);

			var target = await WaitForTargetAsync(context, TargetId, ct);
			if (target == null)
			{
				Debug.LogError($"[TooltipStep] Target '{(TargetId != null ? TargetId.name : "null")}' not registered within {TargetWaitTimeout:0.#}s — declining presentation of '{name}'.", this);
				throw new TutorialStepDeclinedException($"Step '{name}': target '{(TargetId != null ? TargetId.name : "null")}' not registered.");
			}

			bool autoClose = CloseTime > 0f;
			var tooltip = context.UiSystem.Show<UIViewTooltip>(ShowOptions.Variant(TooltipId, new UIViewTooltipContext(Title, Description, target, Position)
			{
				Icon = Icon,
				Offset = Offset,
				TapAnywhereToClose = !autoClose,
				CloseTime = autoClose ? CloseTime : 0f
			}));

			if (tooltip == null)
			{
				Debug.LogWarning($"[TooltipStep] '{name}' could not show its tooltip — declining; the next checkpoint retries.", this);
				throw new TutorialStepDeclinedException($"Step '{name}': the tooltip could not be shown.");
			}

			// The tooltip doesn't hold input - open the gate so the player can act on
			// what it points at, or tap it away when it doesn't close by itself.
			context.InputHold.Release();

			try
			{
				if (autoClose) await WaitAsync(CloseTime, ct);
				else await WaitUntilClosedAsync(tooltip, ct);
			}
			finally
			{
				// Cancellation (view destroyed mid-step) must not leak the tooltip.
				if (tooltip != null) tooltip.Close();
			}
		}
	}
}