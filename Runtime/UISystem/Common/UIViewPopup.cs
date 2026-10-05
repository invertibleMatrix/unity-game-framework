using AK.Core.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace AK.Systems
{
	/// <summary>
	/// A popup with a close button and an optional Animator that starts once it is shown. The
	/// context is typed like any <see cref="UIView{TContext}"/>.
	/// </summary>
	public abstract class UIViewPopup<TContext> : UIView<TContext> where TContext : UIContext, new()
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
			if (_animator == null) return;

			_animator.updateMode = TimeDomain.ToAnimatorUpdateMode();
			_animator.enabled = true;
		}

		public virtual void OnCloseButtonPressed()
		{
			Close();
		}
	}

	/// <summary>A popup that takes no data of its own.</summary>
	public abstract class UIViewPopup : UIViewPopup<UIContext> { }
}