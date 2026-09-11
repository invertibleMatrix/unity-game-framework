using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AK.Systems
{
	/// <summary>
	/// A full-canvas dim behind a view. Put it on a <see cref="UIView"/> and the view fades it
	/// in when it shows and out when it hides; tutorial mode adds one on demand to dim
	/// everything except the highlighted view. The dim is a runtime-built Image that is
	/// oversized relative to the nearest Canvas so it covers the screen even when the view
	/// itself is small.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ViewBackgroundOverlay : MonoBehaviour
	{
		public const float DefaultAlpha = 0.95f;

		[SerializeField, Range(0f, 1f)] private float _alpha = DefaultAlpha;
		[SerializeField, Tooltip("Whether the dim swallows pointer input to whatever is behind the view.")]
		private bool _blockRaycasts = true;
		[SerializeField] private float _fadeInDuration = 0.4f;
		[SerializeField] private float _fadeOutDuration = 0.1f;

		private GameObject _dim;
		private Image      _image;
		private Texture2D  _texture;
		private Sprite     _sprite;

		/// <summary>The dim has been built and not yet destroyed.</summary>
		public bool IsBuilt => _dim != null;

		/// <summary>Fades the dim in with the serialized settings, building it on first use.</summary>
		public void FadeIn()
		{
			FadeIn(_alpha, _blockRaycasts);
		}

		/// <summary>Fades the dim in to <paramref name="alpha"/>, building it on first use.</summary>
		public void FadeIn(float alpha, bool blockRaycasts)
		{
			if (_dim == null) Build();

			_image.raycastTarget = blockRaycasts;
			_image.DOKill();
			_image.color = new Color(0f, 0f, 0f, _image.color.a);
			_image.DOFade(alpha, _fadeInDuration);
		}

		/// <summary>Fades the dim out and stops it blocking input. Keeps it built for the next fade in.</summary>
		public void FadeOut()
		{
			FadeOut(_fadeOutDuration);
		}

		/// <summary>
		/// Fades the dim out over <paramref name="duration"/>; <paramref name="onComplete"/> runs
		/// when it is fully transparent. A <see cref="FadeIn()"/> before then kills the fade and
		/// drops the callback.
		/// </summary>
		public void FadeOut(float duration, TweenCallback onComplete = null)
		{
			if (_image == null)
			{
				onComplete?.Invoke();
				return;
			}

			_image.raycastTarget = false;
			_image.DOKill();
			var tween = _image.DOFade(0f, duration);
			if (onComplete != null) tween.OnComplete(onComplete);
		}

		/// <summary>Destroys the dim and its texture. The next <see cref="FadeIn()"/> rebuilds them.</summary>
		public void Clear()
		{
			if (_image != null) _image.DOKill();

			DestroySafely(_dim);
			DestroySafely(_sprite);
			DestroySafely(_texture);

			_dim = null;
			_image = null;
			_sprite = null;
			_texture = null;
		}

		private void OnDestroy()
		{
			Clear();
		}

		private void Build()
		{
			_texture = new Texture2D(1, 1);
			_texture.SetPixel(0, 0, Color.black);
			_texture.Apply();

			_dim = new GameObject("ViewBackground");
			_dim.AddComponent<PointerEventSink>();
			_image = _dim.AddComponent<Image>();

			Transform t = _dim.transform;
			t.SetParent(transform, false);
			t.SetSiblingIndex(0);
			t.localScale = Vector3.one;

			CoverNearestCanvas(_image.rectTransform);

			// Never assign image.material.mainTexture — Image.material returns the shared default
			// UI material, so that would tint every other Image using m_Material: None. The sprite
			// is enough.
			_sprite = Sprite.Create(_texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
			_image.sprite = _sprite;
			_image.color = new Color(0f, 0f, 0f, 0f);
		}

		/// <summary>Sizes the dim to four times the nearest Canvas so it stays covering under any layout the view lands in.</summary>
		private void CoverNearestCanvas(RectTransform dimRect)
		{
			Canvas canvas = GetComponentInParent<Canvas>();
			if (canvas == null) return;

			var self = (RectTransform)transform;
			var canvasCorners = new Vector3[4];
			canvas.GetComponent<RectTransform>().GetWorldCorners(canvasCorners);

			Vector2 bottomLeft = self.InverseTransformPoint(canvasCorners[0]);
			Vector2 topRight = self.InverseTransformPoint(canvasCorners[2]);

			dimRect.anchorMin = new Vector2(0.5f, 0.5f);
			dimRect.anchorMax = new Vector2(0.5f, 0.5f);
			dimRect.pivot = new Vector2(0.5f, 0.5f);
			dimRect.anchoredPosition = (bottomLeft + topRight) * 0.5f;
			dimRect.sizeDelta = (topRight - bottomLeft) * 4f;
		}

		private static void DestroySafely(Object o)
		{
			if (o == null) return;
			if (Application.isPlaying) Destroy(o);
			else DestroyImmediate(o);
		}
	}

	/// <summary>
	/// Eats pointer click, drag, and scroll events so they don't propagate up the transform
	/// hierarchy to a ScrollRect or a Button below the dim. Empty handlers — the sole purpose is
	/// to be the first component that ExecuteHierarchy finds walking up from the raycast hit.
	/// </summary>
	internal sealed class PointerEventSink : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
	{
		public void OnDrag(PointerEventData eventData) { }
		public void OnBeginDrag(PointerEventData eventData) { }
		public void OnEndDrag(PointerEventData eventData) { }
		public void OnScroll(PointerEventData eventData) { }
		public void OnPointerClick(PointerEventData eventData) { }
	}
}
