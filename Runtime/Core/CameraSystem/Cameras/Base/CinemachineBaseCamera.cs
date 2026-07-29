using AK.StateMachine;
using Unity.Cinemachine;
using UnityEngine;
using AK.Core;

namespace AK.Systems
{
	[RequireComponent(typeof(CinemachineBrain))]
	public abstract class CinemachineBaseCamera<TEntity, TState> : BaseCamera<TEntity, TState>, ICinemachineGameCamera 
		where TEntity : GameEntity
		where TState : BaseState<TEntity>, new()
	{
		protected CinemachineBrain _cinemachineBrain;
		private CinemachineImpulseSource _impulseSource;

		public virtual CinemachineCamera ActiveVirtualCam => _cinemachineBrain.ActiveVirtualCamera as CinemachineCamera;

		/// <inheritdoc />
		public CinemachineBrain Brain => _cinemachineBrain;

		protected override void Awake()
		{
			base.Awake();
			_cinemachineBrain = GetComponent<CinemachineBrain>();
			_impulseSource = GetComponent<CinemachineImpulseSource>();
		}
		
		public override void Shake(float intensity, float duration)
		{
			if (_impulseSource != null)
			{
				_impulseSource.GenerateImpulseWithVelocity(Vector3.one * intensity);
			}
			else
			{
				base.Shake(intensity, duration);
			}
		}
	}
}