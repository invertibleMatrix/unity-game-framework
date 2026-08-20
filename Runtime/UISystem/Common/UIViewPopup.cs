using UnityEngine;
using UnityEngine.UI;

namespace AK.Systems
{
	public abstract class UIViewPopup<TContext> : UIView where TContext : UIContext, new()
	{
		[SerializeField] protected Animator _animator;
		[SerializeField] protected Button   _closeButton;

		public override void OnPrepareShow()
		{
			if (_animator != null) _animator.enabled = false;
		}

		public override void RegisterResources()
		{
			if (_closeButton != null)
			{
				_closeButton.onClick.AddListener(OnCloseButtonPressed);
			}
		}

		public override void UnRegisterResources()
		{
			if (_closeButton != null)
			{
				_closeButton.onClick.RemoveListener(OnCloseButtonPressed);
			}
		}

		public override void OnShow()
		{
			if (_animator != null) _animator.enabled = true;
		}

		public virtual void OnCloseButtonPressed()
		{
			Close();
		}
	}

	public abstract class UIViewPopup : UIViewPopup<UIContext> { }
}