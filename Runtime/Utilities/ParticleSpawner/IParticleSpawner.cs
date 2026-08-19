using System;
using Cysharp.Threading.Tasks;
using AK.Core;
using UnityEngine;

namespace Utilities.ParticleSpawner
{
	public interface IParticleSpawner
	{
		public T          Spawn<T>(UID variantId = null, Action onStop = null) where T : ParticleComponent;
		public UniTask<T> SpawnAsync<T>(UID variantId = null, Action onStop = null) where T : ParticleComponent;

		/// <summary>
		/// Config-driven one-call play: spawn and show in a single step, no generic,
		/// no UID extraction. The particle equivalent of PlayAudio(config).
		/// </summary>
		public ParticleComponent Play(ParticleConfigBase config, Vector3 position,
		                              Quaternion? rotation = null, Color? color = null, Action onStop = null);

		/// <summary>Stops all active effects, or only the given config's.</summary>
		public void StopAll(ParticleConfigBase config = null);
	}
}
