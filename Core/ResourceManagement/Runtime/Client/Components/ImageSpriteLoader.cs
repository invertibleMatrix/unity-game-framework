using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace AK.Core.ResourceManagement
{
	[RequireComponent(typeof(Image))]
	public class ImageSpriteLoader : SpriteLoadingComponent
	{
		[SerializeField, BoxGroup] protected Image _image = default;
		[SerializeField, BoxGroup] protected bool _setNativeSize = false;
		[SerializeField, BoxGroup] protected bool _hideWhenLoading = true;

		public override Sprite Sprite => _image.sprite;

		public Image Image => _image;
		
		public override UniTask<Sprite> LoadSpriteAsync(SpriteLoadingParams @params, CancellationToken cToken = default)
		{
			if (_hideWhenLoading) _image.enabled = false;
			return base.LoadSpriteAsync(@params, cToken);
		}

		public override void SetSprite(Sprite sprite)
		{
			_image.sprite = sprite;
			_image.enabled = true;
			if (_setNativeSize) _image.SetNativeSize();
		}

		public override void DisposeAsset()
		{
			base.DisposeAsset();
			_image.sprite = null;
		}
		
		private void Reset() => _image = this.GetComponent<Image>();
	}
}