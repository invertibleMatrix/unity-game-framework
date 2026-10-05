namespace AK.Utilities.Audio
{
	public enum AudioChannel
	{
		Master,
		Music,
		Sfx
	}

	public interface IAudioMixerController
	{
		/// <summary>Sets a channel's volume (linear 0..1, converted to dB internally) and persists it.</summary>
		void SetVolume(AudioChannel channel, float linearValue);

		/// <summary>Current persisted linear volume (0..1) for a channel.</summary>
		float GetVolume(AudioChannel channel);

		/// <summary>Transitions to the given snapshot asset; negative time uses the snapshot's default.</summary>
		void TransitionToSnapshot(AudioSnapshot snapshot, float transitionTime = -1f);

		/// <summary>
		/// Lowers the music channel by duckDb for durationSeconds of the controller's time, then
		/// restores it. The latest duck wins, and a music volume set meanwhile keeps it.
		/// </summary>
		void DuckMusic(float duckDb, float durationSeconds);
	}
}
