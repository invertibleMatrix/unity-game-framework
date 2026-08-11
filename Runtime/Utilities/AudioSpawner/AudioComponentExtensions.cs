// Assets/_Source/Utilities/AudioSpawner/AudioComponentExtensions.cs
using UnityEngine;

namespace Utilities.AudioSpawner
{
	public static class AudioComponentExtensions
	{
		/// <summary>
		/// Plays audio safely, logging a warning if the component is null.
		/// Returns the component to allow chaining.
		/// </summary>
		public static AudioComponent PlaySafe(this AudioComponent audioComponent, Vector3? position = null)
		{
			if (audioComponent == null)
			{
				Debug.LogWarning("AudioComponent is null - cannot play. This usually indicates a type mismatch in Spawn<T>().");
				return null;
			}

			audioComponent.Play(position);
			return audioComponent;
		}

		/// <summary>
		/// Stops audio safely if not null.
		/// </summary>
		public static AudioComponent StopSafe(this AudioComponent audioComponent)
		{
			audioComponent?.Stop();
			return audioComponent;
		}
	}
}