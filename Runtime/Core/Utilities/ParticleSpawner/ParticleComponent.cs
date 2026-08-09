using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using AK.Core;
using UnityEngine;

namespace Utilities.ParticleSpawner
{
	public class ParticleComponent : GameEntity
	{
		[SerializeField] protected ParticleSystem       _rootParticle;
		[SerializeField] protected List<ParticleSystem> _colorTargets;

		private ParticleConfigBase      _configBase;
		private Action                  _onStop;
		private Transform               _parent;
		private CancellationTokenSource _playCts;

		// Set while this instance is owned by the world (not the pool). Makes the
		// recycle path idempotent — the stop callback and a direct Stop() can race.
		private bool _active;

		public ParticleSystem RootParticle    => _rootParticle;
		public UID            ConfigVariantId { get; private set; }

		private void Awake()
		{
			_parent = transform.parent;
		}

		public virtual void Init(ParticleConfigBase configBase, Action onStop)
		{
			CancelPlay();

			_configBase = configBase;
			ConfigVariantId = configBase.VariantId;
			_onStop = onStop;
			_active = false;

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
			CancelPlay();

			if (_rootParticle == null)
			{
				_rootParticle = GetComponent<ParticleSystem>();
			}

			if (_rootParticle.isPlaying)
			{
				// The stopAction callback recycles via OnParticleSystemStopped.
				_rootParticle.Stop(true);
			}
			else
			{
				// Never started (e.g. stopped during the start delay) — no callback
				// will fire, so recycle directly.
				Recycle();
			}
		}

		protected void OnParticleSystemStopped()
		{
			Recycle();
		}

		public virtual void Show()
		{
			_active = true;

			CancelPlay();
			_playCts = new CancellationTokenSource();
			Activate(_playCts.Token).Forget();
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
			for (int i = 0; i < _colorTargets.Count; i++)
			{
				var mainModule = _colorTargets[i].main;
				mainModule.startColor = color;
			}

			Show(position, rotation);
		}

		protected virtual void OnStop()
		{
			_onStop?.Invoke();
		}

		private void Recycle()
		{
			if (!_active) return;

			// Root stopped but children (trails, fading smoke) can still be alive —
			// recycle only when the whole hierarchy is truly dead, else pop.
			if (_configBase != null && _configBase.WaitForChildrenToFinish &&
			    _rootParticle != null && _rootParticle.IsAlive(true))
			{
				RecycleWhenFullyDeadAsync().Forget();
				return;
			}

			FinalizeRecycle();
		}

		private void FinalizeRecycle()
		{
			if (!_active) return;
			_active = false;
			OnStop();
		}

		private async UniTaskVoid RecycleWhenFullyDeadAsync()
		{
			float waited = 0f;

			while (_rootParticle != null && _rootParticle.IsAlive(true) && waited < 10f)
			{
				bool cancelled = await UniTask.Delay(250, DelayType.DeltaTime, PlayerLoopTiming.Update,
				                                     this.GetCancellationTokenOnDestroy())
				                              .SuppressCancellationThrow();
				if (cancelled) return;

				waited += 0.25f;
			}

			FinalizeRecycle();
		}

		private async UniTaskVoid Activate(CancellationToken ct)
		{
			try
			{
				if (_configBase.StartDelayInSeconds > 0)
				{
					await UniTask.Delay(TimeSpan.FromSeconds(_configBase.StartDelayInSeconds),
					                    DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
				}

				gameObject.SetActive(true);
				_rootParticle.Play(true);

				if (_rootParticle.main.loop && _configBase.StopAfterSeconds > 0)
				{
					await UniTask.Delay(TimeSpan.FromSeconds(_configBase.StopAfterSeconds),
					                    DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
					gameObject.SetActive(false);
				}
			}
			catch (OperationCanceledException)
			{
				// Stopped or recycled during the delay — the cancelling path owns recycling.
			}
		}

		private void CancelPlay()
		{
			_playCts?.Cancel();
			_playCts?.Dispose();
			_playCts = null;
		}

		private void OnDisable()
		{
			CancelPlay();
		}

		private void OnDestroy()
		{
			CancelPlay();
		}
	}
}
