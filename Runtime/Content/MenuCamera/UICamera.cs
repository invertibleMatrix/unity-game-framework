using System;
using AK.CameraSystem;
using Gameplay.Cameras;

namespace UI.Camera
{
    public partial class UICamera : BaseCamera<UICamera, UICamera.Main>
    {
        public override Type DefaultBaseCameraType => typeof(MainCamera);
    }
}