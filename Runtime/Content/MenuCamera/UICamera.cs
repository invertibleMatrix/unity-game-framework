using AK.Systems;
using Gameplay.Cameras;

namespace UI.Camera
{
    public partial class UICamera : BaseCamera<UICamera, UICamera.Main>
    {
        // Base camera type is now assigned via the _baseCameraType serialized field in the Inspector.
    }
}