namespace Utilities.AudioSpawner
{
    public interface IAudioMixerController
    {
        void SetVolume(string parameterName, float linearValue);
        void TransitionToSnapshot(MixerSnapshot snapshot, float transitionTime);
    }
}