using AK.Core;
using UnityEngine;

namespace Utilities.AudioSpawner
{
	/// <summary>
	/// Plays and spawns pooled audio. The primary API takes the AudioConfig asset — call sites
	/// almost always hold one. The identity overloads exist for data that carries a
	/// <see cref="Uid{T}"/> instead of a reference (bundled content, server payloads).
	/// </summary>
	public interface IAudioSpawner
	{
		/// <summary>Plays the config's audio. Use this for nearly everything.</summary>
		AudioComponent PlayAudio(AudioConfig config, Vector3? position = null);

		/// <summary>Plays by identity, resolved through the registry. Logs and returns null when unknown.</summary>
		AudioComponent PlayAudio(Uid<AudioConfig> configId, Vector3? position = null);

		/// <summary>
		/// Type-safe spawn without playing: returns a T from the pool bound to the config for T
		/// (the variant, or T's default config when the variant is None). Use when you need
		/// the component before it plays.
		/// </summary>
		T Spawn<T>(Uid<AudioConfig> variant = default) where T : AudioComponent;

		/// <summary>True while at least one voice of this config is active.</summary>
		bool IsPlaying(AudioConfig config);

		/// <summary>Stops (fade-out) all active voices, or only the given config's.</summary>
		void StopAll(AudioConfig config = null);

		/// <summary>Crossfades to a music track (one at a time). Replaying the current track is a no-op.</summary>
		void PlayMusic(AudioConfig config, float crossfadeSeconds = 1f);

		/// <summary>Fades out and stops the current music track.</summary>
		void StopMusic(float fadeOutSeconds = 1f);

		/// <summary>The currently playing music config, or null.</summary>
		AudioConfig CurrentMusic { get; }
	}
}
