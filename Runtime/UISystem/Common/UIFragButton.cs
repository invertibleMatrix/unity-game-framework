using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AK.Systems
{
	public class UIFragButton : UIView
	{
		[SerializeField] protected Animator        _animator;
		[SerializeField] protected Button          _button;
		[SerializeField] protected TextMeshProUGUI _text;

		public void AddListener(UnityAction listener)
		{
			_button.onClick.AddListener(listener);
		}

		public void RemoveListener(UnityAction listener)
		{
			_button.onClick.RemoveListener(listener);
		}

		public bool TrySetText(string text)
		{
			if (_text == null)
			{
				return false;
			}

			_text.text = text;
			return true;
		}

		public override void OnShow()
		{
			if (_animator != null) _animator.enabled = true;
		}

		public override void OnHide()
		{
			if (_animator != null) _animator.enabled = false;
		}
	}
}