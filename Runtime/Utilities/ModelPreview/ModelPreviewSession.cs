using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core.ResourceManagement;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Utilities.ModelPreview
{
	/// <summary>
	/// Dialog-owned lease over N offscreen model preview booths.
	/// Dispose when the dialog closes — destroys stages and releases session-owned RTs only.
	/// </summary>
	public sealed class ModelPreviewSession : IDisposable
	{
		private const float StageBaseY = -3000f;
		private const float StageYStride = 50f;
		private const float StageXStride = 20f;

		private readonly AssetReferenceT<GameObject> _stagePrefab;
		private readonly ModelPreviewSessionOptions _options;
		private readonly Dictionary<string, Booth> _booths = new();
		private readonly SemaphoreSlim _gate = new(1, 1);

		private GameObject _stageAsset;
		private int _nextSlot;
		private bool _disposed;

		internal ModelPreviewSession(AssetReferenceT<GameObject> stagePrefab, ModelPreviewSessionOptions options)
		{
			_stagePrefab = stagePrefab;
			_options = options ?? new ModelPreviewSessionOptions();
		}

		/// <summary>Load a model by addressable id into a named preview booth.</summary>
		public UniTask<ModelPreview> LoadAsync(string key, string modelAddress, RawImage target = null,
		                                       RenderTexture renderTexture = null, ModelPreviewOptions options = null)
		{
			if (string.IsNullOrEmpty(modelAddress))
			{
				Debug.LogError($"{nameof(ModelPreviewSession)}: model address is required for preview '{key}'.");
				return UniTask.FromResult<ModelPreview>(null);
			}

			return LoadInternalAsync(key, () => UniResources.LoadAssetAsync<GameObject>(modelAddress), target, renderTexture, options);
		}

		/// <summary>Load an already-resolved model prefab into a named preview booth.</summary>
		public UniTask<ModelPreview> LoadAsync(string key, GameObject modelPrefab, RawImage target = null,
		                                       RenderTexture renderTexture = null, ModelPreviewOptions options = null)
		{
			return LoadInternalAsync(key, () => UniTask.FromResult(modelPrefab), target, renderTexture, options);
		}

		/// <summary>Swap the model inside an existing booth, keeping its texture and bindings.</summary>
		public async UniTask UpdateAsync(string key, string modelAddress)
		{
			ThrowIfDisposed();

			if (!await TryEnterGateAsync())
			{
				return;
			}

			try
			{
				if (_disposed)
				{
					return;
				}

				if (!_booths.TryGetValue(key, out Booth booth))
				{
					Debug.LogError($"{nameof(ModelPreviewSession)}: no preview '{key}' to update.");
					return;
				}

			GameObject prefab = await UniResources.LoadAssetAsync<GameObject>(modelAddress);
			if (prefab == null || _disposed)
			{
				return;
			}

			booth.ReplaceModel(prefab, _options.FramingMargin);
			float introDuration = PlayIntro(booth.Model, null);

			// A static booth's camera has already shut off — re-render or the swap never shows.
			if (_options.RenderMode == ModelPreviewRenderMode.Static && booth.Camera != null)
			{
				booth.Camera.RenderStatic(_options.StaticWarmupFrames + Mathf.CeilToInt(introDuration * 60f));
			}
			}
			finally
			{
				ExitGate();
			}
		}

		public bool TryGet(string key, out ModelPreview preview)
		{
			preview = null;
			if (_disposed || string.IsNullOrEmpty(key) || !_booths.TryGetValue(key, out Booth booth))
			{
				return false;
			}

			preview = booth.Preview;
			return true;
		}

		public void Bind(string key, RawImage image)
		{
			ThrowIfDisposed();
			if (image == null || !_booths.TryGetValue(key, out Booth booth))
			{
				Debug.LogError($"{nameof(ModelPreviewSession)}: no preview '{key}' to bind.");
				return;
			}

			bool interactive = booth.Options?.EnableInteraction ?? _options.EnableInteraction;
			if (interactive && booth.Interactable == null)
			{
				booth.Interactable = image.GetComponent<ModelPreviewInteractable>();
				if (booth.Interactable == null)
				{
					booth.Interactable = image.gameObject.AddComponent<ModelPreviewInteractable>();
					booth.InteractableOwned = true;
				}

				booth.Interactable.Init(
					(yaw, pitch) =>
					{
						if (booth.Camera != null)
						{
							booth.Camera.RotateBy(yaw, pitch);
						}
					},
					factor =>
					{
						if (booth.Camera != null)
						{
							booth.Camera.ZoomBy(factor);
						}
					});
			}

			booth.Bind(image);
		}

		public void Unbind(string key)
		{
			if (_disposed || string.IsNullOrEmpty(key) || !_booths.TryGetValue(key, out Booth booth))
			{
				return;
			}

			booth.Unbind();
		}

		public void RotateBy(string key, float yawDelta, float pitchDelta)
		{
			if (!_disposed && _booths.TryGetValue(key, out Booth booth) && booth.Camera != null)
			{
				booth.Camera.RotateBy(yawDelta, pitchDelta);
			}
		}

		public void ZoomBy(string key, float factor)
		{
			if (!_disposed && _booths.TryGetValue(key, out Booth booth) && booth.Camera != null)
			{
				booth.Camera.ZoomBy(factor);
			}
		}

		public void ResetView(string key)
		{
			if (!_disposed && _booths.TryGetValue(key, out Booth booth) && booth.Camera != null)
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
		/// see <see cref="Detach"/>. The booth only exists after LoadAsync resolves — attaching
		/// earlier warns and no-ops.
		/// </summary>
		public void Attach(string key, GameObject decoration, bool changeLayer = false)
		{
			if (_disposed || decoration == null)
			{
				return;
			}

			if (!_booths.TryGetValue(key, out Booth booth))
			{
				// Attaching before the booth exists would silently drop the decoration — say it loudly.
				Debug.LogWarning($"{nameof(ModelPreviewSession)}: no preview '{key}' to attach to — call Attach after LoadAsync completes.", decoration);
				return;
			}

			booth.Attach(decoration, changeLayer);
		}

		/// <summary>Restores a decoration's original layers and unparents it. Never destroys it.</summary>
		public void Detach(string key, GameObject decoration)
		{
			if (!_disposed && decoration != null && _booths.TryGetValue(key, out Booth booth))
			{
				booth.Detach(decoration);
			}
		}

		public void DetachAll(string key)
		{
			if (!_disposed && _booths.TryGetValue(key, out Booth booth))
			{
				booth.DetachAll();
			}
		}

		public void Release(string key)
		{
			if (_disposed || string.IsNullOrEmpty(key) || !_booths.TryGetValue(key, out Booth booth))
			{
				return;
			}

			DestroyBooth(booth);
			_booths.Remove(key);
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			// Non-blocking by design: every in-flight load/update re-checks _disposed at each
			// await boundary and destroys any booth it built before registering it, so this
			// enumeration only ever sees fully-registered booths. A blocking gate wait would
			// deadlock — Dispose runs on the main thread while the in-flight load's
			// continuations need that same thread to complete.
			_disposed = true;

			foreach (Booth booth in _booths.Values)
			{
				DestroyBooth(booth);
			}

			_booths.Clear();
			_gate.Dispose();
		}

		public UniTask DisposeAsync()
		{
			Dispose();
			return UniTask.CompletedTask;
		}

		private async UniTask<ModelPreview> LoadInternalAsync(string key, Func<UniTask<GameObject>> loadModel,
		                                                      RawImage target, RenderTexture renderTexture, ModelPreviewOptions options)
		{
			ThrowIfDisposed();

			if (string.IsNullOrEmpty(key))
			{
				throw new ArgumentException("Preview key is required.", nameof(key));
			}

			if (!await TryEnterGateAsync())
			{
				return null;
			}

			try
			{
				if (_disposed)
				{
					return null;
				}

				if (renderTexture != null && TryFindBoothUsingTexture(renderTexture, key, out string otherKey))
				{
					Debug.LogError($"{nameof(ModelPreviewSession)}: RenderTexture already used by preview '{otherKey}'.", renderTexture);
					return null;
				}

				if (_booths.TryGetValue(key, out Booth existing))
				{
					if (renderTexture == null || renderTexture == existing.Texture)
					{
						if (target != null)
						{
							Bind(key, target);
						}

						return existing.Preview;
					}

					DestroyBooth(existing);
					_booths.Remove(key);
				}

				if (_booths.Count >= _options.MaxConcurrent)
				{
					Debug.LogError($"{nameof(ModelPreviewSession)}: MaxConcurrent ({_options.MaxConcurrent}) reached.");
					return null;
				}

				GameObject modelPrefab = await loadModel();
				if (modelPrefab == null || _disposed)
				{
					return null;
				}

				Booth booth = await CreateBoothAsync(key, modelPrefab, renderTexture, options);
				if (booth == null || _disposed)
				{
					if (booth != null)
					{
						DestroyBooth(booth);
					}

					return null;
				}

				_booths[key] = booth;

				if (target != null)
				{
					Bind(key, target);
				}

				return booth.Preview;
			}
			finally
			{
				ExitGate();
			}
		}

		private async UniTask<GameObject> LoadStageAsync()
		{
			if (_stageAsset != null)
			{
				return _stageAsset;
			}

			if (_stagePrefab == null || !_stagePrefab.RuntimeKeyIsValid())
			{
				Debug.LogError($"{nameof(ModelPreviewSession)}: missing or invalid stage addressable reference.");
				return null;
			}

			_stageAsset = await UniResources.LoadAssetAsync(_stagePrefab);
			return _stageAsset;
		}

		private async UniTask<Booth> CreateBoothAsync(string key, GameObject modelPrefab, RenderTexture callerTexture, ModelPreviewOptions options)
		{
			GameObject stageAsset = await LoadStageAsync();
			if (_disposed || stageAsset == null)
			{
				return null;
			}

			bool ownsTexture = callerTexture == null;
			RenderTexture texture = callerTexture != null ? callerTexture : CreateTexture(key, _options.TextureSize);

			GameObject stage = Object.Instantiate(stageAsset);
			stage.name = $"ModelPreview_{key}";

			int slot = _nextSlot++;
			stage.transform.position = new Vector3(slot * StageXStride, StageBaseY - slot * StageYStride, 0f);

			var camera = stage.GetComponentInChildren<ModelPreviewCamera>(true);
			if (camera == null)
			{
				Debug.LogError($"{nameof(ModelPreviewSession)}: ModelPreviewCamera missing on the stage prefab.", stage);
				Object.Destroy(stage);
				if (ownsTexture)
				{
					ReleaseTexture(texture);
				}

				return null;
			}

			camera.SetTargetTexture(texture);
			camera.SetBackground(options?.BackgroundColor);

			GameObject model = SpawnModel(camera, modelPrefab);
			if (model == null)
			{
				Debug.LogError($"{nameof(ModelPreviewSession)}: model prefab for '{key}' has no renderers or is null.");
				Object.Destroy(stage);
				if (ownsTexture)
				{
					ReleaseTexture(texture);
				}

				return null;
			}

			camera.Frame(ComputeBounds(model), options?.FramingMargin ?? _options.FramingMargin);

			float introDuration = PlayIntro(model, options);

			if (_options.RenderMode == ModelPreviewRenderMode.Static)
			{
				// Keep the camera alive through the intro so the frozen frame shows the final pose.
				camera.RenderStatic(_options.StaticWarmupFrames + Mathf.CeilToInt(introDuration * 60f));
			}
			else if (camera.Camera != null)
			{
				camera.Camera.enabled = true;
			}

			float autoRotate = options?.AutoRotateSpeed ?? 0f;
			if (autoRotate != 0f)
			{
				camera.SetAutoRotate(autoRotate);
			}

			var preview = new ModelPreview(this, key, texture, ownsTexture);
			return new Booth(key, stage, camera, model, texture, ownsTexture, preview, options);
		}

		private static GameObject SpawnModel(ModelPreviewCamera camera, GameObject prefab)
		{
			if (prefab == null || camera.Pivot == null)
			{
				return null;
			}

			GameObject model = Object.Instantiate(prefab, camera.Pivot);
			model.transform.localPosition = Vector3.zero;
			model.transform.localRotation = Quaternion.identity;

			if (model.GetComponentInChildren<Renderer>(true) == null)
			{
				Object.Destroy(model);
				return null;
			}

			CenterModelOnPivot(model, camera.Pivot);

			int layer = FirstSetLayer(camera.ModelLayer);
			if (layer >= 0)
			{
				SetLayerRecursively(model, layer);
			}

			return model;
		}

		/// <summary>Plays the entrance tween on the model; returns its duration (0 when none).</summary>
		private static float PlayIntro(GameObject model, ModelPreviewOptions options)
		{
			if (model == null)
			{
				return 0f;
			}

			ModelPreviewIntro intro = options?.Intro ?? ModelPreviewIntro.Pop;
			if (intro == ModelPreviewIntro.None)
			{
				return 0f;
			}

			float duration = Mathf.Max(0.01f, options?.IntroDuration ?? 0.45f);
			Ease ease = options?.IntroEase ?? Ease.OutBack;

			Transform modelTransform = model.transform;
			Vector3 targetScale = modelTransform.localScale;
			modelTransform.localScale = Vector3.zero;
			modelTransform.DOScale(targetScale, duration)
				.SetEase(ease)
				.SetLink(model, LinkBehaviour.KillOnDestroy)
				.Play(); // explicit — the project runs DOTween with autoPlay off

			return duration;
		}

		// Rotation spins the pivot, so the model's visual center must sit ON the pivot —
		// otherwise off-center geometry orbits the frame as it turns and clips.
		private static void CenterModelOnPivot(GameObject model, Transform pivot)
		{
			Bounds bounds = ComputeBounds(model);
			model.transform.position += pivot.position - bounds.center;
		}

		private static Bounds ComputeBounds(GameObject model)
		{
			var renderers = model.GetComponentsInChildren<Renderer>(true);
			Bounds bounds = renderers[0].bounds;
			for (int i = 1; i < renderers.Length; i++)
			{
				bounds.Encapsulate(renderers[i].bounds);
			}

			return bounds;
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

		private static void SetLayerRecursively(GameObject root, int layer)
		{
			foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
			{
				child.gameObject.layer = layer;
			}
		}

		private bool TryFindBoothUsingTexture(RenderTexture texture, string exceptKey, out string otherKey)
		{
			foreach (KeyValuePair<string, Booth> pair in _booths)
			{
				if (pair.Key != exceptKey && pair.Value.Texture == texture)
				{
					otherKey = pair.Key;
					return true;
				}
			}

			otherKey = null;
			return false;
		}

		private static void DestroyBooth(Booth booth)
		{
			booth?.Destroy();
		}

		private static RenderTexture CreateTexture(string key, int size)
		{
			int safeSize = Mathf.Max(16, size);
			var rt = new RenderTexture(safeSize, safeSize, 24, RenderTextureFormat.ARGB32)
			{
				name = $"ModelPreview_{key}",
				antiAliasing = 1,
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				useMipMap = false,
				autoGenerateMips = false,
			};
			rt.Create();
			return rt;
		}

		private static void ReleaseTexture(RenderTexture rt)
		{
			if (rt == null)
			{
				return;
			}

			if (rt.IsCreated())
			{
				rt.Release();
			}

			Object.Destroy(rt);
		}

		private void ThrowIfDisposed()
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(ModelPreviewSession));
			}
		}

		private async UniTask<bool> TryEnterGateAsync()
		{
			try
			{
				await _gate.WaitAsync();
				return true;
			}
			catch (ObjectDisposedException)
			{
				return false;
			}
		}

		private void ExitGate()
		{
			try
			{
				_gate.Release();
			}
			catch (ObjectDisposedException)
			{
			}
		}

		private sealed class Booth
		{
			private readonly string _key;
			private readonly GameObject _root;
			private readonly bool _ownsTexture;
			private readonly List<Attachment> _attachments = new();
			private RawImage _boundImage;
			private bool _destroyed;

			public Booth(string key, GameObject root, ModelPreviewCamera camera, GameObject model,
			             RenderTexture texture, bool ownsTexture, ModelPreview preview, ModelPreviewOptions options)
			{
				_key = key;
				_root = root;
				Camera = camera;
				Model = model;
				Texture = texture;
				_ownsTexture = ownsTexture;
				Preview = preview;
				Options = options;
			}

			public ModelPreview Preview { get; }
			public RenderTexture Texture { get; }
			public ModelPreviewCamera Camera { get; private set; }
			public GameObject Model { get; private set; }
			public ModelPreviewOptions Options { get; }
			public ModelPreviewInteractable Interactable { get; set; }
			public bool InteractableOwned { get; set; }

			public void ReplaceModel(GameObject prefab, float framingMargin)
			{
				if (_destroyed || Camera == null)
				{
					return;
				}

				if (Model != null)
				{
					Object.Destroy(Model);
				}

				Model = SpawnModel(Camera, prefab);
				if (Model == null)
				{
					Debug.LogError($"{nameof(ModelPreviewSession)}: replacement model for '{_key}' has no renderers.");
					return;
				}

				Camera.Frame(ComputeBounds(Model), framingMargin);
			}

			public void Attach(GameObject decoration, bool changeLayer)
			{
				if (_destroyed || decoration == null || Camera == null || Camera.Pivot == null)
				{
					return;
				}

				var attachment = new Attachment(decoration);
				_attachments.Add(attachment);

				if (changeLayer)
				{
					int layer = FirstSetLayer(Camera.ModelLayer);
					if (layer >= 0)
					{
						attachment.ApplyLayer(layer);
					}
				}

				// Parented to the stage root, NOT the pivot — the pivot is the model's turntable
				// and decorations stay put while it spins. Placed at the model's position.
				decoration.transform.SetParent(_root.transform, true);
				decoration.transform.position = Camera.Pivot.position;
				decoration.transform.rotation = Quaternion.identity;
			}

			public void Detach(GameObject decoration)
			{
				if (decoration == null)
				{
					return;
				}

				for (int i = _attachments.Count - 1; i >= 0; i--)
				{
					if (_attachments[i].Root == decoration)
					{
						_attachments[i].Restore();
						_attachments.RemoveAt(i);
					}
				}
			}

			public void DetachAll()
			{
				for (int i = _attachments.Count - 1; i >= 0; i--)
				{
					_attachments[i].Restore();
				}

				_attachments.Clear();
			}

			public void Bind(RawImage image)
			{
				if (_destroyed || image == null)
				{
					return;
				}

				if (_boundImage != null && _boundImage != image)
				{
					_boundImage.texture = null;
				}

				_boundImage = image;
				_boundImage.texture = Texture;
			}

			public void Unbind()
			{
				if (_boundImage != null)
				{
					if (_boundImage.texture == Texture)
					{
						_boundImage.texture = null;
					}

					_boundImage = null;
				}
			}

			public void Destroy()
			{
				if (_destroyed)
				{
					return;
				}

				_destroyed = true;

				// Decorations are caller-owned (e.g. pooled particles) — restore their layers
				// and release them BEFORE the stage goes away, never destroy them with it.
				DetachAll();

				Unbind();

				if (InteractableOwned && Interactable != null)
				{
					Object.Destroy(Interactable);
				}

				Interactable = null;

				if (Camera != null)
				{
					if (Camera.Camera != null)
					{
						Camera.Camera.enabled = false;
						Camera.Camera.targetTexture = null;
					}

					Camera.SetTargetTexture(null);
				}

				if (_root != null)
				{
					Object.Destroy(_root);
				}

				if (_ownsTexture)
				{
					ReleaseTexture(Texture);
				}
			}

			/// <summary>
			/// Tracks a caller-owned decoration and its original per-child layers so detach can
			/// put everything back — a pooled particle returns to its pool exactly as it left.
			/// </summary>
			private sealed class Attachment
			{
				private readonly GameObject _root;
				private readonly Transform _originalParent;
				private readonly List<KeyValuePair<Transform, int>> _originalLayers = new();

				public Attachment(GameObject root)
				{
					_root = root;
					_originalParent = root.transform.parent;
					foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
					{
						_originalLayers.Add(new KeyValuePair<Transform, int>(child, child.gameObject.layer));
					}
				}

				public GameObject Root => _root;

				public void ApplyLayer(int layer)
				{
					foreach (KeyValuePair<Transform, int> pair in _originalLayers)
					{
						if (pair.Key != null)
						{
							pair.Key.gameObject.layer = layer;
						}
					}
				}

				public void Restore()
				{
					foreach (KeyValuePair<Transform, int> pair in _originalLayers)
					{
						if (pair.Key != null)
						{
							pair.Key.gameObject.layer = pair.Value;
						}
					}

					// Back to the original parent (the spawner), not the scene root — a pooled
					// particle returns exactly where it lives.
					if (_root != null)
					{
						_root.transform.SetParent(_originalParent, true);
					}
				}
			}
		}
	}
}
