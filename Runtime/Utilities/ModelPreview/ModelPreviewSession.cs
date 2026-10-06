using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core.Extensions;
using AK.Kernel.Collections;
using AK.Kernel.Timing;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AK.Utilities.Previews
{
	/// <summary>
	/// Dialog-owned lease over named offscreen model preview booths. Dispose it when the dialog
	/// closes: that cancels pending loads, destroys every booth and gives back every asset claim.
	///
	/// <para><b>Keys.</b> Each booth has a caller-chosen key. Operations on one key (load, update,
	/// release) take effect in call order, one at a time. Operations on different keys run in
	/// parallel.</para>
	///
	/// <para><b>Ownership.</b> Each booth claims the stage prefab, and the model prefab when it
	/// loaded it by address; every claim is given back exactly once. A prefab the caller passes in
	/// stays the caller's. A texture the session creates is destroyed with its booth; a caller's
	/// texture is only unbound.</para>
	///
	/// <para><b>Failure.</b> A request that can't be served (empty address, no free booth, texture
	/// already in use, model without renderers) logs an error and returns null. A failed asset
	/// load throws. A load cancelled by its token, by <see cref="Release(string)"/> or by
	/// <see cref="Dispose"/> throws <see cref="OperationCanceledException"/> and leaves nothing
	/// behind.</para>
	///
	/// <para><b>After dispose.</b> Loads and updates throw <see cref="ObjectDisposedException"/>;
	/// every other call does nothing, since late UI callbacks are normal once a dialog closes.</para>
	///
	/// <para>Main thread only.</para>
	/// </summary>
	public sealed class ModelPreviewSession : IDisposable
	{
		private const float DefaultIntroDuration = 0.45f;
		private const int MinTextureSize = 16;

		private readonly ModelPreviewStageSpace _space;
		private readonly IModelPreviewAssets _assets;
		private readonly ModelPreviewSessionOptions _options;
		private readonly Dictionary<string, ModelPreviewBooth> _booths = new(StringComparer.Ordinal);
		private readonly Dictionary<string, KeyQueue> _queues = new(StringComparer.Ordinal);
		private readonly List<Renderer> _renderers = new();

		private int _creating;
		private bool _disposed;

		internal ModelPreviewSession(ModelPreviewStageSpace space, IModelPreviewAssets assets, ModelPreviewSessionOptions options)
		{
			_space   = space ?? throw new ArgumentNullException(nameof(space));
			_assets  = assets ?? throw new ArgumentNullException(nameof(assets));
			_options = options ?? new ModelPreviewSessionOptions();
		}

		/// <summary>Booths currently open.</summary>
		public int Count => _booths.Count;

		public bool IsDisposed => _disposed;

		// ------------------------------------------------------------------ loading

		/// <summary>
		/// Shows the model at <paramref name="modelAddress"/> in the booth named
		/// <paramref name="key"/>, creating the booth on first use, and binds it to
		/// <paramref name="target"/> when one is given. For a booth that is already open, the same
		/// model only rebinds, another model is swapped in with these options, and another
		/// <paramref name="renderTexture"/> rebuilds the booth around it.
		/// </summary>
		/// <param name="renderTexture">Render into this caller-owned texture instead of one the session creates.</param>
		/// <returns>The preview, or null when the request can't be served (the reason is logged).</returns>
		public UniTask<ModelPreview> LoadAsync(string key, string modelAddress, RawImage target = null,
		                                       RenderTexture renderTexture = null, ModelPreviewOptions options = null,
		                                       CancellationToken cancellation = default)
		{
			return LoadInternalAsync(key, ModelSource.FromAddress(modelAddress), target, renderTexture, options, cancellation);
		}

		/// <summary>
		/// <see cref="LoadAsync(string,string,RawImage,RenderTexture,ModelPreviewOptions,CancellationToken)"/>
		/// for a prefab the caller already holds. The session never releases it.
		/// </summary>
		public UniTask<ModelPreview> LoadAsync(string key, GameObject modelPrefab, RawImage target = null,
		                                       RenderTexture renderTexture = null, ModelPreviewOptions options = null,
		                                       CancellationToken cancellation = default)
		{
			return LoadInternalAsync(key, ModelSource.FromPrefab(modelPrefab), target, renderTexture, options, cancellation);
		}

		/// <summary>
		/// Swaps the model in an open booth, keeping its texture, image, view and options. Runs
		/// after any earlier operation on the key, so it can follow a load still in flight.
		/// </summary>
		/// <returns>True once the model shows; false when there is no such preview or the model can't be shown (logged).</returns>
		public UniTask<bool> UpdateAsync(string key, string modelAddress, CancellationToken cancellation = default)
		{
			return UpdateInternalAsync(key, ModelSource.FromAddress(modelAddress), cancellation);
		}

		/// <summary>
		/// <see cref="UpdateAsync(string,string,CancellationToken)"/> for a prefab the caller
		/// already holds. The session never releases it.
		/// </summary>
		public UniTask<bool> UpdateAsync(string key, GameObject modelPrefab, CancellationToken cancellation = default)
		{
			return UpdateInternalAsync(key, ModelSource.FromPrefab(modelPrefab), cancellation);
		}

		// ------------------------------------------------------------------ by key

		public bool TryGet(string key, out ModelPreview preview)
		{
			bool found = TryGetBooth(key, out ModelPreviewBooth booth);
			preview = found ? booth.Preview : null;
			return found;
		}

		public void Bind(string key, RawImage image)
		{
			if (_disposed)
			{
				return;
			}

			if (image == null)
			{
				Debug.LogError($"{nameof(ModelPreviewSession)}: no image to bind preview '{key}' to.");
				return;
			}

			if (!TryGetBooth(key, out ModelPreviewBooth booth))
			{
				Debug.LogError($"{nameof(ModelPreviewSession)}: no preview '{key}' to bind.");
				return;
			}

			BindImage(booth, image);
		}

		public void Unbind(string key)
		{
			if (TryGetBooth(key, out ModelPreviewBooth booth))
			{
				booth.Unbind();
			}
		}

		public void RotateBy(string key, float yawDelta, float pitchDelta)
		{
			if (TryGetBooth(key, out ModelPreviewBooth booth))
			{
				booth.Camera.RotateBy(yawDelta, pitchDelta);
			}
		}

		/// <summary>Multiplies the camera distance: below 1 moves closer, above 1 moves away.</summary>
		public void ZoomBy(string key, float factor)
		{
			if (TryGetBooth(key, out ModelPreviewBooth booth))
			{
				booth.Camera.ZoomBy(factor);
			}
		}

		public void ResetView(string key)
		{
			if (TryGetBooth(key, out ModelPreviewBooth booth))
			{
				booth.Camera.ResetView();
			}
		}

		/// <summary>
		/// Attaches a caller-owned decoration (e.g. a pooled particle) to a booth: parents it to
		/// the stage root at the model's position — it does NOT rotate with the model (the pivot
		/// is the model's turntable only). With <paramref name="changeLayer"/>, its per-child
		/// layers are recorded and swapped to the model layer so the booth camera renders it
		/// (required unless the decoration is already on that layer). Never takes ownership —
		/// see <see cref="Detach(string,GameObject)"/>. The booth only exists once LoadAsync has
		/// resolved; attaching earlier warns and does nothing.
		/// </summary>
		public void Attach(string key, GameObject decoration, bool changeLayer = false)
		{
			if (_disposed || decoration == null)
			{
				return;
			}

			if (!TryGetBooth(key, out ModelPreviewBooth booth))
			{
				// Attaching before the booth exists would silently drop the decoration — say it loudly.
				Debug.LogWarning($"{nameof(ModelPreviewSession)}: no preview '{key}' to attach to — call Attach after LoadAsync completes.", decoration);
				return;
			}

			AttachTo(booth, decoration, changeLayer);
		}

		/// <summary>Restores a decoration's original layers and parent. Never destroys it.</summary>
		public void Detach(string key, GameObject decoration)
		{
			if (TryGetBooth(key, out ModelPreviewBooth booth))
			{
				booth.Detach(decoration);
			}
		}

		public void DetachAll(string key)
		{
			if (TryGetBooth(key, out ModelPreviewBooth booth))
			{
				booth.DetachAll();
			}
		}

		/// <summary>
		/// Closes the booth named <paramref name="key"/>: cancels every load or update queued or
		/// running on it, destroys the booth and gives back its claims. Does nothing for a key
		/// with no booth and nothing in flight.
		/// </summary>
		public void Release(string key)
		{
			if (_disposed || string.IsNullOrEmpty(key))
			{
				return;
			}

			// The cancelled operations unwind on their own and give back whatever they hold.
			if (_queues.Remove(key, out KeyQueue queue))
			{
				queue.Scope.Cancel();
			}

			if (_booths.Remove(key, out ModelPreviewBooth booth))
			{
				DestroyBooth(booth);
			}
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			// Stop everything in flight first; each operation unwinds and gives back what it holds.
			// Cancelling runs their continuations right here, and those retire queues, so take the
			// queues out of the map before cancelling any.
			if (_queues.Count > 0)
			{
				var queues = new KeyQueue[_queues.Count];
				_queues.Values.CopyTo(queues, 0);
				_queues.Clear();

				foreach (KeyQueue queue in queues)
				{
					queue.Scope.Cancel();
				}
			}

			foreach (ModelPreviewBooth booth in _booths.Values)
			{
				DestroyBooth(booth);
			}

			_booths.Clear();
		}

		public UniTask DisposeAsync()
		{
			Dispose();
			return UniTask.CompletedTask;
		}

		// ------------------------------------------------------------------ by handle (ModelPreview)

		internal bool IsCurrent(Handle<ModelPreviewBooth> handle) => TryGetBooth(handle, out _);

		internal void Bind(Handle<ModelPreviewBooth> handle, RawImage image)
		{
			if (image != null && TryGetBooth(handle, out ModelPreviewBooth booth))
			{
				BindImage(booth, image);
			}
		}

		internal void Unbind(Handle<ModelPreviewBooth> handle)
		{
			if (TryGetBooth(handle, out ModelPreviewBooth booth))
			{
				booth.Unbind();
			}
		}

		internal void RotateBy(Handle<ModelPreviewBooth> handle, float yawDelta, float pitchDelta)
		{
			if (TryGetBooth(handle, out ModelPreviewBooth booth))
			{
				booth.Camera.RotateBy(yawDelta, pitchDelta);
			}
		}

		internal void ZoomBy(Handle<ModelPreviewBooth> handle, float factor)
		{
			if (TryGetBooth(handle, out ModelPreviewBooth booth))
			{
				booth.Camera.ZoomBy(factor);
			}
		}

		internal void ResetView(Handle<ModelPreviewBooth> handle)
		{
			if (TryGetBooth(handle, out ModelPreviewBooth booth))
			{
				booth.Camera.ResetView();
			}
		}

		internal void Attach(Handle<ModelPreviewBooth> handle, GameObject decoration, bool changeLayer)
		{
			if (decoration != null && TryGetBooth(handle, out ModelPreviewBooth booth))
			{
				AttachTo(booth, decoration, changeLayer);
			}
		}

		internal void Detach(Handle<ModelPreviewBooth> handle, GameObject decoration)
		{
			if (TryGetBooth(handle, out ModelPreviewBooth booth))
			{
				booth.Detach(decoration);
			}
		}

		internal void DetachAll(Handle<ModelPreviewBooth> handle)
		{
			if (TryGetBooth(handle, out ModelPreviewBooth booth))
			{
				booth.DetachAll();
			}
		}

		internal void Release(Handle<ModelPreviewBooth> handle)
		{
			// A stale handle must not close whatever booth now holds its key.
			if (TryGetBooth(handle, out ModelPreviewBooth booth))
			{
				Release(booth.Key);
			}
		}

		// ------------------------------------------------------------------ operations

		private async UniTask<ModelPreview> LoadInternalAsync(string key, ModelSource source, RawImage target,
		                                                      RenderTexture renderTexture, ModelPreviewOptions options,
		                                                      CancellationToken cancellation)
		{
			ThrowIfUnusable(key, cancellation);

			if (source.IsMissing)
			{
				Debug.LogError($"{nameof(ModelPreviewSession)}: no model address or prefab given for preview '{key}'.");
				return null;
			}

			Turn turn = await EnterAsync(key, cancellation);
			try
			{
				if (renderTexture != null && TryFindTextureUser(renderTexture, key, out string user))
				{
					Debug.LogError($"{nameof(ModelPreviewSession)}: RenderTexture already used by preview '{user}'.", renderTexture);
					return null;
				}

				if (_booths.TryGetValue(key, out ModelPreviewBooth booth))
				{
					if (renderTexture == null || renderTexture == booth.Texture)
					{
						if (!booth.Shows(source.Address, source.Prefab)
						    && !await SwapModelAsync(booth, source, options, turn.Token))
						{
							return null;
						}

						BindImage(booth, target);
						return booth.Preview;
					}

					// Another texture: rebuild the booth around it.
					_booths.Remove(key);
					DestroyBooth(booth);
				}

				if (_booths.Count + _creating >= _options.MaxConcurrent)
				{
					Debug.LogError($"{nameof(ModelPreviewSession)}: MaxConcurrent ({_options.MaxConcurrent}) reached; no booth for preview '{key}'.");
					return null;
				}

				booth = await CreateBoothAsync(key, source, renderTexture, options, turn.Token);
				if (booth == null)
				{
					return null;
				}

				_booths.Add(key, booth);
				BindImage(booth, target);
				return booth.Preview;
			}
			finally
			{
				Leave(turn);
			}
		}

		private async UniTask<bool> UpdateInternalAsync(string key, ModelSource source, CancellationToken cancellation)
		{
			ThrowIfUnusable(key, cancellation);

			if (source.IsMissing)
			{
				Debug.LogError($"{nameof(ModelPreviewSession)}: no model address or prefab given to update preview '{key}'.");
				return false;
			}

			Turn turn = await EnterAsync(key, cancellation);
			try
			{
				if (!_booths.TryGetValue(key, out ModelPreviewBooth booth))
				{
					Debug.LogError($"{nameof(ModelPreviewSession)}: no preview '{key}' to update.");
					return false;
				}

				return booth.Shows(source.Address, source.Prefab)
				       || await SwapModelAsync(booth, source, booth.Options, turn.Token);
			}
			finally
			{
				Leave(turn);
			}
		}

		/// <summary>
		/// Loads what a new booth needs and builds it. Null when the model can't be shown (logged).
		/// Whatever it claimed goes back if it fails, is cancelled or returns null; otherwise the
		/// booth holds the claims.
		/// </summary>
		private async UniTask<ModelPreviewBooth> CreateBoothAsync(string key, ModelSource source, RenderTexture renderTexture,
		                                                          ModelPreviewOptions options, CancellationToken token)
		{
			// Hold a place under MaxConcurrent while loading, so loads on other keys can't overshoot it.
			_creating++;

			GameObject stageAsset = null;
			GameObject modelAsset = null;
			ModelPreviewBooth booth = null;
			try
			{
				stageAsset = await ClaimAsync(_assets.LoadStageAsync(token), token, "the stage");
				modelAsset = source.Address != null
					? await ClaimAsync(_assets.LoadModelAsync(source.Address, token), token, source.Address)
					: source.Prefab;

				booth = BuildBooth(key, stageAsset, modelAsset, source.Address, renderTexture, options);
				return booth;
			}
			finally
			{
				_creating--;

				if (booth == null)
				{
					if (!ReferenceEquals(stageAsset, null))
					{
						_assets.Release(stageAsset);
					}

					if (!ReferenceEquals(modelAsset, null))
					{
						ReleaseModel(modelAsset, source.Address);
					}
				}
			}
		}

		/// <summary>
		/// Assembles a booth from loaded prefabs, synchronously. On success the booth holds both
		/// claims. On failure (null, or an exception) nothing is left in the scene and the claims
		/// stay the caller's to give back.
		/// </summary>
		private ModelPreviewBooth BuildBooth(string key, GameObject stageAsset, GameObject modelAsset, string modelAddress,
		                                     RenderTexture renderTexture, ModelPreviewOptions options)
		{
			GameObject root = Object.Instantiate(stageAsset);
			ModelPreviewBooth booth = null;
			try
			{
				var camera = root.GetComponentInChildren<ModelPreviewCamera>(true);
				if (camera == null || camera.Camera == null)
				{
					throw new InvalidOperationException(
						$"{nameof(ModelPreviewSession)}: stage prefab '{stageAsset.name}' needs a {nameof(ModelPreviewCamera)} with a Camera under it.");
				}

				booth = new ModelPreviewBooth(this, key, stageAsset, root, camera);
				booth.Handle = _space.Add(booth);
				root.name = $"ModelPreview_{key}";
				root.transform.position = ModelPreviewStageSpace.PositionOf(booth.Handle);

				GameObject model = SpawnModel(camera, modelAsset);
				if (model == null)
				{
					LogNoRenderers(key);
					DiscardBooth(booth);
					return null;
				}

				bool ownsTexture = renderTexture == null;
				booth.SetTexture(ownsTexture ? CreateTexture(key, _options.TextureSize) : renderTexture, ownsTexture);
				booth.ReplaceModel(model, modelAsset, modelAddress, out _, out _);
				booth.Options = options;

				camera.SetTimeDomain(_options.TimeDomain);
				camera.SetTargetTexture(booth.Texture);
				camera.SetStatic(_options.RenderMode == ModelPreviewRenderMode.Static, _options.StaticWarmupFrames);
				ApplyOptions(booth);

				booth.Preview = new ModelPreview(this, booth.Handle, key, booth.Texture, ownsTexture);
				return booth;
			}
			catch
			{
				if (booth != null)
				{
					DiscardBooth(booth);
				}
				else
				{
					root.DestroyInAnyMode();
				}

				throw;
			}
		}

		/// <summary>
		/// Puts the model <paramref name="source"/> names into an open booth with
		/// <paramref name="options"/>, and gives back the claim on the model it replaces.
		/// False when the new model can't be shown (logged); the booth then keeps its model.
		/// </summary>
		private async UniTask<bool> SwapModelAsync(ModelPreviewBooth booth, ModelSource source,
		                                           ModelPreviewOptions options, CancellationToken token)
		{
			GameObject asset = source.Address != null
				? await ClaimAsync(_assets.LoadModelAsync(source.Address, token), token, source.Address)
				: source.Prefab;

			GameObject model = SpawnModel(booth.Camera, asset);
			if (model == null)
			{
				LogNoRenderers(booth.Key);
				ReleaseModel(asset, source.Address);
				return false;
			}

			booth.ReplaceModel(model, asset, source.Address, out GameObject previousAsset, out string previousAddress);
			ReleaseModel(previousAsset, previousAddress);

			booth.Options = options;
			ApplyOptions(booth);
			return true;
		}

		/// <summary>
		/// Awaits a claiming load. A load that lands after cancellation, or lands nothing, gives
		/// its claim straight back.
		/// </summary>
		private async UniTask<GameObject> ClaimAsync(UniTask<GameObject> load, CancellationToken token, string what)
		{
			GameObject asset = await load;
			if (asset != null && !token.IsCancellationRequested)
			{
				return asset;
			}

			if (!ReferenceEquals(asset, null))
			{
				_assets.Release(asset);
			}

			token.ThrowIfCancellationRequested();
			throw new InvalidOperationException($"{nameof(ModelPreviewSession)}: loading {what} returned no prefab.");
		}

		// ------------------------------------------------------------------ key queues

		/// <summary>Waits until every earlier operation on <paramref name="key"/> has left, then gives this one the key.</summary>
		private async UniTask<Turn> EnterAsync(string key, CancellationToken cancellation)
		{
			if (!_queues.TryGetValue(key, out KeyQueue queue))
			{
				queue = new KeyQueue();
				_queues.Add(key, queue);
			}

			UniTaskCompletionSource ahead = queue.Tail;
			var turn = new Turn(key, queue, cancellation);
			queue.Tail = turn.Done;
			queue.Operations++;

			if (ahead != null)
			{
				try
				{
					await ahead.Task.AttachExternalCancellation(turn.Token);
				}
				catch (OperationCanceledException)
				{
					// Leaving the line early must not let the next operation overtake the ones
					// still ahead, so this place passes on only once they have left.
					LeaveAfterAsync(ahead, turn).Forget();
					throw;
				}
			}

			return turn;
		}

		private async UniTaskVoid LeaveAfterAsync(UniTaskCompletionSource ahead, Turn turn)
		{
			await ahead.Task;
			Leave(turn);
		}

		private void Leave(Turn turn)
		{
			turn.Linked?.Dispose();

			KeyQueue queue = turn.Queue;
			if (--queue.Operations == 0)
			{
				// Idle: retire the queue, unless Release or Dispose already took it out of the map.
				if (_queues.TryGetValue(turn.Key, out KeyQueue mapped) && mapped == queue)
				{
					_queues.Remove(turn.Key);
				}

				queue.Scope.Dispose();
			}

			// Last, so the operation this wakes finds the queue settled.
			turn.Done.TrySetResult();
		}

		// ------------------------------------------------------------------ booths

		private bool TryGetBooth(string key, out ModelPreviewBooth booth)
		{
			if (_disposed || string.IsNullOrEmpty(key))
			{
				booth = null;
				return false;
			}

			return _booths.TryGetValue(key, out booth);
		}

		private bool TryGetBooth(Handle<ModelPreviewBooth> handle, out ModelPreviewBooth booth)
		{
			// Handles come from a stage space shared by every session of the service.
			if (!_disposed && _space.TryGet(handle, out booth) && booth.Session == this)
			{
				return true;
			}

			booth = null;
			return false;
		}

		/// <summary>Background, framing, auto-rotate, intro and interaction, from the booth's options.</summary>
		private void ApplyOptions(ModelPreviewBooth booth)
		{
			ModelPreviewOptions options = booth.Options;
			ModelPreviewCamera camera = booth.Camera;

			camera.SetBackground(options?.BackgroundColor);
			camera.Frame(ComputeBounds(booth.Model), options?.FramingMargin ?? _options.FramingMargin);
			camera.SetAutoRotate(options?.AutoRotateSpeed ?? 0f);
			PlayIntro(booth.Model.transform, camera, options);
			booth.SetInteractive(IsInteractive(booth));
		}

		private bool IsInteractive(ModelPreviewBooth booth) => booth.Options?.EnableInteraction ?? _options.EnableInteraction;

		private void BindImage(ModelPreviewBooth booth, RawImage image)
		{
			if (image == null)
			{
				return;
			}

			booth.Bind(image);
			booth.SetInteractive(IsInteractive(booth));
		}

		private static void AttachTo(ModelPreviewBooth booth, GameObject decoration, bool changeLayer)
		{
			booth.Attach(decoration, changeLayer, FirstSetLayer(booth.Camera.ModelLayer));
		}

		/// <summary>Tears down a registered booth and gives back its claims. The caller takes it out of the map.</summary>
		private void DestroyBooth(ModelPreviewBooth booth)
		{
			GameObject modelAsset = booth.ModelAsset;
			string modelAddress = booth.ModelAddress;

			// Instances go before the claims on the prefabs they came from.
			DiscardBooth(booth);
			_assets.Release(booth.StageAsset);
			ReleaseModel(modelAsset, modelAddress);
		}

		/// <summary>Destroys a booth and frees its slot. Claims are the caller's to give back.</summary>
		private void DiscardBooth(ModelPreviewBooth booth)
		{
			_space.Remove(booth.Handle);
			booth.Destroy();
		}

		private void ReleaseModel(GameObject asset, string address)
		{
			// Only address loads are claims; a caller's prefab stays the caller's.
			if (address != null)
			{
				_assets.Release(asset);
			}
		}

		private bool TryFindTextureUser(RenderTexture texture, string exceptKey, out string user)
		{
			foreach (KeyValuePair<string, ModelPreviewBooth> pair in _booths)
			{
				if (pair.Value.Texture == texture && !string.Equals(pair.Key, exceptKey, StringComparison.Ordinal))
				{
					user = pair.Key;
					return true;
				}
			}

			user = null;
			return false;
		}

		// ------------------------------------------------------------------ models

		/// <summary>
		/// Instantiates a model on the camera's turntable, on the camera's model layer, centred on
		/// the pivot. Null, with nothing left behind, when the prefab has no renderer at all.
		/// </summary>
		private GameObject SpawnModel(ModelPreviewCamera camera, GameObject prefab)
		{
			Transform pivot = camera.Pivot;
			GameObject model = Object.Instantiate(prefab, pivot);
			Transform modelTransform = model.transform;
			modelTransform.localPosition = Vector3.zero;
			modelTransform.localRotation = Quaternion.identity;

			if (model.GetComponentInChildren<Renderer>(true) == null)
			{
				model.DestroyInAnyMode();
				return null;
			}

			int layer = FirstSetLayer(camera.ModelLayer);
			if (layer >= 0)
			{
				SetLayerRecursively(modelTransform, layer);
			}

			// The turntable spins about the pivot, so the model's visual centre must sit on it;
			// off-centre geometry would orbit the frame as it turns, and clip.
			Bounds bounds = ComputeBounds(model);
			modelTransform.position += pivot.position - bounds.center;
			return model;
		}

		/// <summary>
		/// World bounds of what the model shows: its active, enabled renderers. Particle, trail and
		/// line renderers are left out, since their bounds follow whatever they have emitted and
		/// would throw the framing off. A model with none of these gets a unit box at its position.
		/// </summary>
		private Bounds ComputeBounds(GameObject model)
		{
			model.GetComponentsInChildren(false, _renderers);

			bool found = false;
			Bounds bounds = default;
			foreach (Renderer renderer in _renderers)
			{
				if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
				{
					continue;
				}

				if (found)
				{
					bounds.Encapsulate(renderer.bounds);
				}
				else
				{
					bounds = renderer.bounds;
					found  = true;
				}
			}

			_renderers.Clear();
			return found ? bounds : new Bounds(model.transform.position, Vector3.one);
		}

		/// <summary>Plays the entrance tween. A static camera is held on until it ends, so the still shows the final pose.</summary>
		private static void PlayIntro(Transform model, ModelPreviewCamera camera, ModelPreviewOptions options)
		{
			ModelPreviewIntro intro = options?.Intro ?? ModelPreviewIntro.Pop;
			if (intro == ModelPreviewIntro.None)
			{
				return;
			}

			float duration = Mathf.Max(0.01f, options?.IntroDuration ?? DefaultIntroDuration);
			Ease ease = options?.IntroEase ?? Ease.OutBack;

			Vector3 targetScale = model.localScale;
			model.localScale = Vector3.zero;

			camera.BeginHold();
			model.DOScale(targetScale, duration)
			     .SetEase(ease)
			     .SetTimeDomain(camera.TimeDomain)
			     .SetAutoKill(true)
			     .SetLink(model.gameObject, LinkBehaviour.KillOnDestroy)
			     .OnKill(camera.EndHold)
			     .Play(); // explicit, whatever DOTween's autoPlay setting is
		}

		private static void LogNoRenderers(string key)
		{
			Debug.LogError($"{nameof(ModelPreviewSession)}: the model for preview '{key}' has no renderers.");
		}

		private static int FirstSetLayer(LayerMask mask)
		{
			int value = mask.value;
			for (int i = 0; i < 32; i++)
			{
				if ((value & (1 << i)) != 0)
				{
					return i;
				}
			}

			return -1;
		}

		private static void SetLayerRecursively(Transform root, int layer)
		{
			root.gameObject.layer = layer;
			for (int i = 0, count = root.childCount; i < count; i++)
			{
				SetLayerRecursively(root.GetChild(i), layer);
			}
		}

		private static RenderTexture CreateTexture(string key, int size)
		{
			int safeSize = Mathf.Max(MinTextureSize, size);
			var texture = new RenderTexture(safeSize, safeSize, 24, RenderTextureFormat.ARGB32)
			{
				name             = $"ModelPreview_{key}",
				antiAliasing     = 1,
				filterMode       = FilterMode.Bilinear,
				wrapMode         = TextureWrapMode.Clamp,
				useMipMap        = false,
				autoGenerateMips = false,
			};
			texture.Create();
			return texture;
		}

		// ------------------------------------------------------------------ guards

		private void ThrowIfUnusable(string key, CancellationToken cancellation)
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(ModelPreviewSession));
			}

			if (string.IsNullOrEmpty(key))
			{
				throw new ArgumentException("Preview key is required.", nameof(key));
			}

			cancellation.ThrowIfCancellationRequested();
		}

		// ------------------------------------------------------------------ types

		/// <summary>Where a model comes from: an address the session loads and claims, or a prefab the caller holds.</summary>
		private readonly struct ModelSource
		{
			/// <summary>Null unless the model loads by address.</summary>
			public readonly string Address;

			/// <summary>Null unless the caller passed the prefab.</summary>
			public readonly GameObject Prefab;

			private ModelSource(string address, GameObject prefab)
			{
				Address = address;
				Prefab  = prefab;
			}

			public bool IsMissing => Address == null && Prefab == null;

			public static ModelSource FromAddress(string address) => new(string.IsNullOrEmpty(address) ? null : address, null);

			public static ModelSource FromPrefab(GameObject prefab) => new(null, prefab);
		}

		/// <summary>The operations on one key, run one at a time in call order. Exists while any is queued or running.</summary>
		private sealed class KeyQueue
		{
			/// <summary>Cancelled by Release and Dispose: stops every operation on the key.</summary>
			public readonly CancellationTokenSource Scope = new();

			/// <summary>Completes when the most recently queued operation leaves.</summary>
			public UniTaskCompletionSource Tail;

			/// <summary>Operations queued or running.</summary>
			public int Operations;
		}

		/// <summary>One operation's place in a <see cref="KeyQueue"/>.</summary>
		private readonly struct Turn
		{
			public readonly string Key;
			public readonly KeyQueue Queue;

			/// <summary>Completes when this operation leaves, letting the next one on the key in.</summary>
			public readonly UniTaskCompletionSource Done;

			/// <summary>Links the caller's token with the key's scope; null when the caller passed none.</summary>
			public readonly CancellationTokenSource Linked;

			/// <summary>Cancelled by the caller, by Release of the key, or by Dispose.</summary>
			public readonly CancellationToken Token;

			public Turn(string key, KeyQueue queue, CancellationToken cancellation)
			{
				Key    = key;
				Queue  = queue;
				Done   = new UniTaskCompletionSource();
				Linked = cancellation.CanBeCanceled
					? CancellationTokenSource.CreateLinkedTokenSource(queue.Scope.Token, cancellation)
					: null;
				Token  = Linked != null ? Linked.Token : queue.Scope.Token;
			}
		}
	}
}
