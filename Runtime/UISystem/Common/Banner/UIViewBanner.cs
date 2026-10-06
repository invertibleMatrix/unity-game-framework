using AK.Core.Extensions;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace AK.Systems
{
	public class UIViewBanner : UIView
	{
		[SerializeField] private UnityEvent _onShow;
		[SerializeField] private UnityEvent _onHide;

		[SerializeField] private TextMeshProUGUI _text;
		[SerializeField] private Animator        _animator;

		private float _duration = DEFAULT_BANNER_DURATION;
		private Tween _hideTween;

		public const string DEFAULT_ID     = "banner1";
		public const string DEFAULT_TOP_ID = "banner2";

		public const float DEFAULT_BANNER_DURATION = 2f;

		/// <summary>
		/// Sets the text and how long the banner stays up once shown. A duration of zero or less
		/// keeps it up until it is closed. A banner shown without Init stays up for
		/// <see cref="DEFAULT_BANNER_DURATION"/>.
		/// </summary>
		public void Init(string text, float duration = DEFAULT_BANNER_DURATION)
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
				_animator.updateMode = TimeDomain.ToAnimatorUpdateMode();
				_animator.enabled = true;
			}

			_hideTween?.Kill();
			
			if (_duration > 0)
			{
				_hideTween = DOVirtual.DelayedCall(_duration, () => { Close(); }).SetTimeDomain(TimeDomain).Play();
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
			_duration = DEFAULT_BANNER_DURATION;

			if (_animator != null)
			{
				_animator.enabled = false;
			}
		}
	}
}