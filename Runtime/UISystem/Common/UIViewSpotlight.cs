using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AK.Systems
{
	[RequireComponent(typeof(Image), typeof(GraphicRaycaster))]
	public class UIViewSpotlight : UIView, ICanvasRaycastFilter, IPointerClickHandler
	{
		private const int MAX_HOLES = 8;

		private static readonly int HolesProperty     = Shader.PropertyToID("_Holes");
		private static readonly int HoleCountProperty = Shader.PropertyToID("_HoleCount");
		private static readonly int FeatherProperty   = Shader.PropertyToID("_Feather");

		[SerializeField] private RectTransform _furnitureRoot;
		[SerializeField] private float         _padding = 20f;
		[SerializeField] private float         _feather = 15f;
		[SerializeField] private float         _introDuration = 0.6f;
		[SerializeField] private Ease          _introEase = Ease.OutCubic;

		private readonly List<RectTransform> _targets = new();
		private readonly Vector4[]           _holes   = new Vector4[MAX_HOLES];
		private readonly Vector3[]           _corners = new Vector3[4];

		private Image    _dimImage;
		private Material _materialInstance;
		private int      _holeCount;
		private Tween    _introTween;
		private float    _introT = 1f;
		private bool     _introActive;

		public RectTransform FurnitureRoot => _furnitureRoot;

		public event Action BackgroundTapped;

		private void Awake()
		{
			_dimImage = GetComponent<Image>();

			// Prefabs created before the RequireComponent existed may miss this.
			if (GetComponent<GraphicRaycaster>() == null)
			{
				gameObject.AddComponent<GraphicRaycaster>();
			}

			if (_dimImage.material != null)
			{
				_materialInstance = new Material(_dimImage.material);
				_dimImage.material = _materialInstance;
			}
		}

		public void SetTargets(IReadOnlyList<RectTransform> targets, bool animateSpotlight = true)
		{
			_targets.Clear();
			if (targets != null)
			{
				foreach (var target in targets)
				{
					if (target != null)
					{
						_targets.Add(target);
					}
				}
			}

			_introTween?.Kill();
			if (animateSpotlight)
			{
				_introActive = true;
				_introT = 0f;
				_introTween = DOTween.To(() => _introT, v => _introT = v, 1f, _introDuration)
				                     .SetEase(_introEase)
				                     .SetTarget(this)
				                     .OnComplete(() => _introActive = false)
				                     .Play();
			}
			else
			{
				_introActive = false;
				_introT = 1f;
			}
		}

		public void AttachFurniture(RectTransform furniture)
		{
			if (furniture == null || _furnitureRoot == null) return;
			furniture.SetParent(_furnitureRoot, true);
		}

		public override void OnPrepareShow()
		{
			base.OnPrepareShow();
			SetInteractable(true);
		}

		public override void OnPrepareHide()
		{
			base.OnPrepareHide();
			SetInteractable(false);
			_introTween?.Kill();
			_introActive = false;
			_introT = 1f;
		}

		private void Update()
		{
			if (_materialInstance != null)
			{
				PushHolesToMaterial();
			}
		}

		protected override void OnDestroy()
		{
			if (_materialInstance != null)
			{
				if (Application.isPlaying) Destroy(_materialInstance);
				else DestroyImmediate(_materialInstance);
				_materialInstance = null;
			}

			base.OnDestroy();
		}

		public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
		{
			return !IsInsideAnyHole(screenPoint);
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			BackgroundTapped?.Invoke();
		}

		private void PushHolesToMaterial()
		{
			int count = 0;
			for (int i = 0; i < _targets.Count && count < MAX_HOLES; i++)
			{
				var target = _targets[i];
				if (target == null) continue;

				// Project with the camera that renders the target — the spotlight's own
				// canvas may have a different render mode/camera.
				Camera cam = GetTargetCamera(target);
				target.GetWorldCorners(_corners);

				Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);
				Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(cam, _corners[1]);
				Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, _corners[2]);

				Vector2 center = (bottomLeft + topRight) * 0.5f;
				float width = Vector2.Distance(topLeft, topRight);
				float height = Vector2.Distance(bottomLeft, topLeft);
				float radius = Mathf.Max(width, height) * 0.5f + _padding;

				if (_introActive)
				{
					radius = Mathf.Lerp(GetCoveringRadius(center), radius, _introT);
				}

				_holes[count] = new Vector4(center.x, center.y, 0f, radius);
				count++;
			}

			_holeCount = count;
			_materialInstance.SetVectorArray(HolesProperty, _holes);
			_materialInstance.SetFloat(HoleCountProperty, count);
			_materialInstance.SetFloat(FeatherProperty, _feather);
		}

		private static Camera GetTargetCamera(RectTransform target)
		{
			var canvas = target.GetComponentInParent<Canvas>();
			if (canvas == null) return null;

			canvas = canvas.rootCanvas;
			return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
		}

		// Radius that keeps the hole clear over every pixel — distance to the farthest
		// screen corner, plus feather so the rim sits fully off-screen.
		private float GetCoveringRadius(Vector2 center)
		{
			Vector2 screen = new Vector2(Screen.width, Screen.height);

			float max = 0f;
			max = Mathf.Max(max, Vector2.Distance(center, Vector2.zero));
			max = Mathf.Max(max, Vector2.Distance(center, new Vector2(screen.x, 0f)));
			max = Mathf.Max(max, Vector2.Distance(center, new Vector2(0f, screen.y)));
			max = Mathf.Max(max, Vector2.Distance(center, screen));

			return max + _feather;
		}

		private bool IsInsideAnyHole(Vector2 screenPos)
		{
			for (int i = 0; i < _holeCount; i++)
			{
				float dx = screenPos.x - _holes[i].x;
				float dy = screenPos.y - _holes[i].y;
				float radius = _holes[i].w;
				if (dx * dx + dy * dy <= radius * radius)
				{
					return true;
				}
			}

			return false;
		}
	}
}
