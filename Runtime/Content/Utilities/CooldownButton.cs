using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using AK.Utilities;
using TMPro;

namespace AK.UI
{
    [RequireComponent(typeof(Button))]
    public class CooldownButton : MonoBehaviour
    {
        [Header("Logic")]
        [Tooltip("How long (seconds) to wait before allowing another click.")]
        [SerializeField] private float _cooldownDuration = 1.0f;
        [SerializeField] private bool _useUnscaledTime = false;

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
        private float _timer;
        private bool _isCoolingDown;

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

        public void StartCooldown(float customDuration = -1f)
        {
            _timer = customDuration > 0 ? customDuration : _cooldownDuration;
            _isCoolingDown = true;
            
            // Note: We do NOT set _sourceButton.interactable = false;
            // The button remains fully interactive/pressable, just logically silent.
            
            if (_overlayImage) _overlayImage.gameObject.SetActive(true);
            if (_timerText) _timerText.gameObject.SetActive(true);
        }

        // ----------------------------------------------------------------------
        // 3. UPDATE LOOP
        // ----------------------------------------------------------------------
        private void Update()
        {
            if (!_isCoolingDown) return;

            float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            _timer -= dt;

            UpdateVisuals();

            if (_timer <= 0)
            {
                FinishCooldown();
            }
        }

        private void UpdateVisuals()
        {
            // Fill Effect (1.0 -> 0.0)
            if (_overlayImage != null)
            {
                float ratio = Mathf.Clamp01(_timer / _cooldownDuration);
                _overlayImage.fillAmount = ratio;
            }

            // Text Effect (TimeFormatter)
            if (_timerText != null)
            {
                _timerText.text = _timer.FormatDuration(_textFormat, max: 1, r: _rounding);
            }
        }

        private void FinishCooldown()
        {
            _isCoolingDown = false;
            _timer = 0;
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