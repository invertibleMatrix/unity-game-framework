using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Core.ResourceManagement
{
	/// <summary>
	/// An abstract base class for components that manage asynchronous sprite loading and resource handling.
	/// </summary>
	public abstract class SpriteLoadingComponent : MonoBehaviour
	{
		[SerializeField] private bool _runOnStart = false;
		[SerializeField] private SpriteLoadingParams _params = SpriteLoadingParams.Default;

		protected bool _canDispose = default;
		private CancellationTokenSource _cTokenSource = default;

		private void Start()
		{
			if (_runOnStart) LoadSprite();
		}

		/// <summary>
		/// Returns The Current Sprite Loaded By This Component...
		/// </summary>
		public abstract Sprite Sprite { get; }

		/// <summary>
		/// <see cref="HasPrimaryKey"/> Returns Whether The Primary Key Is Authored Or Not...
		/// </summary>
		public bool HasPrimaryKey() => _params.HasPrimaryKey();

		/// <summary>
		/// Initiates asynchronous sprite loading using the configured parameters and discards the task result.
		/// </summary>
		[Button]
		public virtual void LoadSprite(string key = default, CancellationToken cToken = default)
			=> LoadSpriteAsync(key, cToken).Forget();

		/// <summary>
		/// Asynchronously loads a sprite using the provided key.
		/// </summary>
		/// <param name="key">The key to identify the sprite to load.</param>
		/// <param name="cToken">Cancellation token for loading cancellation.</param>
		/// <returns>The loaded sprite.</returns>
		public virtual UniTask<Sprite> LoadSpriteAsync(string key = default, CancellationToken cToken = default)
		{
			return LoadSpriteAsync(SpriteLoadingParams.FromKey(_params.GenerateKey(key)), cToken);
		}

		/// <summary>
		/// Asynchronously loads a sprite using the provided parameters.
		/// </summary>
		/// <param name="params">The sprite loading parameters.</param>
		/// <param name="cToken">Cancellation token for loading cancellation.</param>
		/// <returns>The loaded sprite.</returns>
		public virtual async UniTask<Sprite> LoadSpriteAsync(SpriteLoadingParams @params,
			CancellationToken cToken = default)
		{
			DisposeAsset();
			cToken = CheckToken(cToken);

			try
			{
				var sprite = await UniResources.LoadAssetAsync<Sprite>(@params.Key, cToken: cToken);
				_canDispose = true;
				SetSprite(sprite);
				return sprite;
			}
			catch (Exception _)
			{
				return default;
			}
		}

		/// <summary>
		/// Invoked when the sprite needs to be disposed.
		/// </summary>
		[Button]
		public virtual void DisposeAsset()
		{
			if (_canDispose == false) return;

			_canDispose = false;
			UniResources.DisposeAsset(Sprite);
		}

		/// <summary>
		/// Set Sprite will dispose previous sprite if <see cref="disposePrev"/> is true and
		/// call <see cref="SetSprite(UnityEngine.Sprite)"/>
		/// </summary>
		public virtual void SetSprite(Sprite sprite, bool disposePrev)
		{
			if (disposePrev) DisposeAsset();
			SetSprite(sprite);
		}

		/// <summary>
		/// Invoked when the sprite is loaded and ready for use.
		/// </summary>
		/// <param name="sprite">The loaded sprite.</param>
		public abstract void SetSprite(Sprite sprite);

		private void OnDestroy()
		{
			StopLoading();
			DisposeAsset();
		}

		protected CancellationToken CheckToken(CancellationToken cToken)
		{
			if (cToken == default)
			{
				_cTokenSource = new CancellationTokenSource();
				cToken = _cTokenSource.Token;
			}

			return cToken;
		}

		// ReSharper disable once MemberCanBePrivate.Global
		protected void StopLoading()
		{
			if (_cTokenSource == null) return;

			_cTokenSource.Cancel();
			_cTokenSource.Dispose();
			_cTokenSource = null;
		}

#if UNITY_EDITOR
		[Button]
		private void SetSpriteNameAsPrimaryKey()
		{
			if (Sprite == null) return;
			_params.SetPrimaryKey(Sprite.name);
		}
#endif
	}
}