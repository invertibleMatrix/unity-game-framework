namespace AK.Systems
{
	public enum CameraRole
	{
		Base, // Renders to Screen or Render Texture (Clears depth/color)
		Overlay, // Stacks on top of a Base
		Virtual // Cinemachine virtual camera - no Unity Camera of its own; a single CinemachineBrain renders it, priority decides which is live (disabling one smoothly blends to the next)
	}
}