using System;
using UnityEngine;

namespace AK.Systems
{
	public interface IGameCamera
	{
		CameraRole Role       { get; }
		int        LayerOrder { get; }
		Camera     Camera     { get; }
		GameObject GameObject { get; }

		/// <summary>
		/// If this is an Overlay camera, which Base Camera type does it belong to?
		/// Returns null if it has no specific parent or is a Base itself.
		/// </summary>
		Type DefaultBaseCameraType { get; }
        
		void Enable(bool enableGameObject = true);
		void Disable(bool disableGameObject = true);
		void Shake(float intensity, float duration);
	}
}