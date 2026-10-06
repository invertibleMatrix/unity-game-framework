using System;
using AK.Kernel.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AK.Utilities.Previews
{
	/// <summary>
	/// A handle to one open booth of a <see cref="ModelPreviewSession"/>. Once the booth is gone
	/// (disposed here, released by key, rebuilt around another texture, or closed with its
	/// session) every call is a no-op, even if a new booth takes the same key;
	/// <see cref="IsValid"/> tells.
	/// </summary>
	public sealed class ModelPreview : IDisposable
	{
		private readonly ModelPreviewSession _session;
		private readonly Handle<ModelPreviewBooth> _booth;

		internal ModelPreview(ModelPreviewSession session, Handle<ModelPreviewBooth> booth, string key,
		                      RenderTexture texture, bool ownsTexture)
		{
			_session    = session;
			_booth      = booth;
			Key         = key;
			Texture     = texture;
			OwnsTexture = ownsTexture;
		}

		public string Key { get; }

		/// <summary>The texture the booth renders into.</summary>
		public RenderTexture Texture { get; }

		/// <summary>True when the session created <see cref="Texture"/>; it is destroyed with the booth.</summary>
		public bool OwnsTexture { get; }

		/// <summary>True while the booth behind this preview is open.</summary>
		public bool IsValid => _session.IsCurrent(_booth);

		public void Bind(RawImage image) => _session.Bind(_booth, image);
		public void Unbind() => _session.Unbind(_booth);

		public void RotateBy(float yawDelta, float pitchDelta) => _session.RotateBy(_booth, yawDelta, pitchDelta);

		/// <summary>Multiplies the camera distance: below 1 moves closer, above 1 moves away.</summary>
		public void ZoomBy(float factor) => _session.ZoomBy(_booth, factor);

		public void ResetView() => _session.ResetView(_booth);

		/// <summary>
		/// Attaches a caller-owned decoration (e.g. a pooled particle): parents it to the stage
		/// root at the model's position — it does NOT rotate with the model. With
		/// <paramref name="changeLayer"/>, its layers are swapped to the model layer so the booth
		/// camera renders it (required unless it's already on that layer). Ownership stays with
		/// the caller — it is auto-detached (layers and parent restored, never destroyed) on
		/// <see cref="Dispose"/>, session dispose, or <see cref="Detach"/>.
		/// </summary>
		public void Attach(GameObject decoration, bool changeLayer = false) => _session.Attach(_booth, decoration, changeLayer);

		/// <summary>Restores a decoration's original layers and parent, mid-preview. Never destroys it.</summary>
		public void Detach(GameObject decoration) => _session.Detach(_booth, decoration);

		public void DetachAll() => _session.DetachAll(_booth);

		/// <summary>Closes the booth, cancelling anything in flight on its key. Idempotent.</summary>
		public void Dispose() => _session.Release(_booth);
	}
}
