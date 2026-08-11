using AK.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Utilities.AudioSpawner
{
	/// <summary>
	/// A mixer snapshot as a joint asset: identity plus the AudioMixerSnapshot reference,
	/// so code references this asset (filtered picker, Find References) instead of
	/// magic enum values or strings.
	/// </summary>
	[CreateAssetMenu(fileName = "AudioSnapshot", menuName = "AK/Audio/Audio Snapshot")]
	public class AudioSnapshot : MetaDataAsset
	{
		public AudioMixerSnapshot Snapshot;

		[Tooltip("Default transition seconds when the caller doesn't specify one.")]
		public float DefaultTransitionTime = 0.5f;
	}
}
