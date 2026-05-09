using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace AK.UISystem
{
	/// <summary>
	/// Helper component for displaying a tutorial arrow that points to a target UI element.
	/// Spawns an arrow as a child of this GameObject that moves toward a target and oscillates until dismissed.
	/// </summary>
	public class UITutorialArrow : MonoBehaviour
	{
		public enum ArrowSpawnPosition
		{
			TopLeft,
			TopRight,
			BottomLeft,
			BottomRight,
			Center,
			Custom
		}

		[Title("Arrow Settings")]
		[SerializeField, Tooltip("The root canvas for calculating screen positions. If null, will try to find the parent canvas.")]
		private Canvas _rootCanvas;

		[SerializeField, Tooltip("The arrow sprite to spawn. Should point UP by default.")]
		private Sprite _arrowSprite;

		[SerializeField, Tooltip("Predefined spawn position for the arrow (relative to screen)")]
		private ArrowSpawnPosition _spawnPosition = ArrowSpawnPosition.Center;

		[SerializeField, ShowIf("@_spawnPosition == ArrowSpawnPosition.Custom")]
		private Vector2 _customSpawnOffset;

		[Title("Animation Settings")]
		[SerializeField, Tooltip("Target RectTransform to point the arrow at")]
		private RectTransform _target;

		[SerializeField, Tooltip("Distance from target to maintain while oscillating")]
		private float _oscillationDistance = 200f;

		[SerializeField, Tooltip("Duration of one oscillation cycle")]
		private float _oscillationDuration = 2f;

		[SerializeField, Tooltip("Duration for arrow to shrink when dismissed")]
		private float _dismissDuration = 0.3f;

		[SerializeField, Tooltip("Duration for arrow to move to target on spawn")]
		private float _moveToTargetDuration = 1f;

		[SerializeField, Tooltip("Size of the arrow in pixels")]
		private Vector2 _arrowSize = new Vector2(128f, 128f);

		private GameObject _arrowInstance;
		private RectTransform _arrowRect;
		private Image _arrowImage;
		private RectTransform _canvasRect;
		private Tween _moveTween;
		private Tween _oscillationTween;
		private Tween _dismissTween;
		private Tween _rotationTween;

		public void SetRootCanvas(Canvas canvas)
		{
			_rootCanvas = canvas;
		}
		
		/// <summary>
		/// Shows the tutorial arrow, spawning it and animating it toward the target.
		/// The arrow will oscillate near the target until dismissed.
		/// </summary>
		[Button]
		public void ShowArrow()
		{
			DismissArrow();

			if (_arrowSprite == null)
			{
				Debug.LogWarning("Arrow sprite is not assigned!", this);
				return;
			}

			Canvas canvas = GetRootCanvas();
			if (canvas == null)
			{
				Debug.LogError("No canvas found for tutorial arrow!", this);
				return;
			}

			_canvasRect = canvas.GetComponent<RectTransform>();

			// Create arrow as child of this GameObject so it appears above the background overlay
			_arrowInstance = new GameObject("TutorialArrow");
			_arrowInstance.transform.SetParent(transform, false);

			_arrowImage = _arrowInstance.AddComponent<Image>();
			_arrowImage.sprite = _arrowSprite;
			_arrowImage.SetNativeSize();

			_arrowRect = _arrowInstance.GetComponent<RectTransform>();
			_arrowRect.sizeDelta = _arrowSize;
			_arrowRect.pivot = new Vector2(UIConstants.DEFAULT_PIVOT, UIConstants.DEFAULT_PIVOT);
			_arrowRect.anchorMin = new Vector2(UIConstants.DEFAULT_PIVOT, UIConstants.DEFAULT_PIVOT);
			_arrowRect.anchorMax = new Vector2(UIConstants.DEFAULT_PIVOT, UIConstants.DEFAULT_PIVOT);

			// Set initial position (screen-relative, converted to local space)
			SetSpawnPosition();

			// Start with zero scale and fade in
			_arrowRect.localScale = Vector3.zero;
			_arrowImage.color = new Color(UIConstants.FULL_ALPHA, UIConstants.FULL_ALPHA, UIConstants.FULL_ALPHA, UIConstants.ZERO_ALPHA);

			_arrowRect.DOScale(Vector3.one, _moveToTargetDuration).SetEase(Ease.OutBack).Play();
			_arrowImage.DOFade(UIConstants.FULL_ALPHA, _moveToTargetDuration).Play();

			if (_target != null)
			{
				RotateTowardsTarget();
				MoveToTargetAndStartOscillation();
			}
		}

		/// <summary>
		/// Dismisses the arrow with a shrink animation.
		/// </summary>
		[Button]
		public void DismissArrow()
		{
			_moveTween?.Kill();
			_oscillationTween?.Kill();
			_dismissTween?.Kill();
			_rotationTween?.Kill();

			if (_arrowInstance == null)
			{
				return;
			}

			_dismissTween = _arrowRect.DOScale(Vector3.zero, _dismissDuration)
				.SetEase(Ease.InBack)
				.OnComplete(() =>
				{
					if (_arrowInstance != null)
					{
						Destroy(_arrowInstance);
						_arrowInstance = null;
						_arrowRect = null;
						_arrowImage = null;
						_canvasRect = null;
					}
				})
				.Play();

			if (_arrowImage != null)
			{
				_arrowImage.DOFade(UIConstants.ZERO_ALPHA, _dismissDuration).Play();
			}
		}

		/// <summary>
		/// Sets a new target for the arrow to point at.
		/// </summary>
		public void SetTarget(RectTransform newTarget)
		{
			_target = newTarget;
			if (_arrowInstance != null && _target != null)
			{
				_moveTween?.Kill();
				_oscillationTween?.Kill();
				RotateTowardsTarget();
				MoveToTargetAndStartOscillation();
			}
		}

		private Canvas GetRootCanvas()
		{
			if (_rootCanvas != null)
				return _rootCanvas;

			Transform current = transform;
			while (current != null)
			{
				Canvas canvas = current.GetComponent<Canvas>();
				if (canvas != null)
					return canvas;
				current = current.parent;
			}

			return Object.FindObjectOfType<Canvas>();
		}

		private void SetSpawnPosition()
		{
			if (_canvasRect == null)
				return;

			Vector2 spawnOffset = Vector2.zero;

			switch (_spawnPosition)
			{
				case ArrowSpawnPosition.TopLeft:
					spawnOffset = new Vector2(-_canvasRect.rect.width * UIConstants.DEFAULT_PIVOT, _canvasRect.rect.height * UIConstants.DEFAULT_PIVOT);
					break;
				case ArrowSpawnPosition.TopRight:
					spawnOffset = new Vector2(_canvasRect.rect.width * UIConstants.DEFAULT_PIVOT, _canvasRect.rect.height * UIConstants.DEFAULT_PIVOT);
					break;
				case ArrowSpawnPosition.BottomLeft:
					spawnOffset = new Vector2(-_canvasRect.rect.width * UIConstants.DEFAULT_PIVOT, -_canvasRect.rect.height * UIConstants.DEFAULT_PIVOT);
					break;
				case ArrowSpawnPosition.BottomRight:
					spawnOffset = new Vector2(_canvasRect.rect.width * UIConstants.DEFAULT_PIVOT, -_canvasRect.rect.height * UIConstants.DEFAULT_PIVOT);
					break;
				case ArrowSpawnPosition.Center:
					spawnOffset = Vector2.zero;
					break;
				case ArrowSpawnPosition.Custom:
					spawnOffset = _customSpawnOffset;
					break;
			}

			// Convert canvas-space offset to local space of this transform
			Vector2 worldPos = _canvasRect.TransformPoint(spawnOffset);
			Vector2 localPos = transform.InverseTransformPoint(worldPos);
			_arrowRect.anchoredPosition = localPos;
		}

		private void RotateTowardsTarget()
		{
			if (_arrowRect == null || _target == null)
				return;

			Vector2 arrowWorldPos = _arrowRect.position;
			Vector2 targetWorldPos = _target.position;

			Vector2 direction = targetWorldPos - arrowWorldPos;
			float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
			float finalAngle = targetAngle - 90f;

			_rotationTween = _arrowRect.DORotate(new Vector3(UIConstants.ZERO_POSITION, UIConstants.ZERO_POSITION, finalAngle), _moveToTargetDuration)
				.SetEase(Ease.OutQuad)
				.Play();
		}

		private void MoveToTargetAndStartOscillation()
		{
			Vector2 targetPos = GetTargetPositionNearTarget();

			_moveTween = _arrowRect.DOAnchorPos(targetPos, _moveToTargetDuration)
				.SetEase(Ease.OutQuad)
				.OnComplete(() =>
				{
					StartOscillation();
				})
				.Play();
		}

		private Vector2 GetTargetPositionNearTarget()
		{
			Vector2 currentPos = _arrowRect.anchoredPosition;
			Vector2 targetWorldPos = _target.position;
			Vector2 targetLocalPos = transform.InverseTransformPoint(targetWorldPos);

			Vector2 direction = (targetLocalPos - currentPos).normalized;
			return targetLocalPos - (direction * _oscillationDistance);
		}

		private void StartOscillation()
		{
			Vector2 basePos = _arrowRect.anchoredPosition;
			Vector2 targetWorldPos = _target.position;
			Vector2 targetLocalPos = transform.InverseTransformPoint(targetWorldPos);

			Vector2 oscillationDir = (targetLocalPos - basePos).normalized;

			_oscillationTween = DOTween.To(
				() => UIConstants.ZERO_POSITION,
				t =>
				{
					float offset = Mathf.Sin(t * Mathf.PI * 2f) * (_oscillationDistance * UIConstants.DEFAULT_PIVOT);
					_arrowRect.anchoredPosition = basePos + (oscillationDir * offset);
				},
				UIConstants.FULL_ALPHA,
				_oscillationDuration)
				.SetLoops(-1, LoopType.Restart)
				.SetEase(Ease.Linear)
				.Play();
		}

		private void OnDestroy()
		{
			_moveTween?.Kill();
			_oscillationTween?.Kill();
			_dismissTween?.Kill();
			_rotationTween?.Kill();
		}
	}
}
