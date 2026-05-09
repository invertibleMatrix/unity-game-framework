using System;
using Cysharp.Threading.Tasks;
using AK.Core;

namespace Utilities.ParticleSpawner
{
	public interface IParticleSpawner
	{
		public T          Spawn<T>(UID variantId = null, Action onStop = null) where T : ParticleComponent;
		public UniTask<T> SpawnAsync<T>(UID variantId = null, Action onStop = null) where T : ParticleComponent;
	}
}