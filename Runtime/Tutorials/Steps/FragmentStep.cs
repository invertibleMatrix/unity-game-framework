using System.Threading;
using AK.Core;
using AK.Systems;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// Presents a step by showing a fragment view (e.g. a choreographed tutorial view)
	/// and awaiting its ITutorialStepView finish contract. The step references the view
	/// by type only — UISystem resolves a registered static child first and falls back
	/// to the view repository prefab, so fragments living inside another view need no
	/// prefab of their own.
	/// </summary>
	[CreateAssetMenu(fileName = "FragmentStep", menuName = "AK/Tutorials/Fragment Step")]
	public class FragmentStep : TutorialStep
	{
		[SerializeField, DerivedFrom(typeof(UIView)), Tooltip("Type of the fragment view to show.")]
		protected TypeRef FragmentType = new();

		[SerializeField, Tooltip("Variant (ViewId) of the fragment. Empty shows the default variant.")]
		protected string ViewId;

		[SerializeField, Tooltip("UITargetId whose owning view hosts the fragment. Empty shows it without a parent.")]
		protected UITargetId ParentTarget;

		[SerializeField, Tooltip("Close the fragment after it reports finish.")]
		protected bool CloseOnFinish = true;

		[SerializeField, Tooltip("Seconds to linger after the fragment reports finish before closing.")]
		protected float CloseDelay;

		public override async UniTask PresentAsync(TutorialStepContext context, CancellationToken ct)
		{
			System.Type fragmentType = FragmentType != null ? FragmentType.Value : null;
			if (fragmentType == null)
			{
				Debug.LogError($"[FragmentStep] '{name}' has no fragment type assigned.", this);
				return;
			}

			UIView parent = ResolveParent(context);

			var view = context.UiSystem.Show<UIView>(fragmentType, context: BuildContext(), parent: parent, viewId: ViewId ?? string.Empty);
			if (view == null)
			{
				Debug.LogWarning($"[FragmentStep] Failed to show fragment of type '{fragmentType.Name}'.");
				return;
			}

			if (view is ITutorialStepView stepView)
			{
				await stepView.WaitUntilFinish().AttachExternalCancellation(ct);
			}
			else
			{
				Debug.LogWarning($"[FragmentStep] '{view.name}' does not implement ITutorialStepView — the step completes immediately.");
			}

			if (CloseOnFinish && view != null)
			{
				if (CloseDelay > 0f)
				{
					await UniTask.WaitForSeconds(CloseDelay, cancellationToken: ct);
				}

				view.Close();
			}
		}

		// Game subclasses override to pass a typed UIContext to the fragment.
		protected virtual UIContext BuildContext()
		{
			return null;
		}

		private UIView ResolveParent(TutorialStepContext context)
		{
			if (ParentTarget == null) return null;

			if (context.Targets.TryGet(ParentTarget, out var parentTarget) && parentTarget != null)
			{
				return parentTarget.GetComponentInParent<UIView>();
			}

			Debug.LogWarning($"[FragmentStep] Parent target '{ParentTarget.name}' is not registered — showing without a parent.");
			return null;
		}
	}
}
