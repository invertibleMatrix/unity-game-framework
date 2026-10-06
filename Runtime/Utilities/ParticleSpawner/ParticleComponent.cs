using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using AK.Core;
using AK.Core.Extensions;
using AK.Kernel.Timing;
using UnityEngine;

namespace AK.Utilities.Particles
{
	/// <summary>
	/// A pooled particle effect. Its start delay, stop-after and the wait for its children to
	/// die count the time its root ParticleSystem simulates on: unscaled when the root uses
	/// unscaled time, as UI effects do, and scaled otherwise, as gameplay effects do.
	///
	/// <para>An effect is handed out by <see cref="Init"/> and recycled once it has stopped:
	/// when its root system stops by itself, once its particles have died after
	/// <see cref="Stop"/> or its stop-after time, or at once when it is stopped before it plays.
	/// Recycling restores the prefab's pose, layers and colors. A pooled effect then goes back
	/// to its spawner and forgets its config, so keep no reference to it past then.</para>
	/// </summary>
	public class ParticleComponent : GameEntity
	{
		private const double RecyclePollSeconds = 0.25d;
		private const double MaxRecycleWaitSeconds = 10d;

		[SerializeField] protected ParticleSystem       _rootParticle;
		[SerializeField] protected List<ParticleSystem> _colorTargets;

		private ParticleConfigBase _configBase;
		private Action             _onStop;
		private Transform          _parent;

		// Prefab-authored defaults, captured once before the first spawn mutates anything.
		// ResetState restores them on recycle, so the pool never holds an instance changed by a
		// spawn (scale, tint, preview layer, re-parenting).
		private bool                               _defaultsCaptured;
		private Vector3                            _defaultLocalPosition;
		private Quaternion                         _defaultLocalRotation;
		private Vector3                            _defaultLocalScale;
		private List<KeyValuePair<Transform, int>> _defaultLayers;
		private ParticleSystem.MinMaxGradient[]    _defaultColors;

		// Out of its pool: from Init until recycled. Makes recycling idempotent, as the stop
		// callback and a direct Stop() can both recycle.
		private bool _leased;

		// Shown since it was handed out.
		private bool _shown;

		// Told to stop: its root stopped emitting, and its stop callback will recycle it.
		private bool _stopping;

		// Moves on at every hand-out, show and recycle: a wait begun for one show ends quietly
		// once another came since.
		private int _generation;

		public ParticleSystem          RootParticle => _rootParticle;
		public Uid<ParticleConfigBase> ConfigId     { get; private set; }

		/// <summary>The config the effect is handed out for; null once a pooled effect is recycled.</summary>
		internal ParticleConfigBase Config => _configBase;

		/// <summary>The spawner whose pool the effect belongs to; null for an effect of your own.</summary>
		internal ParticleSpawner Owner { get; private set; }

		private TimeDomain EffectTime =>
			_rootParticle != null && _rootParticle.main.useUnscaledTime ? TimeDomain.Unscaled : TimeDomain.Scaled;

		private void Awake()
		{
			CaptureDefaults();
		}

		/// <summary>Makes the effect one of <paramref name="owner"/>'s pooled effects.</summary>
		internal void Adopt(ParticleSpawner owner)
		{
			Owner = owner;
		}

		private void CaptureDefaults()
		{
			if (_defaultsCaptured) return;
			_defaultsCaptured = true;

			_parent = transform.parent;
			_defaultLocalPosition = transform.localPosition;
			_defaultLocalRotation = transform.localRotation;
			_defaultLocalScale = transform.localScale;

			_defaultLayers = new List<KeyValuePair<Transform, int>>();
			foreach (Transform child in GetComponentsInChildren<Transform>(true))
			{
				_defaultLayers.Add(new KeyValuePair<Transform, int>(child, child.gameObject.layer));
			}

			if (_colorTargets != null)
			{
				_defaultColors = new ParticleSystem.MinMaxGradient[_colorTargets.Count];
				for (int i = 0; i < _colorTargets.Count; i++)
				{
					_defaultColors[i] = _colorTargets[i] != null ? _colorTargets[i].main.startColor : default;
				}
			}
		}

		/// <summary>
		/// Restores prefab-authored state — parent, local transform, per-child layers, and
		/// start colors. Called on every recycle so a pooled instance goes back pristine.
		/// </summary>
		public void ResetState()
		{
			CaptureDefaults();

			ResetParent();
			transform.localPosition = _defaultLocalPosition;
			transform.localRotation = _defaultLocalRotation;
			transform.localScale = _defaultLocalScale;

			foreach (KeyValuePair<Transform, int> pair in _defaultLayers)
			{
				if (pair.Key != null)
				{
					pair.Key.gameObject.layer = pair.Value;
				}
			}

			if (_colorTargets != null && _defaultColors != null)
			{
				for (int i = 0; i < _colorTargets.Count && i < _defaultColors.Length; i++)
				{
					if (_colorTargets[i] != null)
					{
						var main = _colorTargets[i].main;
						main.startColor = _defaultColors[i];
					}
				}
			}
		}

