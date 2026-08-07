using System.Threading;
using AK.CoreDomain.Facts;
using AK.Systems;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// Spotlight+tooltip step that completes when a fact is recorded — e.g. GoPressed,
	/// AvatarCustomizationEntered — instead of a dim-tap. The spotlight hole lets the
	/// player interact with the real control; the interaction records the fact,
	/// completes the step, and the base flow closes tooltip and spotlight. Steps
	/// differ only by which fact asset is linked, not by class.
	/// Direct FactType reference is deliberate: fact types are pure-identity
	/// (stateless) UID assets, so bundle duplication is behaviorally transparent.
	/// </summary>
	[CreateAssetMenu(fileName = "SpotLightTooltipAdvanceOnStep", menuName = "AK/Tutorials/SpotLightTooltipAdvanceOnStep")]
	public class SpotLightTooltipAdvanceOnStep : SpotlightTooltipStep
	{
		[SerializeField, Tooltip("Advance this step when this fact is recorded. The step never records it — instrumentation does.")]
		private FactType _advanceOn;

		protected override async UniTask WaitForAdvanceAsync(TutorialStepContext context, UIViewSpotlight spotlight, CancellationToken ct)
		{
			if (_advanceOn == null)
			{
				Debug.LogWarning($"[SpotLightTooltipAdvanceOnStep] '{name}' has no advance fact assigned — falling back to dim-tap.", this);
				await base.WaitForAdvanceAsync(context, spotlight, ct);
				return;
			}

			Debug.Log($"[SpotLightTooltipAdvanceOnStep] '{name}' waiting on fact '{_advanceOn.name}' (GUID: {_advanceOn.Id}).", this);

			await context.Facts.WaitForCountAsync(_advanceOn.Id, 1, ct);
		}
	}
}
