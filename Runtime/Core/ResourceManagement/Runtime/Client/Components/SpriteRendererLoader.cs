using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Core.ResourceManagement
{
	[RequireComponent(typeof(SpriteRenderer))]
	public sealed class SpriteRendererLoader : SpriteLoadingComponent
	{
		[SerializeField, BoxGroup] private SpriteRenderer _renderer = default;

		public override Sprite Sprite => _renderer.sprite;

		public override void SetSprite(Sprite sprite) => _renderer.sprite = sprite;

		public override void DisposeAsset()
		{
			base.DisposeAsset();
			_renderer.sprite = null;
		}

		private void Reset() => _renderer = this.GetComponent<SpriteRenderer>();
	}
}