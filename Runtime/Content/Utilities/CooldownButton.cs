using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Serialization;
using AK.Core.Extensions;
using AK.Kernel.Timing;
using AK.Utilities;
using TMPro;

namespace AK.UI
{
    /// <summary>
    /// Passes a button's clicks on through a cooldown: a click starts the cooldown and reaches
    /// <see cref="OnClick"/>, and clicks during it reach <see cref="OnCooldownReject"/> instead.
    /// The button stays pressable throughout. An optional overlay empties and an optional text
    /// counts down as the cooldown runs, on its time domain: unscaled by default, so a cooldown
    /// runs on while the game is paused.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class CooldownButton : MonoBehaviour
    {
        [Header("Logic")]
        [Tooltip("How long (seconds) to wait before allowing another click.")]
        [SerializeField] private float _cooldownDuration = 1.0f;

        [Tooltip("The time the cooldown counts. Unscaled runs it on while the game is paused at timeScale 0.")]
        [FormerlySerializedAs("_useUnscaledTime")]
        [SerializeField] private TimeDomain _timeDomain = TimeDomain.Unscaled;

        [Header("Events")]
        [Tooltip("Add your listeners HERE. They will only fire if cooldown is ready.")]
        public Button.ButtonClickedEvent OnClick = new Button.ButtonClickedEvent();

        [Tooltip("Fires when the user clicks but the button is still on cooldown (Optional: play error sound).")]
        public UnityEvent OnCooldownReject;

        [Header("Visual Feedback")]
        [SerializeField] private Image _overlayImage;
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private TimeFormat _textFormat = TimeFormat.Abbreviated;
        [SerializeField] private TimeRounding _rounding = TimeRounding.Ceil;

        // Internal State
        private Button _sourceButton;
        private float _duration;
        private float _remaining;
        private bool _isCoolingDown;

        // The countdown text on show, kept to set the label only when its text changes.
        private readonly char[] _shownText = new char[TimeFormatter.MaxDurationLength];
        private int _shownLength = -1;

        // ----------------------------------------------------------------------
        // 1. INITIALIZATION
        // ----------------------------------------------------------------------
        private void Awake()
        {
            _sourceButton = GetComponent<Button>();
        }

        private void OnEnable()
        {
            // We listen to the "Raw" click from the UI Button
            _sourceButton.onClick.AddListener(OnSourceClick);
            ResetVisuals();
        }

        private void OnDisable()
        {
            _sourceButton.onClick.RemoveListener(OnSourceClick);
            _isCoolingDown = false;
        }

        // ----------------------------------------------------------------------
        // 2. THE FILTER LOGIC
        // ----------------------------------------------------------------------
        private void OnSourceClick()
        {
            if (_isCoolingDown)
            {
                // REJECT: Button was clicked, but we are busy.
                // The button still did its "Pressed" animation (visual feedback),
                // but we do NOT fire the main OnClick event.
                OnCooldownReject?.Invoke();
                return;
            }

            // ACCEPT: Start logic
            StartCooldown();

            // Forward the event to the user's listeners
            OnClick?.Invoke();
        }

        /// <summary>
        /// Starts a cooldown of <paramref name="customDuration"/> seconds, or of the button's own
        /// duration when it isn't above zero. The overlay empties over the cooldown started.
        /// </summary>
        public void StartCooldown(float customDuration = -1f)
        {
            _duration = customDuration > 0 ? customDuration : _cooldownDuration;
            _remaining = _duration;
            _isCoolingDown = true;

            // Note: We do NOT set _sourceButton.interactable = false;
            // The button remains fully interactive/pressable, just logically silent.

            if (_overlayImage) _overlayImage.gameObject.SetActive(true);
            if (_timerText) _timerText.gameObject.SetActive(true);
            ShowRemaining();
        }

        // ----------------------------------------------------------------------
        // 3. UPDATE LOOP
        // ----------------------------------------------------------------------
        private void Update()
        {
            if (_isCoolingDown) Tick(_timeDomain.DeltaTime());
        }

        /// <summary>Counts <paramref name="seconds"/> off the cooldown running.</summary>
        internal void Tick(float seconds)
        {
            if (!_isCoolingDown) return;

            _remaining -= seconds;

            if (_remaining <= 0)
            {
                FinishCooldown();
                return;
            }

            ShowRemaining();
        }

        private void ShowRemaining()
        {
            // Fill Effect (1.0 -> 0.0)
            if (_overlayImage != null)
            {
                _overlayImage.fillAmount = _duration > 0 ? Mathf.Clamp01(_remaining / _duration) : 0;
            }

            // Text Effect: formatted without allocating, and set only when it changes.
            if (_timerText != null)
            {
                Span<char> text = stackalloc char[TimeFormatter.MaxDurationLength];
                _remaining.TryFormatDuration(text, out int length, _textFormat, max: 1, r: _rounding);

                ReadOnlySpan<char> shown = text.Slice(0, length);
                if (length == _shownLength && shown.SequenceEqual(_shownText.AsSpan(0, length))) return;

                shown.CopyTo(_shownText);
                _shownLength = length;
                _timerText.SetText(_shownText, 0, length);
            }
        }

        private void FinishCooldown()
        {
            _isCoolingDown = false;
            _remaining = 0;
            ResetVisuals();
        }

        private void ResetVisuals()
        {
            if (_overlayImage)
            {
                _overlayImage.fillAmount = 0;
                _overlayImage.gameObject.SetActive(false);
            }
            if (_timerText)
            {
                _timerText.text = "";
                _timerText.gameObject.SetActive(false);
            }

            _shownLength = -1;
        }

        // ----------------------------------------------------------------------
        // 4. API (Matches Button API)
        // ----------------------------------------------------------------------
        /// <summary>
        /// Manually add a listener via code.
        /// Usage: myCooldownBtn.AddListener(MyMethod);
        /// </summary>
        public void AddListener(UnityAction call)
        {
            OnClick.AddListener(call);
        }

        /// <summary>
        /// Manually remove a listener via code.
        /// </summary>
        public void RemoveListener(UnityAction call)
        {
            OnClick.RemoveListener(call);
        }

        /// <summary>
        /// Removes all listeners.
        /// </summary>
        public void RemoveAllListeners()
        {
            OnClick.RemoveAllListeners();
        }
    }
}
