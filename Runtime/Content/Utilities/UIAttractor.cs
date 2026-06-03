using System;
using System.Collections.Generic;
using System.Threading;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Random = UnityEngine.Random;

/// <summary>
/// Animates reward items from a source to a target UI element with three phases:
/// 1. Pop & Converge: Items spawn and move to center
/// 2. Idle: Items rotate at center
/// 3. Flight: Items fly to target with staggered timing
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIAttractor : MonoBehaviour
{
	public enum MoveType
	{
		Linear,
		Curved
	}

	#region Serialized Fields

	[Header("Item Configuration")] [SerializeField]
	private GameObject _rewardItemPrefab;

	[SerializeField] private Vector2 _scaleRange = new Vector2(1.0f, 1.2f);

	[Header("Phase 1: Pop & Converge")] [SerializeField]
	private MoveType _convergeMoveType = MoveType.Curved;

	[SerializeField] private float _convergeDuration = 0.5f;
	[SerializeField] private Ease  _convergeEase     = Ease.OutQuad;
	[SerializeField] private float _scatterRadius    = 150f;
	[SerializeField] private float _spawnStagger     = 0.03f;
	[SerializeField] private float _popDuration      = 0.4f;
	[SerializeField] private Ease  _popEase          = Ease.OutBack;

	[Header("Phase 2: Idle at Center")] [SerializeField]
	private float _idleDuration = 0.6f;

	[SerializeField] private bool    _rotateDuringIdle   = true;
	[SerializeField] private Vector2 _rotationSpeedRange = new Vector2(150f, 250f);

	[Header("Phase 3: Flight to Target")] [SerializeField]
	private MoveType _flightMoveType = MoveType.Curved;

	[SerializeField] private float _flightDuration = 0.7f;
	[SerializeField] private Ease  _flightEase     = Ease.InOutQuad;
	[SerializeField] private float _itemsPerSecond = 20f;

	[SerializeField, Range(0.1f, 10f)]
	private float _arrivalThreshold = 5f;

	[Header("Curve Settings")] [SerializeField]
	private float _curveStrength = 200f;

	[Header("Impact Effects")] [SerializeField]
	private float _impactScale = 1.3f;

	[SerializeField] private float       _impactDuration = 0.15f;
	[SerializeField] private Ease        _impactEase     = Ease.OutBack;
	[SerializeField] private AudioSource _audioSource;
	[SerializeField] private AudioClip   _arrivalSound;

	[Header("Setup")] [SerializeField]
	private Transform _overrideItemsParent;

	#endregion

	#region Private State

	private RectTransform _targetRect;
	private RectTransform _itemsParentRT;
	private Canvas        _canvas;
	private Camera        _uiCamera;
	private bool          _isInitialized;
	private Sprite        _icon;

	private Action<GameObject, int> _onItemSpawn;
	private Action<int>             _onItemPop;
	private Action<int>             _onItemConverge;
	private Action<int>             _onItemReachTarget;

	private readonly List<GameObject>        _activeItems = new List<GameObject>();
	private          Tween                   _impactTween;
	private          CancellationTokenSource _cancellationTokenSource;

	#endregion

	#region Initialization

	public void Init(Sprite icon = null)
	{
		if (_isInitialized) return;

		_icon = icon;
		_targetRect = GetComponent<RectTransform>();
		_canvas = GetComponentInParent<Canvas>();

		if (_canvas == null)
		{
			Debug.LogError($"[UIAttractor] {name} must be under a Canvas.", this);
			return;
		}

		_uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
		SetupParentContainer();
		_isInitialized = true;
	}

	private void SetupParentContainer()
	{
		if (_overrideItemsParent != null)
		{
			_itemsParentRT = _overrideItemsParent.GetComponent<RectTransform>();
		}
		else
		{
			var containerGO = new GameObject($"{gameObject.name}_ItemsContainer");
			containerGO.transform.SetParent(_canvas.transform, false);
			containerGO.transform.SetAsLastSibling();

			_itemsParentRT = containerGO.AddComponent<RectTransform>();
			_itemsParentRT.anchorMin = Vector2.zero;
			_itemsParentRT.anchorMax = Vector2.one;
			_itemsParentRT.offsetMin = Vector2.zero;
			_itemsParentRT.offsetMax = Vector2.zero;
			_itemsParentRT.pivot = new Vector2(0.5f, 0.5f);
		}
	}

	#endregion

	#region Main Animation Flow

	public async UniTask AttractItems(int totalCount,
	                                  Sprite icon = null,
	                                  RectTransform spawnSource = null,
	                                  Action<GameObject, int> onItemSpawn = null,
	                                  Action<int> onItemPop = null,
	                                  Action<int> onItemConverge = null,
	                                  Action<int> onItemReachTarget = null)
	{
		if (!_isInitialized) Init(icon);
		if (_rewardItemPrefab == null || totalCount <= 0) return;

		_onItemSpawn = onItemSpawn;
		_onItemPop = onItemPop;
		_onItemConverge = onItemConverge;
		_onItemReachTarget = onItemReachTarget;
		// Cancel any existing animation and cleanup
		CancelCurrentAnimation();
		_cancellationTokenSource = new CancellationTokenSource();
		var ct = _cancellationTokenSource.Token;

		// Phase 1: Pop & Converge to center
		if (!ct.IsCancellationRequested)
			await SpawnAndConvergeItems(totalCount, spawnSource, ct);

		// Phase 2: Idle at center
		if (!ct.IsCancellationRequested)
			await IdlePhase(ct);

		// Phase 3: Fly to target
		if (!ct.IsCancellationRequested)
			await FlyItemsToTarget(ct);

		_cancellationTokenSource?.Dispose();
		_cancellationTokenSource = null;
	}

	private async UniTask SpawnAndConvergeItems(int count, RectTransform spawnSource, CancellationToken ct)
	{
		var convergeTasks = new List<UniTask>();

		for (int i = 0; i < count; i++)
		{
			if (ct.IsCancellationRequested) return;

			var itemGO = CreateItem(spawnSource);
			_onItemSpawn?.Invoke(itemGO, i + 1);
			var itemRT = itemGO.GetComponent<RectTransform>();
			var scatterPos = Random.insideUnitCircle * _scatterRadius;

			// Animate pop (scale)
			var randomScale = Random.Range(_scaleRange.x, _scaleRange.y);
			int itemNumber = i + 1;
			itemRT.DOScale(randomScale, _popDuration).SetEase(_popEase).OnComplete(() => { _onItemPop?.Invoke(itemNumber + 1); }).Play();

			// Animate converge (position)
			var moveTween = CreateMoveTween(itemRT, scatterPos, _convergeMoveType, _convergeDuration, _convergeEase);
			moveTween.OnComplete(() => { _onItemConverge?.Invoke(itemNumber); });
			moveTween.Play();
			convergeTasks.Add(moveTween.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct));

			if (_spawnStagger > 0)
			{
				var cancelled = await UniTask.Delay(TimeSpan.FromSeconds(_spawnStagger), cancellationToken: ct)
				                             .SuppressCancellationThrow();
				if (cancelled) return;
			}
		}

		var result = await UniTask.WhenAll(convergeTasks).SuppressCancellationThrow();
	}

	private async UniTask IdlePhase(CancellationToken ct)
	{
		if (_rotateDuringIdle)
			StartIdleRotation();

		if (_idleDuration > 0)
		{
			var cancelled = await UniTask.Delay(TimeSpan.FromSeconds(_idleDuration), cancellationToken: ct)
			                             .SuppressCancellationThrow();
		}
	}

	private async UniTask FlyItemsToTarget(CancellationToken ct)
	{
		var destination = ConvertWorldToLocalPosition(_targetRect.position);
		var delayBetweenFlights = 1f / _itemsPerSecond;

		// Copy list to avoid modification during iteration
		var itemsToFly = new List<GameObject>(_activeItems);

		for (int i = 0; i < itemsToFly.Count; i++)
		{
			GameObject item = itemsToFly[i];
			if (ct.IsCancellationRequested) return;

			if (item == null) continue;

			// Stop idle rotation
			item.transform.DOKill();

			// Launch item and don't wait for completion
			FlyItemToTargetAsync(item, destination, ct, i + 1).Forget();

			if (delayBetweenFlights > 0)
			{
				var cancelled = await UniTask.Delay(TimeSpan.FromSeconds(delayBetweenFlights), cancellationToken: ct)
				                             .SuppressCancellationThrow();
				if (cancelled) return;
			}
		}

		// Wait for all items to be destroyed
		await UniTask.WaitUntil(() => _activeItems.Count == 0 || this == null, cancellationToken: ct)
		             .SuppressCancellationThrow();
	}

	#endregion

	#region Item Flight & Arrival

	private async UniTaskVoid FlyItemToTargetAsync(GameObject itemGO, Vector2 destination, CancellationToken ct, int itemNumber)
	{
		if (itemGO == null) return;

		var itemRT = itemGO.GetComponent<RectTransform>();
		var moveTween = CreateMoveTween(itemRT, destination, _flightMoveType, _flightDuration, _flightEase);
		moveTween.Play();

		// Monitor distance during flight
		while (!ct.IsCancellationRequested && itemGO != null && itemRT != null)
		{
			var distance = Vector2.Distance(itemRT.localPosition, destination);

			if (distance < _arrivalThreshold)
			{
				_onItemReachTarget?.Invoke(itemNumber);
				moveTween.Kill();
				PlayArrivalEffect();
				DestroyItem(itemGO);
				break;
			}

			var cancelled = await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();
			if (cancelled) break;
		}
	}

	private void PlayArrivalEffect()
	{
		// Play arrival sound
		if (_audioSource != null && _arrivalSound != null)
			_audioSource.PlayOneShot(_arrivalSound);

		// Complete any previous impact animation
		if (_impactTween != null && _impactTween.IsActive())
			_impactTween.Complete(withCallbacks: true);

		// Punch scale animation
		transform.localScale = Vector3.one;
		_impactTween = transform.DOPunchScale(
			                        Vector3.one * (_impactScale - 1f),
			                        _impactDuration,
			                        vibrato: 1,
			                        elasticity: 0.5f
		                        ).SetEase(_impactEase)
		                        .Play();
	}

	#endregion

	#region Item Creation & Management

	private GameObject CreateItem(RectTransform spawnSource)
	{
		var itemGO = Instantiate(_rewardItemPrefab, _itemsParentRT);
		itemGO.SetActive(true);
		var img = itemGO.GetComponent<Image>();

		if (_icon != null)
			img.sprite = _icon;

		var itemRT = img.rectTransform;
		itemRT.anchorMin = Vector2.one * 0.5f;
		itemRT.anchorMax = Vector2.one * 0.5f;
		itemRT.pivot = Vector2.one * 0.5f;
		itemRT.localScale = Vector3.zero;

		// Set initial position
		itemRT.localPosition = spawnSource != null
			? ConvertWorldToLocalPosition(spawnSource.position)
			: Vector2.zero;

		_activeItems.Add(itemGO);
		return itemGO;
	}

	private void DestroyItem(GameObject item)
	{
		if (item == null) return;

		_activeItems.Remove(item);
		item.transform.DOKill();
		Destroy(item);
	}

	private void StartIdleRotation()
	{
		foreach (var item in _activeItems)
		{
			if (item == null) continue;

			var speed = Random.Range(_rotationSpeedRange.x, _rotationSpeedRange.y);
			var direction = Random.value > 0.5f ? 1f : -1f;

			item.transform.DOLocalRotate(
				    Vector3.forward * 360f * direction,
				    360f / speed,
				    RotateMode.FastBeyond360
			    ).SetEase(Ease.Linear)
			    .SetLoops(-1, LoopType.Incremental)
			    .Play();
		}
	}

	#endregion

	#region Tween Utilities

	private Tween CreateMoveTween(RectTransform target, Vector2 endPos, MoveType moveType, float duration, Ease ease)
	{
		Tween tween = moveType == MoveType.Linear
			? target.DOLocalMove(endPos, duration)
			: target.DOLocalPath(CalculateCurvedPath(target.localPosition, endPos), duration, PathType.CatmullRom);

		return tween.SetEase(ease);
	}

	private Vector3[] CalculateCurvedPath(Vector2 startPos, Vector2 endPos)
	{
		var direction = endPos - startPos;
		var midPoint = startPos + direction * 0.5f;
		var normal = new Vector2(-direction.y, direction.x).normalized;
		var curveDirection = Random.value > 0.5f ? 1f : -1f;
		var curveMagnitude = _curveStrength * curveDirection * Random.Range(0.8f, 1.2f);
		var controlPoint = midPoint + (normal * curveMagnitude);

		return new Vector3[] { controlPoint, endPos };
	}

	private Vector2 ConvertWorldToLocalPosition(Vector3 worldPos)
	{
		var screenPoint = RectTransformUtility.WorldToScreenPoint(_uiCamera, worldPos);
		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			_itemsParentRT,
			screenPoint,
			_uiCamera,
			out Vector2 localPoint
		);
		return localPoint;
	}

	#endregion

	#region Cleanup

	private void CancelCurrentAnimation()
	{
		_cancellationTokenSource?.Cancel();
		_cancellationTokenSource?.Dispose();
		_cancellationTokenSource = null;

		// Destroy all active items
		foreach (var item in _activeItems.ToArray())
		{
			if (item != null)
			{
				item.transform.DOKill();
				Destroy(item);
			}
		}

		_activeItems.Clear();

		// Reset impact animation
		_impactTween?.Kill(complete: true);
		_impactTween = null;
		transform.localScale = Vector3.one;
	}

	private void OnDestroy()
	{
		CancelCurrentAnimation();

		if (_overrideItemsParent == null && _itemsParentRT != null)
			Destroy(_itemsParentRT.gameObject);
	}

	#endregion
}