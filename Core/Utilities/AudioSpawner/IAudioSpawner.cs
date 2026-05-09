using System;
using AK.Core;
using UnityEngine;

namespace Utilities.AudioSpawner
{
	public interface IAudioSpawner
	{
		/// <summary>
		/// Type-safe spawn: Returns exact type T if config exists for T.
		/// Returns null if no config found for type T (strict type matching).
		/// Use this when you need to manipulate the component or have custom logic.
		/// </summary>
		T Spawn<T>(UID variantId = null) where T : AudioComponent;

		/// <summary>
		/// Simple audio playback: Plays audio by UID regardless of type.
		/// Uses whatever AudioComponent type the config specifies.
		/// Use this for 99% of cases when you just want to play audio.
		/// </summary>
		AudioComponent PlayAudio(UID variantId, Vector3? position = null);

		/// <summary>
		/// Legacy method: Spawns and plays audio.
		/// OBSOLETE: Use PlayAudio(uid, position) for simple playback.
		/// </summary>
		[Obsolete("Use PlayAudio(uid, position) for simple playback. Use Spawn<T>() only when you need the component reference.")]
		AudioComponent SpawnAndPlay<T>(UID variantId = null, Vector3? position = null) where T : AudioComponent;

		/// <summary>
		/// Legacy method: Spawns audio by type.
		/// OBSOLETE: Use Spawn<T>() for type-safe spawning.
		/// </summary>
		[Obsolete("Use Spawn<T>() for type-safe spawning.")]
		AudioComponent Spawn(Type type, UID variantId);
	}
}