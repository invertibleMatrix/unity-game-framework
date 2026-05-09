using System;
using DG.Tweening;
using UnityEngine;

namespace AK.Utilities
{
	public class PingPongRotator : MonoBehaviour
	{
		public Vector3 RotationStart;
		public Vector3 RotationEnd;
		public float   Time;

		private Tween _tween;

		void OnEnable()
		{
			transform.localEulerAngles = RotationStart;
			_tween = transform.DOLocalRotate(RotationEnd, Time).SetEase(Ease.Linear).SetLoops(-1, LoopType.Yoyo).Play();
		}

		private void OnDisable()
		{
			_tween.Kill();
		}
	}
}