using System;
using System.Collections.Generic;
using AK.Core.Extensions;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AK.Systems
{
	/// <summary>
	/// Dims the screen except for holes over its targets. Opening the holes plays an iris-in:
	/// the intro. Input is held off through the <see cref="UIInputGate"/> while the intro
	/// plays, so a stray tap can't act on a half-presented step. The hold ends exactly once:
	/// when the intro finishes or is stopped, or, failing both, when the gate times it out
	/// <see cref="IntroHoldMargin"/> after the intro should have ended. The intro and the
	/// dismiss hold run on the UI's time, unscaled by default, so they also finish in a game
	/// paused at timeScale 0.
	/// </summary>
	[RequireComponent(typeof(Image), typeof(GraphicRaycaster))]
	public class UIViewSpotlight : UIView<UIViewSpotlightContext>, ICanvasRaycastFilter, IPointerDownHandler, IPointerClickHandler
	{
		private const int MAX_HOLES = 8;

		/// <summary>
		/// How long past the intro's end the gate waits before ending the intro's input hold
		/// itself: the backstop for an intro tween that never finishes, such as one paused with
		/// all of DOTween. The holes then snap open.
		/// </summary>
		public const float IntroHoldMargin = 1f;

		private const string IntroHoldOwner = nameof(UIViewSpotlight) + " intro";

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

		private Image     _dimImage;
		private Color     _defaultDimColor;
		private Material  _materialInstance;
		private int       _holeCount;
		private Tween     _introTween;
		private InputHold _introHold;
		private int       _introRun;
		private float     _introT = 1f;
		private bool      _introActive;
		private bool      _introPending;
		private double    _dismissHoldUntil;
		private bool      _downWhileAccepting;

		public RectTransform FurnitureRoot => _furnitureRoot;

		// Serialized fields are the defaults; non-null context members win per show.
		private float EffectivePadding              => Context?.Padding              ?? _padding;
		private float EffectiveFeather              => Context?.Feather              ?? _feather;
		private float EffectiveIntroDuration        => Context?.IntroDuration        ?? _introDuration;
		private Ease  EffectiveIntroEase            => Context?.IntroEase            ?? _introEase;
		private float EffectiveDismissHoldDuration  => Context?.DismissHoldDuration  ?? 0f;
		private bool  EffectivePassClicksThroughHole => Context?.PassClicksThroughHole ?? false;
		private Color EffectiveDimColor             => Context?.DimColor             ?? _defaultDimColor;

		private bool IsDismissHoldActive => TimeDomain.Now() < _dismissHoldUntil;

		// The intro needs no lock of its own: the gate keeps every pointer off while it plays.
		private bool IsInputLocked => IsDismissHoldActive;

		/// <summary>True while the intro plays.</summary>
		public bool IsIntroPlaying => _introActive;

		public event Action BackgroundTapped;

		private void Awake()
		{
			_dimImage = GetComponent<Image>();
			_defaultDimColor = _dimImage.color;

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

		/// <summary>
		/// Puts holes over <paramref name="targets"/>. With <paramref name="animateSpotlight"/>
		/// they open with the intro. A Show's onInit runs before the show sets its context, so the
		/// intro plays with that show's IntroDuration and IntroEase: on a spotlight that is hidden
		/// or closing it waits for the show, and on one already shown the show starts it again.
		/// </summary>
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

			StopIntro();
			_introPending = false;

			if (!animateSpotlight) return;

			if (State is ViewState.Hidden or ViewState.Hiding)
			{
				_introPending = true;
				_introActive = true;
				_introT = 0f;
			}
			else
			{
				StartIntro();
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
			// Every show sets the tint, so a tinted step never leaks into the next spotlight.
			_dimImage.color = EffectiveDimColor;
			_downWhileAccepting = false;
			float hold = EffectiveDismissHoldDuration;
			_dismissHoldUntil = hold > 0f ? TimeDomain.Now() + hold : 0d;

			// A re-show's intro plays with this show's settings, also one started before the
			// show set its context.
			if (_introPending || _introActive) StartIntro();
		}

		public override void OnPrepareHide()
		{
			base.OnPrepareHide();
			SetInteractable(false);
			_introPending = false;
			StopIntro();
			_dismissHoldUntil = 0d;
			_downWhileAccepting = false;
		}

		private void OnDisable()
		{
			// Inactive for any reason: nothing plays, so nothing may keep input held. An intro
			// waiting for a show keeps waiting: a static spotlight re-shown while it closes is
			// given its targets before the close deactivates it.
			StopIntro();
		}

		private void Update()
		{
			// The gate ended the intro's hold before the intro ended, at the timeout or by a
			// ReleaseAll. Open the holes too, so what the player sees matches what input does.
			if (_introActive && !_introPending && _introHold.IsSet && !_introHold.IsHeld)
			{
				StopIntro();
			}

			if (_materialInstance != null)
			{
				PushHolesToMaterial();
			}
		}

		/// <summary>Plays the intro from closed, holding input until it ends.</summary>
		private void StartIntro()
		{
			_introPending = false;
			StopIntro();

			float duration = EffectiveIntroDuration;
			if (!(duration > 0f)) return;

			int run = ++_introRun;
			_introActive = true;
			_introT = 0f;

			UIInputGate gate = UISystem?.InputGate;
			if (gate != null) _introHold = gate.Hold(IntroHoldOwner, duration + IntroHoldMargin);

			// OnKill fires when the tween completes (auto-kill) and when it is killed. A tween
			// killed late, after a newer intro started, finds a newer run and leaves it alone.
			_introTween = DOTween.To(() => _introT, v => _introT = v, 1f, duration)
			                     .SetEase(EffectiveIntroEase)
			                     .SetTimeDomain(TimeDomain)
			                     .SetAutoKill(true)
			                     .SetTarget(this)
			                     .OnKill(() =>
			                     {
				                     if (run == _introRun) EndIntro();
			                     })
			                     .Play();
		}

		/// <summary>Stops any intro in place: the holes are fully open and input is released. Idempotent.</summary>
		private void StopIntro()
		{
			_introRun++;
			Tween tween = _introTween;
			_introTween = null;
			tween?.Kill();
			EndIntro();
		}

		private void EndIntro()
		{
			_introTween = null;
			_introActive = false;
			_introT = 1f;
			_introHold.Release();
			_introHold = default;
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
			// Dismiss-hold and tap-anywhere steps cover the whole screen so a hole tap
			// dismisses instead of falling through to the control. While the intro plays,
			// the input gate keeps every pointer off anyway.
			if (IsInputLocked || !EffectivePassClicksThroughHole)
			{
				return true;
			}

			return !IsInsideAnyHole(screenPoint);
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			_downWhileAccepting = !IsInputLocked;
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			// Ignore dismiss-hold taps, and a click whose press started then —
			// otherwise a mash down-during / up-after would dismiss instantly.
			if (IsInputLocked || !_downWhileAccepting)
			{
				return;
			}

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
				float radius = Mathf.Max(width, height) * 0.5f + EffectivePadding;

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
			_materialInstance.SetFloat(FeatherProperty, EffectiveFeather);
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

			return max + EffectiveFeather;
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

	/// <summary>
	/// Optional per-show overrides for the spotlight. Every member is nullable:
	/// null means "not provided" — the view's serialized values apply instead.
	/// </summary>
	public class UIViewSpotlightContext : UIContext
	{
		public float? Padding;
		public float? Feather;
		public float? IntroDuration;
		public Ease?  IntroEase;
		public float? DismissHoldDuration;
		public bool?  PassClicksThroughHole;
		public Color? DimColor;
	}
}
