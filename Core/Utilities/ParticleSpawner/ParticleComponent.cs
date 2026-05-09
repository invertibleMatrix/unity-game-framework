using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using AK.Core;
using UnityEngine;

namespace Utilities.ParticleSpawner
{
	public class ParticleComponent : GameEntity
	{
		[SerializeField] protected ParticleSystem       _rootParticle;
		[SerializeField] protected List<ParticleSystem> _colorTargets;

		private ParticleConfigBase _configBase;
		private Action             _onStop;

		public ParticleSystem RootParticle    => _rootParticle;
		public UID            ConfigVariantId { get; private set; }

		private Transform _parent;

		private void Awake()
		{
			_parent = transform.parent;
		}

		public virtual void Init(ParticleConfigBase configBase, Action onStop)
		{
			_configBase = configBase;
			ConfigVariantId = configBase.VariantId;
			_onStop = onStop;
			if (_rootParticle != null)
			{
				var main = _rootParticle.main;
				main.stopAction = ParticleSystemStopAction.Callback;
			}
		}

		public void ResetParent()
		{
			transform.parent = _parent;
		}

		public void Stop()
		{
			if (_rootParticle == null)
			{
				_rootParticle = GetComponent<ParticleSystem>();
			}
			_rootParticle.Stop(true);
		}

		protected void OnParticleSystemStopped()
		{
			OnStop();
		}

		public virtual void Show()
		{
			Activate().Forget();
		}

		public virtual void Show(Vector3 position)
		{
			transform.position = position;
			Show();
		}

		public virtual void Show(Vector3 position, Quaternion rotation)
		{
			transform.rotation = rotation;
			Show(position);
		}

		public virtual void Show(Vector3 position, Quaternion rotation, Color color)
		{
			Show(position, rotation);
			for (int i = 0; i < _colorTargets.Count; i++)
			{
				var mainModule = _colorTargets[i].main;
				mainModule.startColor = color;
			}
		}

		protected virtual void OnStop()
		{
			_onStop?.Invoke();
		}

		private async UniTask Activate()
		{
			await UniTask.Delay((int)(_configBase.StartDelayInSeconds * 1000f));
			gameObject.SetActive(true);
			_rootParticle.Play(true);

			if (_rootParticle != null)
			{
				if (_rootParticle.main.loop && _configBase.StopAfterSeconds > 0)
				{
					await UniTask.Delay((int)(_configBase.StopAfterSeconds * 1000f));
					gameObject.SetActive(false);
				}
			}
		}
	}
}