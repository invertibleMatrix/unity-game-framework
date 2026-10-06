using System.Collections.Generic;
using AK.Core.Extensions;
using AK.Kernel.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AK.Utilities.Previews
{
	/// <summary>
	/// One offscreen booth: a stage instance, its camera, the model on its turntable, the texture
	/// it renders into, and the image showing it. The booth owns the stage and model instances,
	/// the texture when it created it, and an interactable it added. Decorations stay the
	/// caller's. Asset claims are the session's business; the booth only remembers what its
	/// stage and model came from.
	/// </summary>
	internal sealed class ModelPreviewBooth
	{
		private readonly List<Attachment> _attachments = new();
		private readonly List<Light> _directionalLights = new();
		private ModelPreviewInteractable _interactable;
		private bool _ownsInteractable;

		public ModelPreviewBooth(ModelPreviewSession session, string key, GameObject stageAsset, GameObject root, ModelPreviewCamera camera)
		{
			Session    = session;
			Key        = key;
			StageAsset = stageAsset;
			Root       = root;
			Camera     = camera;

			// The directional lights the stage has on, as authored. Lights authored off stay off.
			root.GetComponentsInChildren(true, _directionalLights);
			for (int i = _directionalLights.Count - 1; i >= 0; i--)
			{
				Light light = _directionalLights[i];
				if (light.type != LightType.Directional || !light.enabled)
				{
					_directionalLights.RemoveAt(i);
				}
			}
		}

		public ModelPreviewSession Session { get; }
		public string Key { get; }

		/// <summary>The stage prefab <see cref="Root"/> was instantiated from.</summary>
		public GameObject StageAsset { get; }

		public GameObject Root { get; }
		public ModelPreviewCamera Camera { get; }

		public Handle<ModelPreviewBooth> Handle { get; set; }
		public ModelPreview Preview { get; set; }
		public ModelPreviewOptions Options { get; set; }

		public RenderTexture Texture { get; private set; }
		public bool OwnsTexture { get; private set; }

		/// <summary>The model instance on the turntable.</summary>
		public GameObject Model { get; private set; }

		/// <summary>The prefab <see cref="Model"/> was instantiated from.</summary>
		public GameObject ModelAsset { get; private set; }

		/// <summary>The address the session loaded <see cref="ModelAsset"/> from; null for a caller-supplied prefab.</summary>
		public string ModelAddress { get; private set; }

		public RawImage Image { get; private set; }
		public bool IsDestroyed { get; private set; }

		/// <summary>True when the booth already shows the model this source names.</summary>
		public bool Shows(string address, GameObject prefab)
		{
			return address != null
				? string.Equals(address, ModelAddress, System.StringComparison.Ordinal)
				: ModelAddress == null && ModelAsset == prefab;
		}

		public void SetTexture(RenderTexture texture, bool owned)
		{
			Texture     = texture;
			OwnsTexture = owned;
		}

		/// <summary>
		/// Turns the stage's directional lights on or off. A directional light reaches every
		/// model on its layers, whichever booth the model is in, so the stage space keeps only
		/// one booth's lights on.
		/// </summary>
		public void SetDirectionalLights(bool on)
		{
			foreach (Light light in _directionalLights)
			{
				if (light != null)
				{
					light.enabled = on;
				}
			}
		}

		/// <summary>
		/// Puts <paramref name="model"/> on the turntable and destroys the instance it replaces.
		/// Hands back what the old model came from, so the caller can give its claim back.
		/// </summary>
		public void ReplaceModel(GameObject model, GameObject asset, string address,
		                         out GameObject previousAsset, out string previousAddress)
		{
			previousAsset   = ModelAsset;
			previousAddress = ModelAddress;

			if (Model != null)
			{
				Model.DestroyInAnyMode();
			}

			Model        = model;
			ModelAsset   = asset;
			ModelAddress = address;
		}

		/// <summary>Shows the booth's texture on <paramref name="image"/>, taking it from any image bound before.</summary>
		public void Bind(RawImage image)
		{
			if (IsDestroyed || image == null)
			{
				return;
			}

			if (Image != null && Image != image)
			{
				Unbind();
			}

			Image         = image;
			Image.texture = Texture;
		}

		public void Unbind()
		{
			ReleaseInteractable();

			if (Image != null && Image.texture == Texture)
			{
				Image.texture = null;
			}

			Image = null;
		}

		/// <summary>
		/// Turns drag-to-rotate and pinch or scroll zoom on the bound image on or off. Uses a
		/// <see cref="ModelPreviewInteractable"/> already on the image, or adds one it then owns.
		/// </summary>
		public void SetInteractive(bool interactive)
		{
			if (!interactive || IsDestroyed || Image == null)
			{
				ReleaseInteractable();
				return;
			}

			if (_interactable != null && _interactable.gameObject == Image.gameObject)
			{
				return;
			}

			ReleaseInteractable();

			_interactable     = Image.GetComponent<ModelPreviewInteractable>();
			_ownsInteractable = _interactable == null;
			if (_ownsInteractable)
			{
				_interactable = Image.gameObject.AddComponent<ModelPreviewInteractable>();
			}

			_interactable.Init(Camera.RotateBy, Camera.ZoomBy);
		}

		public void Attach(GameObject decoration, bool changeLayer, int modelLayer)
		{
			if (IsDestroyed || decoration == null || Camera.Pivot == null)
			{
				return;
			}

			var attachment = new Attachment(decoration);
			_attachments.Add(attachment);

			if (changeLayer && modelLayer >= 0)
			{
				attachment.ApplyLayer(modelLayer);
			}

			// Parented to the stage root, NOT the pivot — the pivot is the model's turntable
			// and decorations stay put while it spins. Placed at the model's position.
			decoration.transform.SetParent(Root.transform, true);
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

		/// <summary>
		/// Tears the booth down: decorations restored to their owners, image unbound, owned
		/// interactable and texture released, stage (and the model on it) destroyed. Idempotent.
		/// </summary>
		public void Destroy()
		{
			if (IsDestroyed)
			{
				return;
			}

			IsDestroyed = true;

			// Decorations are caller-owned (e.g. pooled particles) — restore their layers
			// and parents BEFORE the stage goes away, never destroy them with it.
			DetachAll();
			Unbind();

			if (Camera != null && Camera.Camera != null)
			{
				Camera.Camera.enabled       = false;
				Camera.Camera.targetTexture = null;
			}

			Root.DestroyInAnyMode();
			Model = null;

			if (OwnsTexture && Texture != null)
			{
				if (Texture.IsCreated())
				{
					Texture.Release();
				}

				Texture.DestroyInAnyMode();
			}
		}

		private void ReleaseInteractable()
		{
			// Unity null: an interactable destroyed with its image needs nothing.
			if (_interactable != null)
			{
				if (_ownsInteractable)
				{
					_interactable.DestroyInAnyMode();
				}
				else
				{
					_interactable.Init(null, null);
				}
			}

			_interactable     = null;
			_ownsInteractable = false;
		}

		/// <summary>
		/// Tracks a caller-owned decoration and its original per-child layers so detach can
		/// put everything back — a pooled particle returns to its pool exactly as it left.
		/// </summary>
		private sealed class Attachment
		{
			private readonly Transform _originalParent;
			private readonly List<KeyValuePair<Transform, int>> _originalLayers = new();

			public Attachment(GameObject root)
			{
				Root            = root;
				_originalParent = root.transform.parent;
				foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
				{
					_originalLayers.Add(new KeyValuePair<Transform, int>(child, child.gameObject.layer));
				}
			}

			public GameObject Root { get; }

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
				if (Root != null)
				{
					Root.transform.SetParent(_originalParent, true);
				}
			}
		}
	}
}
