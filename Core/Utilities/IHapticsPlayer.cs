namespace AK.Utilities
{
	public interface IHapticsPlayer
	{
		public void PlayLevelSuccessHaptic();
		public void PlayLightImpactHaptic();
		public void PlayHeavyImpactHaptic();
		public void PlaySoftImpactHaptic();
		public void PlayMediumImpactHaptic();
		public void PlayLevelFailHaptic();
		public void PlayWarningHaptic();
		public void PlaySelectionHaptic();
	}
}