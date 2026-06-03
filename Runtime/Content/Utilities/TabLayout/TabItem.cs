using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI.Utilities
{
	/// <summary>
	/// Represents a single tab item in the tabbed layout system.
	/// Uses Unity Button component for click handling.
	/// </summary>
	public class TabItem<TData> : MonoBehaviour
	{
		[Header("Required Components")] [SerializeField]
		protected Button _button;

		[SerializeField] private RectTransform _tabItemTransform;
		[SerializeField] private RectTransform _scalingTargetTransform;

		[Header("Visual Elements (Optional)")] [SerializeField]
		protected Image _backgroundImage;

		[SerializeField] private Image           _iconImage;
		[SerializeField] private TextMeshProUGUI _labelText;

		[Header("Optional Visual Elements")] [SerializeField]
		protected GameObject _highlightObject;

		[SerializeField] private Image _selectionBorder;

		[Header("Colors")] [SerializeField]
		protected Graphic _targetColorGraphic;

		[SerializeField] private Color _normalColor   = Color.white;
		[SerializeField] private Color _selectedColor = new Color(1f, 0.95f, 0.85f);

		[Header("Tab Size Configuration")] [SerializeField]
		protected bool _useCustomTabSizes = true;

		[SerializeField] private Vector2 _normalTabSize                = new(100, 100);
		[SerializeField] private Vector2 _selectedTabSize              = new(100f, 100f);
		[SerializeField] private float   _tabResizeDuration            = 0.3f;
		[SerializeField] private float   _tabResizeDelayAfterIndicator = 0.1f;
		[SerializeField] private Ease    _tabResizeEase                = Ease.OutBack;

		protected TabbedLayout _tabbedLayout;
		protected TData        _tabData;

		public int             Index            { get; private set; }
		public RectTransform   TabItemTransform => _tabItemTransform;
		public Image           BackgroundImage  => _backgroundImage;
		public Image           IconImage        => _iconImage;
		public TextMeshProUGUI LabelText        => _labelText;
		public bool            IsSelected       { get; private set; }

		private void Awake()
		{
			if (_button == null)
				_button = GetComponent<Button>();
		}

		public virtual void Initialize(int index, TabbedLayout layout, TData data = default)
		{
			Index = index;
			_tabbedLayout = layout;
			_tabData = data;

			if (_tabItemTransform == null)
				_tabItemTransform = transform as RectTransform;
		}

		private void OnEnable()
		{
			if (_button != null)
				_button.onClick.AddListener(OnTabClicked);
		}

		private void OnDisable()
		{
			if (_button != null)
				_button.onClick.RemoveListener(OnTabClicked);
		}

		private void OnTabClicked()
		{
			_tabbedLayout?.SelectTab(Index);
		}

		/// <summary>
		/// Sets the text content of the label if available.
		/// </summary>
		public void SetText(string text)
		{
			if (_labelText != null)
				_labelText.text = text;
		}

		/// <summary>
		/// Sets the icon sprite if icon image is available.
		/// </summary>
		public void SetIcon(Sprite icon)
		{
			if (_iconImage != null)
				_iconImage.sprite = icon;
		}

		public void SetSelected(bool selected, bool immediate = false)
		{
			IsSelected = selected;

			// Highlight object visibility
			if (_highlightObject != null)
				_highlightObject.SetActive(selected);

			// Selection border
			if (_selectionBorder != null)
				_selectionBorder.enabled = selected;

			// Update color on target graphic
			UpdateColorState(selected, immediate);
			if (selected)
			{
				AnimateTargetSize(_selectedTabSize, immediate);
			}
			else
			{
				AnimateTargetSize(_normalTabSize, immediate);
			}
		}

		private void AnimateTargetSize(Vector2 targetSize, bool immediate)
		{
			if (immediate)
			{
				_scalingTargetTransform.sizeDelta = targetSize;
			}
			else
			{
				float delay = (targetSize == _normalTabSize) ? 0f : _tabResizeDelayAfterIndicator;

				_scalingTargetTransform
					.DOSizeDelta(targetSize, _tabResizeDuration)
					.SetEase(_tabResizeEase)
					.SetDelay(delay)
					.SetUpdate(true)
					.Play();
			}
		}

		private void UpdateColorState(bool isSelected, bool immediate)
		{
			Graphic targetGraphic = _targetColorGraphic;
			if (targetGraphic == null)
				targetGraphic = _backgroundImage;

			if (targetGraphic != null)
			{
				Color targetColor = isSelected ? _selectedColor : _normalColor;
				UpdateGraphicColor(targetGraphic, targetColor, immediate);
			}
		}

		private void UpdateGraphicColor(Graphic graphic, Color color, bool immediate)
		{
			if (graphic == null)
				return;

			if (immediate)
			{
				graphic.color = color;
			}
			else
			{
				graphic.DOColor(color, 0.2f).SetUpdate(true).Play();
			}
		}

		public Vector2 GetPosition()
		{
			return _tabItemTransform != null ? _tabItemTransform.position : Vector2.zero;
		}

		public class VoidData { }
	}

	public class TabItem : TabItem<TabItem<TabItem>.VoidData>
	{
		public override void Initialize(int index, TabbedLayout layout, TabItem<TabItem>.VoidData data = default)
		{
			base.Initialize(index, layout, new TabItem<TabItem>.VoidData());
		}
	}
}