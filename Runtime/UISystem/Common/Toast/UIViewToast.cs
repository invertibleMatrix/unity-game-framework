using AK.Systems;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AK.Systems.UI
{
	public class UIViewToast : UIView
	{
		[SerializeField] private Image           FloaterBg;
		[SerializeField] private TextMeshProUGUI FloaterText;
		[SerializeField] private RectTransform   ContainerPanel;

		[SerializeField] private float _floatDistance   = 120f;
		[SerializeField] private float _duration        = 2f;
		[SerializeField] private float _fadeOutDuration = 0.5f;
		
		private Vector2  _initialPosition;
		private int      _moveDirection = 1;
		private string   _textToDisplay;
		private Sequence _floaterMoveSequence;
		private Sequence _floaterHideSequence;
		private bool     _isDestroying;

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
				.Append(ContainerPanel.DOAnchorPosY(_initialPosition.y + (_moveDirection * _floatDistance), _duration).SetEase(Ease.OutSine)
				                      .OnComplete(Hide))
				.Join(FloaterBg.DOFade(1f, 0f))
				.Join(FloaterText.DOFade(1f, 0f));
			_floaterMoveSequence.Play();
		}

		private void Hide()
		{
			if (_isDestroying) return;

			_floaterHideSequence?.Kill();
			_floaterHideSequence = DOTween.Sequence();
			_floaterHideSequence.Append(FloaterBg.DOFade(0f, _fadeOutDuration))
			                    .Join(FloaterText.DOFade(0f, _fadeOutDuration).OnComplete(() => Close()));
			_floaterHideSequence.Play();
		}

		/// <summary>
		/// The floater sequences target ContainerPanel/FloaterBg/FloaterText - none of which are
		/// the view's own animation targets, so the base cleanup never kills them. A surviving
		/// sequence on a pooled toast would later complete and call Close() on an unregistered
		/// view. Kill them here (mirrors what UIViewBanner already does).
		/// </summary>
		public override void UnRegisterResources()
		{
			_floaterMoveSequence?.Kill();
			_floaterMoveSequence = null;

			_floaterHideSequence?.Kill();
			_floaterHideSequence = null;

			base.UnRegisterResources();
		}

		protected override void OnDestroy()
		{
			_isDestroying = true;

			_floaterMoveSequence?.Kill();
			_floaterMoveSequence = null;

			_floaterHideSequence?.Kill();
			_floaterHideSequence = null;

			base.OnDestroy();
		}
	}
}