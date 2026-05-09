using AK.UISystem;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace AK.UISystem
{
	public class UIViewBanner : UIView
	{
		[SerializeField] private UnityEvent _onShow;
		[SerializeField] private UnityEvent _onHide;

		[SerializeField] private TextMeshProUGUI _text;
		[SerializeField] private Animator        _animator;

		private float _duration;
		private Tween _hideTween;

		public const string DEFAULT_ID     = UIConstants.DEFAULT_BANNER_ID;
		public const string DEFAULT_TOP_ID = UIConstants.DEFAULT_TOP_BANNER_ID;
		public const string AFFIRMATION_ID = UIConstants.AFFIRMATION_BANNER_ID;

		public void Init(string text, float duration = UIConstants.DEFAULT_BANNER_DURATION)
		{
			_text.text = text;
			_duration = duration;
		}

		public override void OnPrepareShow()
		{
			_onShow?.Invoke();
		}

		public override void OnPrepareHide()
		{
			_onHide?.Invoke();
		}

		public override void OnShow()
		{
			if (_animator != null)
			{
				_animator.enabled = true;
			}

			_hideTween?.Kill();
			
			if (_duration > 0)
			{
				_hideTween = DOVirtual.DelayedCall(_duration, () => { Close(); }).Play();
			}
		}

		public override void OnHide()
		{
			_hideTween?.Kill();
		}

		public override void UnRegisterResources()
		{
			_hideTween?.Kill();
		}
		
		public override void OnReset()
		{
			base.OnReset();
			_text.text = "";
			_duration = UIConstants.DEFAULT_BANNER_DURATION;

			if (_animator != null)
			{
				_animator.enabled = false;
			}
		}
	}
}