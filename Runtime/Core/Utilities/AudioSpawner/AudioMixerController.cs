using AK.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Utilities.AudioSpawner
{
    public enum MixerSnapshot
    {
        Gameplay,
        Paused
    }

    public class AudioMixerController : GameEntity, IAudioMixerController
    {
        [Header("Mixer & Snapshots")]
        [SerializeField] private AudioMixer _mainMixer;
        [SerializeField] private AudioMixerSnapshot _gameplaySnapshot;
        [SerializeField] private AudioMixerSnapshot _mainMenuSnapshot;
        [SerializeField] private AudioMixerSnapshot _pausedSnapshot;

        public void SetVolume(string parameterName, float linearValue)
        {
            // Convert linear value (0-1) to logarithmic decibels (-80 to 0).
            // A value of 0.0001f is used to avoid taking the log of zero.
            float dbValue = Mathf.Log10(Mathf.Max(linearValue, 0.0001f)) * 20;
            _mainMixer.SetFloat(parameterName, dbValue);
        }

        public void TransitionToSnapshot(MixerSnapshot snapshot, float transitionTime)
        {
            AudioMixerSnapshot targetSnapshot = snapshot switch
            {
                MixerSnapshot.Gameplay => _gameplaySnapshot,
                MixerSnapshot.Paused   => _pausedSnapshot,
                _                      => _gameplaySnapshot
            };

            targetSnapshot.TransitionTo(transitionTime);
        }
    }
}