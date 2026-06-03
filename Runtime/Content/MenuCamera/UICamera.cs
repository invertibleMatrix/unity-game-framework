using System;
using AK.Systems;
using Gameplay.Cameras;

namespace UI.Camera
{
    public partial class UICamera : BaseCamera<UICamera, UICamera.Main>
    {
        public override Type DefaultBaseCameraType => typeof(MainCamera);
    }
}