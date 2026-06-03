using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AK.Systems;
using UnityEditor;
using UnityEngine;

namespace AK.Systems.Editor
{
	/// <summary>
	/// Editor window to visualize the current state of the V2 UI system.
	/// Shows channel stacks, per-parent fragment history, view pool, and validation.
	/// </summary>
	public class UIViewStackVisualizerWindowV2 : EditorWindow
	{
		[MenuItem("AK/UI/V2 - View Stack Visualizer")]
		public static void ShowWindow()
		{
			var window = GetWindow<UIViewStackVisualizerWindowV2>("V2 View Stack");
			window.Show();
		}

		private Vector2 _scrollPosition;
		private bool _showHUD = true;
		private bool _showMenu = true;
		private bool _showOverlay = true;
		private bool _showViewPool = true;
		private bool _showValidation = true;
		private bool _autoRefresh = true;
		private float _refreshInterval = 0.5f;
		private float _lastRefreshTime;

		// Collapsible state
		private Dictionary<UIChannel, bool> _channelCollapsed = new();
		private Dictionary<int, bool> _viewCollapsed = new();
		private bool _poolCollapsed;
		private bool _validationCollapsed;

		// Validation cache
		private List<ValidationIssue> _validationIssues = new();
		private int _lastValidationFrame = -1;

		// Cached reflection data (resolved once per repaint, not per draw)
		private Dictionary<UIChannel, Stack<UIView>> _channelStacks;
		private Dictionary<UIView, Stack<UIView>> _historyStacks;
		private Dictionary<UIView, ViewRecordInfo> _viewRegistry;
		private HashSet<UIView> _closingViews;
		private object _viewPool;
		private bool _dataValid;

