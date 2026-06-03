using AK.Systems;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AK.Systems.UI._Source.UISystem.Common
{
	public class UIViewToast : UIView
	{
		[SerializeField] private Image           FloaterBg;
		[SerializeField] private TextMeshProUGUI FloaterText;
		[SerializeField] private RectTransform   ContainerPanel;

		private Vector2  _initialPosition;
		private int      _moveDirection = 1;
		private string   _textToDisplay;
		private Sequence _floaterMoveSequence;
		private Sequence _floaterHideSequence;

		public void Init(float spawnPositionY, string textToDisplay)
		{
			_moveDirection = 1;
			_textToDisplay = textToDisplay;
			_initialPosition = new Vector2(0, spawnPositionY);
			StartDisplaySequence();
		}

		private void StartDisplaySequence()
		{
			_floaterMoveSequence?.Kill();
			_floaterMoveSequence = DOTween.Sequence();

			ContainerPanel.gameObject.SetActive(true);
			FloaterText.text = _textToDisplay;
			ContainerPanel.anchoredPosition = _initialPosition;

			_floaterMoveSequence
				.Append(ContainerPanel.DOAnchorPosY(_initialPosition.y + (_moveDirection * UIConstants.TOAST_FLOAT_DISTANCE), UIConstants.TOAST_FLOAT_DURATION).SetEase(Ease.OutSine)
				                      .OnKill(Hide))
				.Join(FloaterBg.DOFade(UIConstants.FULL_ALPHA, UIConstants.ZERO_ALPHA))
				.Join(FloaterText.DOFade(UIConstants.FULL_ALPHA, UIConstants.ZERO_ALPHA));
			_floaterMoveSequence.Play();
		}

		private void Hide()
		{
			_floaterHideSequence?.Kill();
			_floaterHideSequence = DOTween.Sequence();
			_floaterHideSequence.Append(FloaterBg.DOFade(UIConstants.ZERO_ALPHA, UIConstants.TOAST_FADE_OUT_DURATION))
			                    .Join(FloaterText.DOFade(UIConstants.ZERO_ALPHA, UIConstants.TOAST_FADE_OUT_DURATION).OnKill(() => Close()));
			_floaterHideSequence.Play();
		}

		protected override void OnDestroy()
		{
			_floaterMoveSequence?.Kill();
			_floaterMoveSequence = null;

			_floaterHideSequence?.Kill();
			_floaterHideSequence = null;
		}
	}
}