using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;

namespace UI.Utilities
{
	/// <summary>
	/// Main controller for a tabbed layout system with animated indicator.
	/// Tabs can be assigned in inspector or added manually at runtime via AddTab().
	/// </summary>
	public class TabbedLayout : MonoBehaviour
	{
		[Header("Indicator")] [SerializeField]
		private RectTransform _indicator;

		[Header("Indicator Animation")] [SerializeField]
		private float _indicatorMoveDuration = 0.4f;

		[SerializeField] private Ease _indicatorMoveEase = Ease.OutCubic;

		[Header("Tabs (Inspector Assignment - Optional)")]
		[Tooltip("Tabs can be assigned in inspector OR added manually via AddTab()")]
		[SerializeField]
		private TabItem[] _inspectorTabItems;

		[Header("Tabs Container")] [SerializeField]
		private RectTransform _tabsContainer;

		private int           _selectedIndex = -1;
		private Tween         _currentIndicatorTween;
		private List<TabItem> _tabs           = new List<TabItem>();
		private bool          _isInteractable = true;

		public RectTransform TabsContainer => _tabsContainer;

		/// <summary>
		/// Event fired when a tab is selected. Passes the selected index.
		/// </summary>
		public event Action<int> OnTabSelected;

		public event Action<TabItem> OnTabItemSelected;

		/// <summary>
		/// Event fired when tab selection animation completes.
		/// </summary>
		public event Action<int> OnTabSelectionComplete;

		public event Action<TabItem> OnTabItemSelectionComplete;

		public int                    SelectedIndex => _selectedIndex;
		public TabItem                SelectedItem  => GetTabOrNull(_selectedIndex);
		public int                    TabCount      => _tabs.Count;
		public IReadOnlyList<TabItem> Tabs          => _tabs.AsReadOnly();

		private async UniTask Start()
		{
			await UniTask.Yield();
			if (Application.isPlaying)
				Initialize();
		}

		private void Reset()
		{
			_inspectorTabItems = GetComponentsInChildren<TabItem>(true);
		}

		#region Initialization

		private void Initialize()
		{
			_tabs.Clear();

			if (_inspectorTabItems != null)
			{
				foreach (var tab in _inspectorTabItems)
				{
					if (tab != null)
						_tabs.Add(tab);
				}
			}

			ReindexAllTabs();

			if (_tabs.Count > 0)
			{
				SelectTab(0, true);
			}

			else
			{
				_indicator.gameObject.SetActive(false);
			}
		}

		#endregion

		#region Public API - Tab Management

		/// <summary>
		/// Adds a manually created TabItem to the layout.
		/// </summary>
		public void AddTab(TabItem tab)
		{
			if (tab == null)
			{
				Debug.LogWarning("TabbedLayout: Cannot add null tab.", this);
				return;
			}

			int index = _tabs.Count;
			_tabs.Add(tab);
			tab.Initialize(index, this);
		}

		/// <summary>
		/// Adds and selects a manually created TabItem.
		/// </summary>
		public void AddAndSelectTab(TabItem tab, bool animate = true)
		{
			if (tab == null)
			{
				Debug.LogWarning("TabbedLayout: Cannot add null tab.", this);
				return;
			}

			AddTab(tab);
			SelectTab(_tabs.Count - 1, !animate);
		}

		/// <summary>
		/// Removes a tab at the specified index.
		/// </summary>
		public void RemoveTab(int index)
		{
			if (index < 0 || index >= _tabs.Count)
			{
				Debug.LogWarning($"TabbedLayout: Tab index {index} is out of range.", this);
				return;
			}

			var tab = _tabs[index];
			if (tab != null)
				Destroy(tab.gameObject);

			_tabs.RemoveAt(index);
			ReindexAllTabs();

			AdjustSelectedIndexAfterRemoval(index);
		}

		/// <summary>
		/// Removes a specific tab item.
		/// </summary>
		public void RemoveTab(TabItem tab)
		{
			if (tab == null)
				return;

			int index = _tabs.IndexOf(tab);
			if (index >= 0)
				RemoveTab(index);
		}

		/// <summary>
		/// Removes all tabs.
		/// </summary>
		public void ClearTabs()
		{
			foreach (var tab in _tabs)
			{
				if (tab != null)
					Destroy(tab.gameObject);
			}

			_tabs.Clear();
			_selectedIndex = -1;
		}

		#endregion

		#region Public API - Tab Selection

		public void SetInteractable(bool isInteractable)
		{
			_isInteractable = isInteractable;
		}

