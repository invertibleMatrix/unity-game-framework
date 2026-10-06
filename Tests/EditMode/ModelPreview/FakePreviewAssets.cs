using System;
using System.Collections.Generic;
using System.Threading;
using AK.Utilities.Previews;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Tests.Previews
{
	/// <summary>
	/// <see cref="IModelPreviewAssets"/> that counts claims per prefab. Loads land at once, or,
	/// when <see cref="Deferred"/>, wait until the test completes or fails them, oldest first.
	/// A release without a claim throws, so an over-release fails the test where it happens.
	/// </summary>
	internal sealed class FakePreviewAssets : IModelPreviewAssets
	{
		private readonly Dictionary<GameObject, int> _claims = new();
		private readonly Dictionary<GameObject, int> _releases = new();
		private readonly List<PendingLoad> _pending = new();

		public FakePreviewAssets(GameObject stage)
		{
			Stage = stage;
		}

		public GameObject Stage { get; }

		public Dictionary<string, GameObject> Models { get; } = new(StringComparer.Ordinal);

		/// <summary>Loads wait for <see cref="CompleteNext"/> or <see cref="FailNext"/> instead of landing at once.</summary>
		public bool Deferred { get; set; }

		/// <summary>Deferred loads ignore their cancellation token, like a provider that can't abort.</summary>
		public bool IgnoreCancellation { get; set; }

		/// <summary>Thrown by the next stage load, then cleared.</summary>
		public Exception NextStageFailure { get; set; }

		public int StageLoads { get; private set; }
		public int ModelLoads { get; private set; }
		public int PendingCount => _pending.Count;

		public int OutstandingClaims
		{
			get
			{
				int total = 0;
				foreach (int count in _claims.Values)
				{
					total += count;
				}

				return total;
			}
		}

		public int ClaimsOn(GameObject asset) => _claims.TryGetValue(asset, out int count) ? count : 0;

		public int ReleasesOf(GameObject asset) => _releases.TryGetValue(asset, out int count) ? count : 0;

		public UniTask<GameObject> LoadStageAsync(CancellationToken cancellation)
		{
			StageLoads++;

			if (NextStageFailure != null)
			{
				Exception failure = NextStageFailure;
				NextStageFailure = null;
				return UniTask.FromException<GameObject>(failure);
			}

			return Load(Stage, cancellation);
		}

		public UniTask<GameObject> LoadModelAsync(string address, CancellationToken cancellation)
		{
			ModelLoads++;

			return Models.TryGetValue(address, out GameObject model)
				? Load(model, cancellation)
				: UniTask.FromException<GameObject>(new InvalidOperationException($"No model at '{address}'."));
		}

		public void Release(GameObject asset)
		{
			if (ClaimsOn(asset) == 0)
			{
				throw new InvalidOperationException("Released a prefab that holds no claim.");
			}

			_claims[asset]--;
			_releases[asset] = ReleasesOf(asset) + 1;
		}

		/// <summary>The prefab the oldest pending load will land.</summary>
		public GameObject NextPending => _pending[0].Asset;

		/// <summary>Lands the oldest pending load: one claim, then the caller resumes.</summary>
		public void CompleteNext() => TakeNext().Complete();

		public void FailNext(Exception failure) => TakeNext().Fail(failure);

		public void CompleteAll()
		{
			while (_pending.Count > 0)
			{
				CompleteNext();
			}
		}

		private UniTask<GameObject> Load(GameObject asset, CancellationToken cancellation)
		{
			if (cancellation.IsCancellationRequested)
			{
				return UniTask.FromCanceled<GameObject>(cancellation);
			}

			if (!Deferred)
			{
				Claim(asset);
				return UniTask.FromResult(asset);
			}

			var load = new PendingLoad(this, asset);
			_pending.Add(load);

			if (!IgnoreCancellation)
			{
				load.CancelWith(cancellation);
			}

			return load.Task;
		}

		private PendingLoad TakeNext()
		{
			PendingLoad load = _pending[0];
			_pending.RemoveAt(0);
			return load;
		}

		private void Claim(GameObject asset) => _claims[asset] = ClaimsOn(asset) + 1;

		private sealed class PendingLoad
		{
			private readonly FakePreviewAssets _owner;
			private readonly UniTaskCompletionSource<GameObject> _source = new();
			private CancellationTokenRegistration _registration;

			public PendingLoad(FakePreviewAssets owner, GameObject asset)
			{
				_owner = owner;
				Asset  = asset;
			}

			public GameObject Asset { get; }

			public UniTask<GameObject> Task => _source.Task;

			public void CancelWith(CancellationToken cancellation)
			{
				_registration = cancellation.Register(() =>
				{
					// Cancelled before landing: no claim was made.
					_owner._pending.Remove(this);
					_source.TrySetCanceled(cancellation);
				});
			}

			public void Complete()
			{
				_registration.Dispose();
				_owner.Claim(Asset);
				_source.TrySetResult(Asset);
			}

			public void Fail(Exception failure)
			{
				_registration.Dispose();
				_source.TrySetException(failure);
			}
		}
	}
}