		private void OnGUI()
		{
			EditorGUILayout.BeginVertical();
			EditorGUILayout.Space(10);

			EditorGUILayout.LabelField("V2 View Stack Visualizer", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"Visualizes the V2 UIView system state.\n" +
				"Channel stacks: views with UIViewChannel (screens).\n" +
				"History stacks: views without UIViewChannel (fragments) per parent.\n" +
				"Stack order: TOP (newest) to BOTTOM (oldest).",
				MessageType.Info);

			EditorGUILayout.Space(10);

			// Controls
			EditorGUILayout.BeginHorizontal();
			_autoRefresh = EditorGUILayout.Toggle("Auto Refresh", _autoRefresh);
			if (_autoRefresh)
			{
				_refreshInterval = EditorGUILayout.FloatField("Interval (s)", _refreshInterval);
				_refreshInterval = Mathf.Max(0.1f, _refreshInterval);
			}

			EditorGUILayout.EndHorizontal();

			if (GUILayout.Button("Refresh Now"))
			{
				_lastValidationFrame = -1;
				Repaint();
			}

			EditorGUILayout.Space(10);

			// Channel toggles
			EditorGUILayout.LabelField("Channels", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal();
			_showHUD = EditorGUILayout.Toggle("HUD", _showHUD);
			_showMenu = EditorGUILayout.Toggle("Menu", _showMenu);
			_showOverlay = EditorGUILayout.Toggle("Overlay", _showOverlay);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(5);

			// Debug tools
			EditorGUILayout.LabelField("Debug Tools", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal();
			_showViewPool = EditorGUILayout.Toggle("Show View Pool", _showViewPool);
			_showValidation = EditorGUILayout.Toggle("Show Validation", _showValidation);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(10);

			// Content
			_scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

			var viewSystem = FindFirstObjectByType<UISystem>();
			if (viewSystem == null)
			{
				EditorGUILayout.HelpBox(
					"No UIViewSystem found in scene. Make sure it is active.",
					MessageType.Warning);
			}
			else
			{
				DrawV2Stack(viewSystem);
			}

			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();

			if (_autoRefresh && EditorApplication.isPlaying &&
			    (Time.realtimeSinceStartup - _lastRefreshTime > _refreshInterval))
			{
				_lastRefreshTime = Time.realtimeSinceStartup;
				Repaint();
			}
		}

		private void OnInspectorUpdate()
		{
			if (_autoRefresh && EditorApplication.isPlaying)
			{
				Repaint();
			}
		}

		// ================================================================
		// DATA EXTRACTION VIA REFLECTION
		// ================================================================

		private void CacheReflectionData(UISystem uiSystem)
		{
			_dataValid = false;
			_channelStacks = null;
			_historyStacks = null;
			_viewRegistry = null;
			_closingViews = null;
			_viewPool = null;

			try
			{
				var sysType = typeof(UISystem);

				// _channelStacks
				var channelStacksField = sysType.GetField("_channelStacks",
					BindingFlags.NonPublic | BindingFlags.Instance);
				_channelStacks = channelStacksField?.GetValue(uiSystem)
					as Dictionary<UIChannel, Stack<UIView>>;

				// _historyStacks
				var historyStacksField = sysType.GetField("_historyStacks",
					BindingFlags.NonPublic | BindingFlags.Instance);
				_historyStacks = historyStacksField?.GetValue(uiSystem)
					as Dictionary<UIView, Stack<UIView>>;

				// _viewRegistry -> extract ViewRecord info
				var viewRegistryField = sysType.GetField("_viewRegistry",
					BindingFlags.NonPublic | BindingFlags.Instance);
				var rawRegistry = viewRegistryField?.GetValue(uiSystem)
					as Dictionary<UIView, object>;
				if (rawRegistry != null)
				{
					_viewRegistry = new Dictionary<UIView, ViewRecordInfo>();
					foreach (var kvp in rawRegistry)
					{
						_viewRegistry[kvp.Key] = ExtractViewRecordInfo(kvp.Value);
					}
				}

				// _closingViews
				var closingViewsField = sysType.GetField("_closingViews",
					BindingFlags.NonPublic | BindingFlags.Instance);
				_closingViews = closingViewsField?.GetValue(uiSystem) as HashSet<UIView>;

				// _viewPool
				var viewPoolField = sysType.GetField("_viewPool",
					BindingFlags.NonPublic | BindingFlags.Instance);
				_viewPool = viewPoolField?.GetValue(uiSystem);

				_dataValid = true;
			}
			catch (Exception ex)
			{
				EditorGUILayout.HelpBox(
					$"Reflection error: {ex.Message}", MessageType.Error);
			}
		}

		private static ViewRecordInfo ExtractViewRecordInfo(object record)
		{
			var info = new ViewRecordInfo();
			if (record == null) return info;

			var recordType = record.GetType();

			var instanceProp = recordType.GetProperty("Instance");
			if (instanceProp != null)
				info.Instance = instanceProp.GetValue(record) as UIView;

			var parentProp = recordType.GetProperty("Parent");
			if (parentProp != null)
				info.Parent = parentProp.GetValue(record) as UIView;

			var isStaticProp = recordType.GetProperty("IsStatic");
			if (isStaticProp != null)
				info.IsStatic = (bool)isStaticProp.GetValue(record);

			var isDynamicProp = recordType.GetProperty("IsDynamic");
			if (isDynamicProp != null)
				info.IsDynamic = (bool)isDynamicProp.GetValue(record);

			var childrenProp = recordType.GetProperty("Children");
			if (childrenProp != null)
			{
				var childList = childrenProp.GetValue(record) as List<UIView>;
				if (childList != null)
					info.Children = childList;
			}

			return info;
		}

		// ================================================================
		// MAIN DRAW
		// ================================================================

		private void DrawV2Stack(UISystem uiSystem)
		{
			CacheReflectionData(uiSystem);
			if (!_dataValid) return;

			// Summary
			int screenCount = _channelStacks?.Values.Sum(s => s?.Count ?? 0) ?? 0;
			int fragmentCount = _historyStacks?.Values.Sum(s => s?.Count ?? 0) ?? 0;
			int registeredCount = _viewRegistry?.Count ?? 0;
			int closingCount = _closingViews?.Count ?? 0;

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.LabelField("System Summary", EditorStyles.boldLabel);
			EditorGUILayout.LabelField($"Screens (in channel stacks): {screenCount}", EditorStyles.miniLabel);
			EditorGUILayout.LabelField($"Fragments (in history stacks): {fragmentCount}", EditorStyles.miniLabel);
			EditorGUILayout.LabelField($"Registered views: {registeredCount}", EditorStyles.miniLabel);
			if (closingCount > 0)
			{
				var warnStyle = new GUIStyle(EditorStyles.miniLabel);
				warnStyle.normal.textColor = Color.yellow;
				EditorGUILayout.LabelField($"Closing: {closingCount}", warnStyle);
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.Space(10);

			// Validation
			if (_showValidation)
			{
				RunValidation();
				DrawValidationResults();
			}

			// Channel stacks (screens)
			if (_showHUD) DrawChannel("HUD Channel", UIChannel.HUD, Color.cyan);
			if (_showMenu) DrawChannel("Menu Channel", UIChannel.Menu, Color.yellow);
			if (_showOverlay) DrawChannel("Overlay Channel", UIChannel.Overlay, Color.magenta);

			// Fragment history stacks (drawn per parent)
			DrawFragmentHistoryStacks();

			// View pool
			if (_showViewPool) DrawViewPool();
		}

		// ================================================================
		// CHANNEL STACKS
		// ================================================================

		private void DrawChannel(string channelName, UIChannel channel, Color channelColor)
		{
			if (!_channelCollapsed.ContainsKey(channel))
				_channelCollapsed[channel] = false;

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);

			var originalColor = GUI.backgroundColor;
			GUI.backgroundColor = channelColor;

			EditorGUILayout.BeginHorizontal();
			var wasCollapsed = _channelCollapsed[channel];
			var isCollapsed = EditorGUILayout.Toggle(wasCollapsed, GUILayout.Width(20));
			_channelCollapsed[channel] = isCollapsed;

			Stack<UIView> stack = null;
			int count = 0;
			if (_channelStacks != null && _channelStacks.TryGetValue(channel, out stack))
				count = stack?.Count ?? 0;

			EditorGUILayout.LabelField($"{channelName} ({count})", EditorStyles.boldLabel);
			EditorGUILayout.EndHorizontal();

			GUI.backgroundColor = originalColor;

			if (stack == null || count == 0)
			{
				EditorGUILayout.LabelField("No screens in this channel", EditorStyles.miniLabel);
				EditorGUILayout.EndVertical();
				EditorGUILayout.Space(5);
				return;
			}

			EditorGUILayout.Space(3);

			if (!isCollapsed)
			{
				var screenArray = stack.ToArray();
				for (int i = 0; i < screenArray.Length; i++)
				{
					DrawView(screenArray[i], isTop: i == 0, isScreen: true);
				}
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.Space(5);
		}

		// ================================================================
		// FRAGMENT HISTORY STACKS
		// ================================================================

		private void DrawFragmentHistoryStacks()
		{
			if (_historyStacks == null || _historyStacks.Count == 0)
				return;

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.LabelField($"Fragment History Stacks ({_historyStacks.Count} parents)",
				EditorStyles.boldLabel);
			EditorGUILayout.Space(3);

			foreach (var kvp in _historyStacks)
			{
				var parent = kvp.Key;
				var history = kvp.Value;

				if (parent == null || history == null || history.Count == 0)
					continue;

				int parentId = parent.GetInstanceID();
				if (!_viewCollapsed.ContainsKey(parentId))
					_viewCollapsed[parentId] = false;

				EditorGUILayout.BeginVertical(EditorStyles.helpBox);
				EditorGUILayout.BeginHorizontal();

				var wasCollapsed = _viewCollapsed[parentId];
				var isCollapsed = EditorGUILayout.Toggle(wasCollapsed, GUILayout.Width(20));
				_viewCollapsed[parentId] = isCollapsed;

				var parentStyle = new GUIStyle(EditorStyles.boldLabel);
				parentStyle.normal.textColor = new Color(0.7f, 0.7f, 1f);
				EditorGUILayout.LabelField(
					$"Parent: {SafeName(parent)} ({history.Count} fragments)", parentStyle);

				if (GUILayout.Button("Select", GUILayout.Width(50)))
				{
					Selection.activeGameObject = SafeGameObject(parent);
				}

				EditorGUILayout.EndHorizontal();

				if (!isCollapsed)
				{
					var fragArray = history.ToArray();
					for (int i = 0; i < fragArray.Length; i++)
					{
						DrawView(fragArray[i], isTop: i == 0, isScreen: false);
					}
				}

				EditorGUILayout.EndVertical();
				EditorGUILayout.Space(3);
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.Space(5);
		}

		// ================================================================
		// VIEW DRAWING
		// ================================================================

		private void DrawView(UIView view, bool isTop, bool isScreen)
		{
			if (view == null)
			{
				EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
				EditorGUILayout.LabelField("  NULL View Reference", EditorStyles.miniLabel);
				EditorGUILayout.EndHorizontal();
				return;
			}

			string goName = SafeName(view);
			if (goName == null)
			{
				var errorStyle = new GUIStyle(EditorStyles.miniLabel);
				errorStyle.normal.textColor = Color.red;
				EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
				EditorGUILayout.LabelField("  Destroyed View", errorStyle);
				EditorGUILayout.EndHorizontal();
				return;
			}

			int viewId = view.GetInstanceID();
			if (!_viewCollapsed.ContainsKey(viewId))
				_viewCollapsed[viewId] = true; // default collapsed for details

			EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

			// Icon + name
			var nameStyle = new GUIStyle(EditorStyles.miniLabel);
			if (isTop)
				nameStyle.normal.textColor = Color.green;
			else if (_closingViews != null && _closingViews.Contains(view))
				nameStyle.normal.textColor = Color.red;

			string icon = isTop ? "> " : "  ";
			string screenTag = isScreen ? "[S]" : "[F]";

			EditorGUILayout.BeginVertical();

			// Header row
			EditorGUILayout.BeginHorizontal();
			var wasCollapsed = _viewCollapsed[viewId];
			var isCollapsed = EditorGUILayout.Toggle(wasCollapsed, GUILayout.Width(16));
			_viewCollapsed[viewId] = isCollapsed;

			EditorGUILayout.LabelField($"{icon}{screenTag} {goName}", nameStyle);

			if (isTop)
			{
				var topStyle = new GUIStyle(EditorStyles.miniLabel);
				topStyle.normal.textColor = Color.green;
				EditorGUILayout.LabelField("TOP", topStyle, GUILayout.Width(30));
			}

			if (_closingViews != null && _closingViews.Contains(view))
			{
				var closeStyle = new GUIStyle(EditorStyles.miniLabel);
				closeStyle.normal.textColor = Color.red;
				EditorGUILayout.LabelField("CLOSING", closeStyle, GUILayout.Width(55));
			}

			// Select button
			if (GUILayout.Button("Select", GUILayout.Width(50)))
			{
				Selection.activeGameObject = SafeGameObject(view);
			}

			EditorGUILayout.EndHorizontal();

			// Details (when expanded)
			if (!isCollapsed)
			{
				EditorGUILayout.Space(2);

				EditorGUILayout.LabelField($"Type: {view.GetType().Name}", EditorStyles.miniLabel);

				if (!string.IsNullOrEmpty(view.ViewId))
					EditorGUILayout.LabelField($"ViewId: {view.ViewId}", EditorStyles.miniLabel);

				EditorGUILayout.LabelField($"Stack: {view.StackBehaviour}", EditorStyles.miniLabel);
				EditorGUILayout.LabelField($"HasChannel: {view.HasChannel}", EditorStyles.miniLabel);
				EditorGUILayout.LabelField($"IsVisible: {view.IsVisible}", EditorStyles.miniLabel);
				EditorGUILayout.LabelField($"PoolOnClose: {view.ReturnToPoolOnClose}", EditorStyles.miniLabel);
				EditorGUILayout.LabelField($"AllowMulti: {view.AllowMultipleInstances}", EditorStyles.miniLabel);

				if (view.HasChannel)
				{
					EditorGUILayout.LabelField(
						$"SortOrder: {view.Channel.SortOrder}", EditorStyles.miniLabel);
					var canvas = view.Channel.Canvas;
					if (canvas != null)
						EditorGUILayout.LabelField(
							$"Canvas.sortingOrder: {canvas.sortingOrder}", EditorStyles.miniLabel);
				}

				if (view.CanvasGroup != null)
				{
					EditorGUILayout.LabelField(
						$"Alpha: {view.CanvasGroup.alpha:F2}", EditorStyles.miniLabel);
					EditorGUILayout.LabelField(
						$"Interactable: {view.CanvasGroup.interactable}", EditorStyles.miniLabel);
					EditorGUILayout.LabelField(
						$"BlocksRaycasts: {view.CanvasGroup.blocksRaycasts}", EditorStyles.miniLabel);
				}

				// ViewRegistry info
				if (_viewRegistry != null && _viewRegistry.TryGetValue(view, out var record))
				{
					var tag = record.IsStatic ? "Static" : "Dynamic";
					var tagStyle = new GUIStyle(EditorStyles.miniLabel);
					tagStyle.normal.textColor = record.IsStatic ? Color.cyan : Color.yellow;
					EditorGUILayout.LabelField($"Registry: {tag}", tagStyle);

					if (record.Parent != null)
					{
						string parentName = SafeName(record.Parent) ?? "<destroyed>";
						EditorGUILayout.LabelField($"Parent: {parentName}", EditorStyles.miniLabel);
					}

					if (record.Children != null && record.Children.Count > 0)
					{
						int alive = record.Children.Count(c => c != null);
						EditorGUILayout.LabelField(
							$"Children: {alive}", EditorStyles.miniLabel);
					}
				}
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();
		}

		// ================================================================
		// VIEW POOL
		// ================================================================

		private void DrawViewPool()
		{
			if (_viewPool == null)
				return;

			var poolsField = _viewPool.GetType().GetField("_pools",
				BindingFlags.NonPublic | BindingFlags.Instance);
			if (poolsField == null)
				return;

			var pools = poolsField.GetValue(_viewPool);
			var poolsDict = pools as IDictionary;
			if (poolsDict == null || poolsDict.Count == 0)
			{
				EditorGUILayout.HelpBox("View pool is empty.", MessageType.Info);
				return;
			}

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);

			EditorGUILayout.BeginHorizontal();
			var wasCollapsed = _poolCollapsed;
			var isCollapsed = EditorGUILayout.Toggle(wasCollapsed, GUILayout.Width(20));
			_poolCollapsed = isCollapsed;

			var headerStyle = new GUIStyle(EditorStyles.boldLabel);
			headerStyle.normal.textColor = Color.orange;
			EditorGUILayout.LabelField($"View Pool ({poolsDict.Count} pools)", headerStyle);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(5);

			if (!isCollapsed)
			{
				int totalViews = 0;

				foreach (DictionaryEntry entry in poolsDict)
				{
					var poolKey = entry.Key;
					var viewStack = entry.Value as Stack<UIView>;

					if (viewStack == null || viewStack.Count == 0)
						continue;

					totalViews += viewStack.Count;

					// Extract PoolKey fields (Type + ViewId)
					var keyType = poolKey.GetType();
					var typeField = keyType.GetField("Type",
						BindingFlags.Public | BindingFlags.Instance);
					var viewIdField = keyType.GetField("ViewId",
						BindingFlags.Public | BindingFlags.Instance);

					var viewType = typeField?.GetValue(poolKey) as Type;
					var viewId = viewIdField?.GetValue(poolKey) as string;

					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					EditorGUILayout.LabelField(
						$"{viewType?.Name ?? "Unknown"}", EditorStyles.boldLabel);

					if (!string.IsNullOrEmpty(viewId))
						EditorGUILayout.LabelField($"ViewId: {viewId}", EditorStyles.miniLabel);

					EditorGUILayout.LabelField(
						$"Pooled: {viewStack.Count}", EditorStyles.miniLabel);

					// Show first few entries
					var array = viewStack.ToArray();
					int showCount = Mathf.Min(array.Length, 3);
					for (int i = 0; i < showCount; i++)
					{
						var v = array[i];
						EditorGUILayout.BeginHorizontal();
						if (v == null)
						{
							var errStyle = new GUIStyle(EditorStyles.miniLabel);
							errStyle.normal.textColor = Color.red;
							EditorGUILayout.LabelField("  NULL", errStyle);
						}
						else if (v.gameObject == null)
						{
							var errStyle = new GUIStyle(EditorStyles.miniLabel);
							errStyle.normal.textColor = Color.red;
							EditorGUILayout.LabelField("  Destroyed", errStyle);
						}
						else
						{
							EditorGUILayout.LabelField($"  {v.name}", EditorStyles.miniLabel);
							if (GUILayout.Button("Select", GUILayout.Width(50)))
								Selection.activeGameObject = v.gameObject;
						}

						EditorGUILayout.EndHorizontal();
					}

					if (array.Length > 3)
						EditorGUILayout.LabelField(
							$"  ... and {array.Length - 3} more", EditorStyles.miniLabel);

					EditorGUILayout.EndVertical();
					EditorGUILayout.Space(3);
				}

				EditorGUILayout.LabelField(
					$"Total Pooled Views: {totalViews}", EditorStyles.miniLabel);
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.Space(5);
		}

		// ================================================================
		// VALIDATION
		// ================================================================

		private void RunValidation()
		{
			if (Time.frameCount == _lastValidationFrame)
				return;

			_lastValidationFrame = Time.frameCount;
			_validationIssues.Clear();

			if (_viewRegistry == null)
				return;

			// Gather all views in channel stacks
			var viewsInChannels = new HashSet<UIView>();
			if (_channelStacks != null)
			{
				foreach (var stack in _channelStacks.Values)
				{
					if (stack == null) continue;
					foreach (var v in stack)
					{
						if (v != null) viewsInChannels.Add(v);
					}
				}
			}

			// Gather all views in history stacks
			var viewsInHistory = new HashSet<UIView>();
			if (_historyStacks != null)
			{
				foreach (var kvp in _historyStacks)
				{
					if (kvp.Value == null) continue;
					foreach (var v in kvp.Value)
					{
						if (v != null) viewsInHistory.Add(v);
					}
				}
			}

			// Validate view registry
			foreach (var kvp in _viewRegistry)
			{
				var view = kvp.Key;
				var record = kvp.Value;

				if (view == null)
				{
					_validationIssues.Add(new ValidationIssue
					{
						Severity = IssueSeverity.Error,
						Category = "Registry",
						Message = "Null view key in registry"
					});
					continue;
				}

				// Check if destroyed
				if (!IsAlive(view))
				{
					_validationIssues.Add(new ValidationIssue
					{
						Severity = IssueSeverity.Error,
						Category = "Destroyed",
						Message = $"View of type {view.GetType().Name} is destroyed but still in registry",
						View = view
					});
					continue;
				}

				// Check screen consistency: views with a channel should be in a channel stack
				if (view.HasChannel && !viewsInChannels.Contains(view) &&
				    _closingViews?.Contains(view) != true)
				{
					_validationIssues.Add(new ValidationIssue
					{
						Severity = IssueSeverity.Warning,
						Category = "Channel",
						Message = $"Screen '{view.name}' has UIViewChannel but is not in any channel stack",
						View = view
					});
				}

				// Check fragment consistency: views without channel should have a history or be static
				if (!view.HasChannel && !viewsInHistory.Contains(view) && !record.IsStatic &&
				    _closingViews?.Contains(view) != true)
				{
					_validationIssues.Add(new ValidationIssue
					{
						Severity = IssueSeverity.Info,
						Category = "History",
						Message = $"Dynamic fragment '{view.name}' is registered but not in any history stack",
						View = view
					});
				}

				// Check parent consistency
				if (record.Parent != null && !IsAlive(record.Parent))
				{
					_validationIssues.Add(new ValidationIssue
					{
						Severity = IssueSeverity.Error,
						Category = "Parent",
						Message = $"View '{view.name}' has destroyed parent",
						View = view
					});
				}
				else if (record.Parent != null && !_viewRegistry.ContainsKey(record.Parent))
				{
					_validationIssues.Add(new ValidationIssue
					{
						Severity = IssueSeverity.Warning,
						Category = "Parent",
						Message = $"View '{view.name}' has parent '{record.Parent.name}' that is not in registry",
						View = view
					});
				}

				// Check child consistency
				if (record.Children != null)
				{
					foreach (var child in record.Children)
					{
						if (child == null) continue;

						if (!_viewRegistry.ContainsKey(child))
						{
							_validationIssues.Add(new ValidationIssue
							{
								Severity = IssueSeverity.Error,
								Category = "Parent-Child",
								Message =
									$"View '{view.name}' has child '{child.name}' that is not in registry",
								View = view
							});
						}
					}
				}
			}

			// Check for views in history but not in registry
			foreach (var v in viewsInHistory)
			{
				if (v != null && !_viewRegistry.ContainsKey(v))
				{
					_validationIssues.Add(new ValidationIssue
					{
						Severity = IssueSeverity.Error,
						Category = "History",
						Message = $"View '{v.name}' is in history stack but not in registry",
						View = v
					});
				}
			}

			// Check for views in channel stacks but not in registry
			foreach (var v in viewsInChannels)
			{
				if (v != null && !_viewRegistry.ContainsKey(v))
				{
					_validationIssues.Add(new ValidationIssue
					{
						Severity = IssueSeverity.Error,
						Category = "Channel",
						Message = $"View '{v.name}' is in channel stack but not in registry",
						View = v
					});
				}
			}
		}

		private void DrawValidationResults()
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);

			EditorGUILayout.BeginHorizontal();
			var wasCollapsed = _validationCollapsed;
			var isCollapsed = EditorGUILayout.Toggle(wasCollapsed, GUILayout.Width(20));
			_validationCollapsed = isCollapsed;

			var headerStyle = new GUIStyle(EditorStyles.boldLabel);
			int issueCount = _validationIssues.Count;

			if (issueCount > 0)
			{
				var errorCount = _validationIssues.Count(i => i.Severity == IssueSeverity.Error);
				var warningCount = _validationIssues.Count(i => i.Severity == IssueSeverity.Warning);

				if (errorCount > 0)
					headerStyle.normal.textColor = Color.red;
				else if (warningCount > 0)
					headerStyle.normal.textColor = Color.yellow;
				else
					headerStyle.normal.textColor = Color.cyan;

				EditorGUILayout.LabelField(
					$"Validation ({errorCount} errors, {warningCount} warnings)", headerStyle);
			}
			else
			{
				headerStyle.normal.textColor = Color.green;
				EditorGUILayout.LabelField("Validation Passed", headerStyle);
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(5);

			if (!isCollapsed && _validationIssues.Count > 0)
			{
				var errors = _validationIssues.Where(i => i.Severity == IssueSeverity.Error).ToList();
				var warnings = _validationIssues.Where(i => i.Severity == IssueSeverity.Warning).ToList();
				var infos = _validationIssues.Where(i => i.Severity == IssueSeverity.Info).ToList();

				if (errors.Count > 0)
				{
					EditorGUILayout.LabelField("Errors", EditorStyles.boldLabel);
					foreach (var issue in errors) DrawValidationIssue(issue);
					EditorGUILayout.Space(3);
				}

				if (warnings.Count > 0)
				{
					EditorGUILayout.LabelField("Warnings", EditorStyles.boldLabel);
					foreach (var issue in warnings) DrawValidationIssue(issue);
					EditorGUILayout.Space(3);
				}

				if (infos.Count > 0)
				{
					EditorGUILayout.LabelField("Info", EditorStyles.boldLabel);
					foreach (var issue in infos) DrawValidationIssue(issue);
				}
			}
			else if (!isCollapsed && _validationIssues.Count == 0)
			{
				EditorGUILayout.HelpBox("No issues found. All references are valid.", MessageType.Info);
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.Space(5);
		}

		private void DrawValidationIssue(ValidationIssue issue)
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

			var color = issue.Severity == IssueSeverity.Error ? Color.red :
				issue.Severity == IssueSeverity.Warning ? Color.yellow : Color.cyan;

			var style = new GUIStyle(EditorStyles.miniLabel);
			style.normal.textColor = color;

			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField($"[{issue.Category}] {issue.Message}", style);
			if (!string.IsNullOrEmpty(issue.Context))
				EditorGUILayout.LabelField($"  Context: {issue.Context}", EditorStyles.miniLabel);
			EditorGUILayout.EndVertical();

			if (issue.View != null && GUILayout.Button("Select", GUILayout.Width(50)))
			{
				var go = SafeGameObject(issue.View);
				if (go != null) Selection.activeGameObject = go;
			}

			EditorGUILayout.EndHorizontal();
		}

		// ================================================================
		// HELPERS
		// ================================================================

		private static string SafeName(UIView view)
		{
			try { return view?.name; }
			catch { return null; }
		}

		private static GameObject SafeGameObject(UIView view)
		{
			try { return view?.gameObject; }
			catch { return null; }
		}

		private static bool IsAlive(UIView view)
		{
			try { return view != null && view.gameObject != null; }
			catch { return false; }
		}

		// ================================================================
		// NESTED TYPES
		// ================================================================

		private enum IssueSeverity
		{
			Info,
			Warning,
			Error
		}

		private class ValidationIssue
		{
			public IssueSeverity Severity;
			public string Category;
			public string Message;
			public string Context;
			public UIView View;
		}

		private class ViewRecordInfo
		{
			public UIView Instance;
			public UIView Parent;
			public bool IsStatic;
			public bool IsDynamic;
			public List<UIView> Children = new();
		}
	}
}