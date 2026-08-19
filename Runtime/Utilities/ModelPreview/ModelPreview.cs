using System;
using UnityEngine;
using UnityEngine.UI;

namespace Utilities.ModelPreview
{
	/// <summary>
	/// One live model preview owned by a <see cref="ModelPreviewSession"/>.
	/// Valid until <see cref="Dispose"/> or session release.
	/// </summary>
	public sealed class ModelPreview : IDisposable
	{
		private readonly ModelPreviewSession _session;
		private bool _disposed;

		internal ModelPreview(ModelPreviewSession session, string key, RenderTexture texture, bool ownsTexture)
		{
			_session = session;
			Key = key;
			Texture = texture;
			OwnsTexture = ownsTexture;
		}

		public string Key { get; }
		public RenderTexture Texture { get; }
		public bool OwnsTexture { get; }

		public void Bind(RawImage image) => _session.Bind(Key, image);
		public void Unbind() => _session.Unbind(Key);

		public void RotateBy(float yawDelta, float pitchDelta) => _session.RotateBy(Key, yawDelta, pitchDelta);
		public void ZoomBy(float factor) => _session.ZoomBy(Key, factor);
		public void ResetView() => _session.ResetView(Key);

		/// <summary>
		/// Attaches a caller-owned decoration (e.g. a pooled particle): parents it to the stage
		/// root at the model's position — it does NOT rotate with the model. With
		/// <paramref name="changeLayer"/>, its layers are swapped to the model layer so the booth
		/// camera renders it (required unless it's already on that layer). Ownership stays with
		/// the caller — it is auto-detached (layers restored, never destroyed) on
		/// <see cref="Dispose"/>, session dispose, or <see cref="Detach"/>.
		/// Call only after the LoadAsync that produced this handle has resolved.
		/// </summary>
		public void Attach(GameObject decoration, bool changeLayer = false) => _session.Attach(Key, decoration, changeLayer);

		/// <summary>Restores a decoration's original layers and unparents it, mid-preview. Never destroys it.</summary>
		public void Detach(GameObject decoration) => _session.Detach(Key, decoration);

		public void DetachAll() => _session.DetachAll(Key);

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_session.Release(Key);
		}
	}
}
