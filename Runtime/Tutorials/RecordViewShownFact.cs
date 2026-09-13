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

		private IUISystem _views;
		private bool      _subscribed;

		// Injection runs after Instantiate, so a root that is active in its prefab has already
		// been through OnEnable when the system arrives; subscribe from whichever comes last.
		[Inject]
		private void Construct(IUISystem views)
		{
			_views = views;
			if (isActiveAndEnabled) Subscribe();
		}

		private void OnEnable()
		{
			Subscribe();
		}

		private void OnDisable()
		{
			Unsubscribe();
		}

		private void Subscribe()
		{
			if (_subscribed || _views == null) return;
			_subscribed = true;
			_views.ViewShown += OnViewShown;
		}

		private void Unsubscribe()
		{
			if (!_subscribed) return;
			_subscribed = false;
			_views.ViewShown -= OnViewShown;
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
