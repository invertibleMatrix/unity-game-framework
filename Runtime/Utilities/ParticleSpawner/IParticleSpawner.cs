using System;
using Cysharp.Threading.Tasks;
using AK.Core;
using UnityEngine;

namespace Utilities.ParticleSpawner
{
	public interface IParticleSpawner
	{
		/// <summary>
		/// Type-safe spawn: a T from the pool bound to the config for T (the variant, or T's
		/// default config when None). Use when you need the component before it plays.
		/// </summary>
		T          Spawn<T>(Uid<ParticleConfigBase> variant = default, Action onStop = null) where T : ParticleComponent;
		UniTask<T> SpawnAsync<T>(Uid<ParticleConfigBase> variant = default, Action onStop = null) where T : ParticleComponent;

		/// <summary>Spawn from a held config reference. The common case for game code.</summary>
		ParticleComponent Spawn(ParticleConfigBase config, Action onStop = null);

		/// <summary>Spawn from a held config reference, returning the component as T (null if the prefab isn't a T).</summary>
		T Spawn<T>(ParticleConfigBase config, Action onStop = null) where T : ParticleComponent;

		/// <summary>
		/// Config-driven one-call play: spawn and show in a single step.
		/// </summary>
		ParticleComponent Play(ParticleConfigBase config, Vector3 position,
		                       Quaternion? rotation = null, Color? color = null, Action onStop = null);

		/// <summary>Stops all active effects, or only the given config's.</summary>
		void StopAll(ParticleConfigBase config = null);
	}
}
