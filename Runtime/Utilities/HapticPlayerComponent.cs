using System;
using Reflex.Extensions;
using UnityEngine;

namespace AK.Utilities
{
	public class HapticPlayerComponent : MonoBehaviour, IHapticsPlayer
	{
		private IHapticsPlayer _hapticsPlayer;

		private void Awake()
		{
			_hapticsPlayer = gameObject.scene.GetSceneContainer().Resolve<IHapticsPlayer>();
		}

		public void PlayLevelSuccessHaptic()
		{
			_hapticsPlayer.PlayLevelSuccessHaptic();
		}

		public void PlayLightImpactHaptic()
		{
			_hapticsPlayer.PlayLightImpactHaptic();
		}

		public void PlayHeavyImpactHaptic()
		{
			_hapticsPlayer.PlayHeavyImpactHaptic();
		}

		public void PlaySoftImpactHaptic()
		{
			_hapticsPlayer.PlaySoftImpactHaptic();
		}

		public void PlayMediumImpactHaptic()
		{
			_hapticsPlayer.PlayMediumImpactHaptic();
		}

		public void PlayLevelFailHaptic()
		{
			_hapticsPlayer.PlayLevelFailHaptic();
		}

		public void PlayWarningHaptic()
		{
			_hapticsPlayer.PlayWarningHaptic();
		}

		public void PlaySelectionHaptic()
		{
			_hapticsPlayer.PlaySelectionHaptic();
		}
	}
}