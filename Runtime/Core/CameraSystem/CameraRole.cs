namespace AK.CameraSystem
{
	public enum CameraRole
	{
		Base, // Renders to Screen or Render Texture (Clears depth/color)
		Overlay // Stacks on top of a Base
	}
}