		/// <summary>
		/// Select a tab at the specified index with optional animation.
		/// </summary>
		/// <param name="index">Index of the tab to select</param>
		/// <param name="immediate">If true, skip animations and apply changes immediately</param>
		public void SelectTab(int index, bool immediate = false)
		{
			if (!_isInteractable) return;
			if (_tabs.Count == 0)
			{
				Debug.LogWarning("TabbedLayout: No tabs available.", this);
				return;
			}

			if (index < 0 || index >= _tabs.Count)
			{
				Debug.LogWarning($"Tab index {index} is out of range (0-{_tabs.Count - 1}).", this);
				return;
			}

			if (index == _selectedIndex)
				return;

			if (!_indicator.gameObject.activeSelf)
			{
				_indicator.gameObject.SetActive(true);
			}

			// Complete any ongoing animation if switching to immediate mode
			if (!immediate)
				_currentIndicatorTween?.Complete();

			int previousIndex = _selectedIndex;
			_selectedIndex = index;

			var newTab = _tabs[index];
			var previousTab = GetTabOrNull(previousIndex);

			OnTabSelected?.Invoke(index);
			OnTabItemSelected?.Invoke(newTab);
			AnimateSelection(newTab, previousTab, immediate);
		}

		/// <summary>
		/// Select a specific tab item.
		/// </summary>
		public void SelectTab(TabItem tab, bool immediate = false)
		{
			if (tab == null)
			{
				Debug.LogWarning("TabbedLayout: Cannot select null tab.", this);
				return;
			}

			int index = _tabs.IndexOf(tab);
			if (index < 0)
			{
				Debug.LogWarning("TabbedLayout: Tab not found in layout.", this);
				return;
			}

			SelectTab(index, immediate);
		}

		#endregion

		#region Private Methods

		private void ReindexAllTabs()
		{
			for (int i = 0; i < _tabs.Count; i++)
			{
				_tabs[i]?.Initialize(i, this);
			}
		}

		[Button]
		public void RepositionIndicator()
		{
			TabItem currentTab = GetTabOrNull(_selectedIndex);
			if (currentTab)
			{
				if (_tabsContainer != null)
					LayoutRebuilder.ForceRebuildLayoutImmediate(_tabsContainer);

				Vector2 targetPosition = currentTab.TabItemTransform.position;
				_indicator.position = new Vector3(targetPosition.x, targetPosition.y, _indicator.position.z);
			}
		}

		private void AnimateSelection(TabItem newTab, TabItem previousTab, bool immediate)
		{
			_currentIndicatorTween?.Kill();

			Vector2 targetPosition = newTab.TabItemTransform.position;
			Vector3 targetPosition3D = new Vector3(targetPosition.x, targetPosition.y, _indicator.position.z);

			if (immediate)
			{
				ApplyImmediateSelection(targetPosition, newTab, previousTab);
			}
			else
			{
				_currentIndicatorTween = _indicator
				                         .DOMove(targetPosition3D, _indicatorMoveDuration)
				                         .SetEase(_indicatorMoveEase)
				                         .SetUpdate(true)
				                         .OnComplete(() =>
				                         {
					                         newTab.SetSelected(true, false);
					                         previousTab?.SetSelected(false, false);
					                         OnTabSelectionComplete?.Invoke(_selectedIndex);
					                         OnTabItemSelectionComplete?.Invoke(newTab);
				                         })
				                         .Play();
			}
		}

		private void ApplyImmediateSelection(Vector2 targetPosition, TabItem newTab, TabItem previousTab)
		{
			_indicator.position = new Vector3(targetPosition.x, targetPosition.y, _indicator.position.z);
			newTab.SetSelected(true, true);
			previousTab?.SetSelected(false, true);
			OnTabSelectionComplete?.Invoke(_selectedIndex);
			OnTabItemSelectionComplete?.Invoke(newTab);
		}

		private void AdjustSelectedIndexAfterRemoval(int removedIndex)
		{
			if (_selectedIndex == removedIndex)
			{
				_selectedIndex = _tabs.Count > 0
					? Mathf.Min(removedIndex, _tabs.Count - 1)
					: -1;

				if (_selectedIndex >= 0)
					SelectTab(_selectedIndex, true);
			}
			else if (_selectedIndex > removedIndex)
			{
				_selectedIndex--;
			}
		}

		private TabItem GetTabOrNull(int index)
		{
			return index >= 0 && index < _tabs.Count ? _tabs[index] : null;
		}

		#endregion

		private void OnDestroy()
		{
			_currentIndicatorTween?.Kill();
		}
	}
}