		/// <summary>
		/// Hands the effect out for <paramref name="configBase"/>, ending what it was doing.
		/// <paramref name="onStop"/> runs once it is recycled, after a pooled effect is back in
		/// its pool.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="configBase"/> is null.</exception>
		public virtual void Init(ParticleConfigBase configBase, Action onStop)
		{
			if (configBase == null) throw new ArgumentNullException(nameof(configBase));

			CaptureDefaults();

			_generation++;
			_configBase = configBase;
			ConfigId = configBase.IdAs<ParticleConfigBase>();
			_onStop = onStop;
			_leased = true;
			_shown = false;
			_stopping = false;

			if (_rootParticle == null)
			{
				_rootParticle = GetComponent<ParticleSystem>();
			}

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

		/// <summary>
		/// Stops the effect: its systems stop emitting, and it is recycled once its particles
		/// have died. An effect that isn't playing yet, handed out or in its start delay, is
		/// recycled at once.
		/// </summary>
		public void Stop()
		{
			if (!_leased || _stopping) return;

			if (_shown && _rootParticle != null && _rootParticle.isPlaying)
			{
				// The stop callback recycles through OnParticleSystemStopped.
				_stopping = true;
				_rootParticle.Stop(true);
				return;
			}

			_generation++;
			Recycle();
		}

		protected void OnParticleSystemStopped()
		{
			Recycle();
		}

		/// <summary>
		/// Plays the effect after its start delay. A pooled effect must be handed out first; an
		/// effect of your own plays again with its config.
		/// </summary>
		public virtual void Show()
		{
			if (!_leased)
			{
				if (Owner is not null || _configBase == null)
				{
					Debug.LogError($"ParticleComponent '{name}' has no config to show: Init it first. A pooled effect forgets its config when it is recycled.", this);
					return;
				}

				_leased = true;
			}

			_shown = true;
			_stopping = false;
			ActivateAsync(++_generation).Forget();
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
			if (_colorTargets != null)
			{
				for (int i = 0; i < _colorTargets.Count; i++)
				{
					if (_colorTargets[i] == null) continue;

					var mainModule = _colorTargets[i].main;
					mainModule.startColor = color;
				}
			}

			Show(position, rotation);
		}

		/// <summary>Runs once the effect is recycled, before its stop callback.</summary>
		protected virtual void OnStop()
		{
		}

		private void Recycle()
		{
			if (!_leased) return;

			// Root stopped but children (trails, fading smoke) can still be alive —
			// recycle only when the whole hierarchy is truly dead, else pop.
			if (_configBase.WaitForChildrenToFinish && _rootParticle != null && _rootParticle.IsAlive(true))
			{
				RecycleWhenFullyDeadAsync(_generation).Forget();
				return;
			}

			FinalizeRecycle();
		}

		private void FinalizeRecycle()
		{
			if (!_leased) return;

			_leased = false;
			_shown = false;
			_stopping = false;
			_generation++;
			ResetState();

			Action onStop = _onStop;

			if (Owner is not null)
			{
				// Forgets its hand-out before the pool can hand it out again.
				ParticleConfigBase config = _configBase;
				_configBase = null;
				_onStop = null;
				ConfigId = default;

				if (Owner != null) Owner.Release(this, config);
				else Destroy(gameObject); // its spawner is gone
			}

			OnStop();
			onStop?.Invoke();
		}

		private async UniTaskVoid RecycleWhenFullyDeadAsync(int generation)
		{
			try
			{
				double waited = 0d;

				while (_rootParticle != null && _rootParticle.IsAlive(true) && waited < MaxRecycleWaitSeconds)
				{
					await EffectTime.Delay(RecyclePollSeconds, destroyCancellationToken);
					if (generation != _generation) return;

					waited += RecyclePollSeconds;
				}

				FinalizeRecycle();
			}
			catch (OperationCanceledException)
			{
				// Destroyed while waiting.
			}
		}

		private async UniTaskVoid ActivateAsync(int generation)
		{
			try
			{
				if (_configBase.StartDelayInSeconds > 0)
				{
					await EffectTime.Delay(_configBase.StartDelayInSeconds, destroyCancellationToken);
					if (generation != _generation) return;
				}

				gameObject.SetActive(true);
				_rootParticle.Play(true);

				if (_rootParticle.main.loop && _configBase.StopAfterSeconds > 0)
				{
					await EffectTime.Delay(_configBase.StopAfterSeconds, destroyCancellationToken);
					if (generation == _generation) Stop();
				}
			}
			catch (OperationCanceledException)
			{
				// Destroyed while waiting.
			}
		}

		private void OnDestroy()
		{
			_generation++;
			_leased = false;

			if (Owner != null)
			{
				Owner.Forget(this);
			}
		}
	}
}
