using AK.Core;
using UnityEngine;

namespace AK.Utilities.Audio
{
	/// <summary>
	/// Plays and spawns pooled audio. The primary API takes the AudioConfig asset — call sites
	/// almost always hold one. The identity overloads exist for data that carries a
	/// <see cref="Uid{T}"/> instead of a reference (bundled content, server payloads).
	/// </summary>
	public interface IAudioSpawner
	{
		/// <summary>
		/// Plays the config's audio. Use this for nearly everything. Returns the voice, or null
		/// when nothing plays: the config's replay gap turned it away, or it has nothing to play.
		/// The voice goes back to its pool when it ends; keep no reference to it past then.
		/// </summary>
		AudioComponent PlayAudio(AudioConfig config, Vector3? position = null);

		/// <summary>Plays by identity, resolved through the registry. Logs and returns null when unknown.</summary>
		AudioComponent PlayAudio(Uid<AudioConfig> configId, Vector3? position = null);

		/// <summary>
		/// Type-safe spawn without playing: returns a T from the pool bound to the config for T
		/// (the variant, or T's default config when the variant is None). Use when you need
		/// the component before it plays. The voice is tracked like a playing one, and passes its
		/// gates when it plays. Stop it to give it back unplayed.
		/// </summary>
		T Spawn<T>(Uid<AudioConfig> variant = default) where T : AudioComponent;

		/// <summary>True while a voice of this config plays, its start delay and fade-out included.</summary>
		bool IsPlaying(AudioConfig config);

		/// <summary>Stops (fade-out) every voice handed out, or only the given config's.</summary>
		void StopAll(AudioConfig config = null);

		/// <summary>Crossfades to a music track (one at a time). Replaying the current track is a no-op.</summary>
		void PlayMusic(AudioConfig config, float crossfadeSeconds = 1f);

		/// <summary>Fades out and stops the music, a track still fading out included.</summary>
		void StopMusic(float fadeOutSeconds = 1f);

		/// <summary>The music playing, or null: null after StopMusic, and once a track that doesn't loop has played out.</summary>
		AudioConfig CurrentMusic { get; }
	}
}
