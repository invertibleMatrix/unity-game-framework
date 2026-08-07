using AK.CoreDomain.Facts;
using AK.Services.Facts;
using AK.Systems;
using Reflex.Attributes;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// Records a fact whenever the view on this GameObject completes a show.
	/// Drop it on a screen's root to ledger screen visits as facts.
	/// </summary>
	public class RecordViewShownFact : MonoBehaviour
	{
		[SerializeField] private FactType _fact;

		[Inject] private IFactService _facts;

		private void OnEnable()
		{
			UIView.Shown += OnViewShown;
		}

		private void OnDisable()
		{
			UIView.Shown -= OnViewShown;
		}

		private void OnViewShown(UIView view)
		{
			if (_facts == null || _fact == null || view == null) return;

			if (view.gameObject == gameObject)
			{
				_facts.Record(_fact);
			}
		}
	}
}
