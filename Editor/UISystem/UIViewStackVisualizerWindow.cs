using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AK.UISystem;
using UnityEditor;
using UnityEngine;

namespace AK.UISystem.Editor
{
	/// <summary>
	/// Editor window to visualize the current state of V2 UI views.
	/// Shows channel stacks (screens with UIViewChannel) and per-parent history stacks (fragments).
	/// Unified single UIView concept with optional UIViewChannel determines stack placement.
	/// Includes validation to detect stale references and inconsistent state.
	/// </summary>
	public class UIViewStackVisualizerWindow : EditorWindow
	{
		[MenuItem("AK/UI/UI View Stack Visualizer (V2)")]
		public static void ShowWindow()
		{
			var window = GetWindow<UIViewStackVisualizerWindow>("UI View Stack Visualizer (V2)");
			window.Show();
		}

		private Vector2 _scrollPosition;
		private bool _showChannelStacks = true;
		private bool _showHistoryStacks = true;
		private bool _showViewPool = true;
		private bool _showValidation = true;
		private bool _autoRefresh = true;
		private float _refreshInterval = 0.5f;
		private float _lastRefreshTime;

		// Collapsible state tracking
		private Dictionary<UIChannel, bool> _channelCollapsed = new Dictionary<UIChannel, bool>();
		private Dictionary<UIView, bool> _parentCollapsed = new Dictionary<UIView, bool>();
		private bool _viewPoolCollapsed = false;
		private bool _validationCollapsed = false;

		// Validation results cache
		private List<ValidationIssue> _validationIssues = new List<ValidationIssue>();
		private int _lastValidationFrame = -1;

		private void OnGUI()
		{
			EditorGUILayout.BeginVertical();
			EditorGUILayout.Space(10);

			// Header
			EditorGUILayout.LabelField("UI View Stack Visualizer (V2)", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox(
				"Visualizes the unified UIView hierarchy in V2.\n" +
				"Views with UIViewChannel are shown in channel stacks (screens).\n" +
				"Views without UIViewChannel are shown in per-parent history stacks (fragments).\n" +
				"Validation checks for stale references and inconsistent state.",
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
				_lastValidationFrame = -1; // Force re-validation
				Repaint();
			}

			EditorGUILayout.Space(10);

			// Content toggles
			EditorGUILayout.LabelField("Content", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal();
			_showChannelStacks = EditorGUILayout.Toggle("Channel Stacks", _showChannelStacks);
			_showHistoryStacks = EditorGUILayout.Toggle("History Stacks", _showHistoryStacks);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			_showViewPool = EditorGUILayout.Toggle("Show View Pool", _showViewPool);
			_showValidation = EditorGUILayout.Toggle("Show Validation", _showValidation);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(10);

			// Content
			_scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

			var viewSystem = FindObjectOfType<UIViewSystem>();
			if (viewSystem == null)
			{
				EditorGUILayout.HelpBox(
					"No UIViewSystem found in the scene. Make sure the UIViewSystem is active.",
					MessageType.Warning);
			}
			else
			{
				DrawViewHierarchy(viewSystem);
			}

			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();

			// Auto refresh
			if (_autoRefresh && EditorApplication.isPlaying && (Time.realtimeSinceStartup - _lastRefreshTime > _refreshInterval))
			{
				_lastRefreshTime = Time.realtimeSinceStartup;
				Repaint();
			}
		}

		private void DrawViewHierarchy(UIViewSystem viewSystem)
		{
			// Use reflection to access private fields
			var channelStacksField = typeof(UIViewSystem).GetField("_channelStacks",
				BindingFlags.NonPublic | BindingFlags.Instance);
			var historyStacksField = typeof(UIViewSystem).GetField("_historyStacks",
				BindingFlags.NonPublic | BindingFlags.Instance);
			var viewRegistryField = typeof(UIViewSystem).GetField("_viewRegistry",
				BindingFlags.NonPublic | BindingFlags.Instance);

			if (channelStacksField == null || historyStacksField == null || viewRegistryField == null)
			{
				EditorGUILayout.HelpBox(
					"Could not access UIViewSystem internals. The system structure may have changed.",
					MessageType.Error);
				return;
			}

			var channelStacks = channelStacksField.GetValue(viewSystem) as Dictionary<UIChannel, Stack<UIView>>;
			var historyStacks = historyStacksField.GetValue(viewSystem) as Dictionary<UIView, Stack<UIView>>;
			var viewRegistry = viewRegistryField.GetValue(viewSystem) as Dictionary<UIView, object>;

			if (channelStacks == null)
			{
				EditorGUILayout.HelpBox("Channel stacks dictionary is null.", MessageType.Error);
				return;
			}

			// Run validation first
			if (_showValidation)
			{
				RunValidation(viewSystem, channelStacks, historyStacks, viewRegistry);
				DrawValidationResults();
			}

			// Draw channel stacks (screens)
			if (_showChannelStacks)
			{
				DrawChannelStacks(channelStacks, viewRegistry);
			}

			// Draw history stacks (per-parent fragments)
			if (_showHistoryStacks && historyStacks != null)
			{
				DrawHistoryStacks(historyStacks, viewRegistry);
			}

			// Draw view pool
			if (_showViewPool)
			{
				DrawViewPool(viewSystem);
			}
		}

		#region Validation

		private void RunValidation(UIViewSystem viewSystem, Dictionary<UIChannel, Stack<UIView>> channelStacks,
			Dictionary<UIView, Stack<UIView>> historyStacks, Dictionary<UIView, object> viewRegistry)
		{
			// Only run validation once per frame
			if (Time.frameCount == _lastValidationFrame)
				return;

			_lastValidationFrame = Time.frameCount;
			_validationIssues.Clear();

			try
			{
				if (viewRegistry == null)
				{
					_validationIssues.Add(new ValidationIssue
					{
						Severity = IssueSeverity.Error,
						Category = "Registry",
						Message = "View registry is null",
						Context = "RunValidation"
					});
					return;
				}

				// Track all views in channel stacks
				var viewsInChannels = new HashSet<UIView>();
				foreach (var stack in channelStacks.Values)
				{
					if (stack != null)
					{
						foreach (var view in stack)
						{
							if (view != null)
								viewsInChannels.Add(view);
						}
					}
				}

				// Track all views in history stacks
				var viewsInHistory = new HashSet<UIView>();
				if (historyStacks != null)
				{
					foreach (var stack in historyStacks.Values)
					{
						if (stack != null)
						{
							foreach (var view in stack)
							{
								if (view != null)
									viewsInHistory.Add(view);
							}
						}
					}
				}

				// Validate each view in registry
				foreach (var kvp in viewRegistry)
				{
					try
					{
						var view = kvp.Key;
						var record = kvp.Value;

						if (view == null)
						{
							_validationIssues.Add(new ValidationIssue
							{
								Severity = IssueSeverity.Error,
								Category = "Registry",
								Message = "Null view key in registry",
								Context = "ViewRegistry"
							});
							continue;
						}

						// Check if view's GameObject is destroyed
						try
						{
							if (view.gameObject == null)
							{
								_validationIssues.Add(new ValidationIssue
								{
									Severity = IssueSeverity.Error,
									Category = "Destroyed",
									Message = $"View '{view.name}' GameObject is destroyed but still in registry",
									Context = view.name,
									View = view
								});
								continue;
							}
						}
						catch (MissingReferenceException)
						{
							_validationIssues.Add(new ValidationIssue
							{
								Severity = IssueSeverity.Error,
								Category = "Destroyed",
								Message = $"View of type {view.GetType().Name} is destroyed but still in registry",
								Context = view.GetType().Name
							});
							continue;
						}

						// Get parent from record
						var parentView = GetParentFromRecord(record);

						// Check parent-child consistency
						if (parentView != null && viewRegistry.TryGetValue(parentView, out var parentRecord))
						{
							var parentChildren = GetChildrenFromRecord(parentRecord);
							if (!parentChildren.Contains(view))
							{
								_validationIssues.Add(new ValidationIssue
								{
									Severity = IssueSeverity.Warning,
									Category = "Parent-Child",
									Message = $"View '{view.name}' has parent '{parentView.name}' but parent's children list doesn't contain it",
									Context = view.name,
									View = view
								});
							}
						}

						// Validate children
						var children = GetChildrenFromRecord(record);
						foreach (var child in children)
						{
							if (child == null)
							{
								_validationIssues.Add(new ValidationIssue
								{
									Severity = IssueSeverity.Warning,
									Category = "Parent-Child",
									Message = $"View '{view.name}' has null child in children list",
									Context = view.name,
									View = view
								});
								continue;
							}

							if (!viewRegistry.ContainsKey(child))
							{
								_validationIssues.Add(new ValidationIssue
								{
									Severity = IssueSeverity.Error,
									Category = "Parent-Child",
									Message = $"View '{view.name}' has child '{child.name}' that is not in registry",
									Context = view.name,
									View = view
								});
							}
						}
					}
					catch (MissingReferenceException)
					{
						// View was destroyed during validation, skip it
					}
					catch (Exception ex)
					{
						_validationIssues.Add(new ValidationIssue
						{
							Severity = IssueSeverity.Warning,
							Category = "Validation",
							Message = $"Error validating view: {ex.Message}",
							Context = "RunValidation"
						});
					}
				}

				// Check for orphan views in history
				foreach (var view in viewsInHistory)
				{
					if (view != null && !viewRegistry.ContainsKey(view))
					{
						_validationIssues.Add(new ValidationIssue
						{
							Severity = IssueSeverity.Error,
							Category = "History",
							Message = $"View '{view.name}' is in history stack but not in registry",
							Context = view.name,
							View = view
						});
					}
				}
			}
			catch (Exception ex)
			{
				_validationIssues.Add(new ValidationIssue
				{
					Severity = IssueSeverity.Error,
					Category = "Validation",
					Message = $"Validation error: {ex.Message}",
					Context = "RunValidation"
				});
			}
		}

		private UIView GetParentFromRecord(object record)
		{
			if (record == null) return null;
			var parentProp = record.GetType().GetProperty("Parent");
			return parentProp?.GetValue(record) as UIView;
		}

		private List<UIView> GetChildrenFromRecord(object record)
		{
			if (record == null) return new List<UIView>();
			var childrenProp = record.GetType().GetProperty("Children");
			return childrenProp?.GetValue(record) as List<UIView> ?? new List<UIView>();
		}

		private void DrawValidationResults()
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);

			// Validation header with collapse toggle
			EditorGUILayout.BeginHorizontal();
			var wasCollapsed = _validationCollapsed;
			var isCollapsed = EditorGUILayout.Toggle(wasCollapsed, GUILayout.Width(20));
			_validationCollapsed = isCollapsed;

			var headerStyle = new GUIStyle(EditorStyles.boldLabel);
			var issueCount = _validationIssues.Count;
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

				EditorGUILayout.LabelField($"🔍 Validation ({errorCount} errors, {warningCount} warnings)", headerStyle);
			}
			else
			{
				headerStyle.normal.textColor = Color.green;
				EditorGUILayout.LabelField("✓ Validation Passed", headerStyle);
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(5);

			if (!isCollapsed && _validationIssues.Count > 0)
			{
				// Group by severity
				var errors = _validationIssues.Where(i => i.Severity == IssueSeverity.Error).ToList();
				var warnings = _validationIssues.Where(i => i.Severity == IssueSeverity.Warning).ToList();
				var infos = _validationIssues.Where(i => i.Severity == IssueSeverity.Info).ToList();

				if (errors.Count > 0)
				{
					EditorGUILayout.LabelField("❌ Errors", EditorStyles.boldLabel);
					foreach (var issue in errors)
					{
						DrawValidationIssue(issue);
					}
					EditorGUILayout.Space(3);
				}

				if (warnings.Count > 0)
				{
					EditorGUILayout.LabelField("⚠️ Warnings", EditorStyles.boldLabel);
					foreach (var issue in warnings)
					{
						DrawValidationIssue(issue);
					}
					EditorGUILayout.Space(3);
				}

				if (infos.Count > 0)
				{
					EditorGUILayout.LabelField("ℹ️ Info", EditorStyles.boldLabel);
					foreach (var issue in infos)
					{
						DrawValidationIssue(issue);
					}
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
			{
				EditorGUILayout.LabelField($"  Context: {issue.Context}", EditorStyles.miniLabel);
			}
			EditorGUILayout.EndVertical();

			// Select button
			if (issue.View != null && GUILayout.Button("Select", GUILayout.Width(50)))
			{
				Selection.activeGameObject = issue.View.gameObject;
			}

			EditorGUILayout.EndHorizontal();
		}

		#endregion

		private void DrawChannelStacks(Dictionary<UIChannel, Stack<UIView>> channelStacks, Dictionary<UIView, object> viewRegistry)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.LabelField("Channel Stacks (Screens)", EditorStyles.boldLabel);
			EditorGUILayout.Space(5);

			if (channelStacks.Count == 0)
			{
				EditorGUILayout.LabelField("No channel stacks found.", EditorStyles.miniLabel);
				EditorGUILayout.EndVertical();
				return;
			}

			foreach (var kvp in channelStacks)
			{
				var channel = kvp.Key;
				var stack = kvp.Value;

				DrawChannelStack(channel, stack, viewRegistry);
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.Space(5);
		}

		private void DrawChannelStack(UIChannel channel, Stack<UIView> stack, Dictionary<UIView, object> viewRegistry)
		{
			if (!_channelCollapsed.ContainsKey(channel))
			{
				_channelCollapsed[channel] = false;
			}

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);

			// Channel header with collapse toggle
			var originalColor = GUI.backgroundColor;
			GUI.backgroundColor = new Color(0.3f, 0.3f, 0.5f);

			EditorGUILayout.BeginHorizontal();
			var wasCollapsed = _channelCollapsed[channel];
			var isCollapsed = EditorGUILayout.Toggle(wasCollapsed, GUILayout.Width(20));
			_channelCollapsed[channel] = isCollapsed;

			EditorGUILayout.LabelField($"Channel {channel}", EditorStyles.boldLabel);
			EditorGUILayout.LabelField($"({stack.Count})", EditorStyles.miniLabel);
			EditorGUILayout.EndHorizontal();

			GUI.backgroundColor = originalColor;

			if (stack.Count == 0)
			{
				EditorGUILayout.LabelField("No views in this channel", EditorStyles.miniLabel);
				EditorGUILayout.EndVertical();
				return;
			}

			EditorGUILayout.Space(5);

			// Draw views from TOP to BOTTOM
			if (!isCollapsed)
			{
				var viewArray = stack.ToArray();
				for (int i = 0; i < viewArray.Length; i++)
				{
					var view = viewArray[i];
					DrawView(view, i == 0, isTopLevel: true, viewRegistry);
				}
			}

			EditorGUILayout.EndVertical();
		}

		private void DrawHistoryStacks(Dictionary<UIView, Stack<UIView>> historyStacks, Dictionary<UIView, object> viewRegistry)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.LabelField("History Stacks (Fragments)", EditorStyles.boldLabel);
			EditorGUILayout.Space(5);

			if (historyStacks.Count == 0)
			{
				EditorGUILayout.LabelField("No history stacks found.", EditorStyles.miniLabel);
				EditorGUILayout.EndVertical();
				return;
			}

			foreach (var kvp in historyStacks)
			{
				var parent = kvp.Key;
				var stack = kvp.Value;

				if (parent == null || stack == null || stack.Count == 0)
					continue;

				DrawHistoryStack(parent, stack, viewRegistry);
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.Space(5);
		}

		private void DrawHistoryStack(UIView parent, Stack<UIView> stack, Dictionary<UIView, object> viewRegistry)
		{
			if (!_parentCollapsed.ContainsKey(parent))
			{
				_parentCollapsed[parent] = false;
			}

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);

			// Parent header with collapse toggle
			var originalColor = GUI.backgroundColor;
			GUI.backgroundColor = new Color(0.3f, 0.5f, 0.3f);

			EditorGUILayout.BeginHorizontal();
			var wasCollapsed = _parentCollapsed[parent];
			var isCollapsed = EditorGUILayout.Toggle(wasCollapsed, GUILayout.Width(20));
			_parentCollapsed[parent] = isCollapsed;

			EditorGUILayout.LabelField($"Parent: {parent.name}", EditorStyles.boldLabel);
			EditorGUILayout.LabelField($"({stack.Count})", EditorStyles.miniLabel);

			// Select parent button
			if (GUILayout.Button("Select", GUILayout.Width(50)))
			{
				Selection.activeGameObject = parent.gameObject;
			}

			EditorGUILayout.EndHorizontal();

			GUI.backgroundColor = originalColor;

			EditorGUILayout.Space(5);

			// Draw fragments from TOP to BOTTOM
			if (!isCollapsed)
			{
				var viewArray = stack.ToArray();
				for (int i = 0; i < viewArray.Length; i++)
				{
					var view = viewArray[i];
					DrawView(view, i == 0, isTopLevel: false, viewRegistry);
				}
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.Space(3);
		}

		private void DrawView(UIView view, bool isTop, bool isTopLevel, Dictionary<UIView, object> viewRegistry)
		{
			if (view == null)
			{
				EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
				EditorGUILayout.LabelField("⚠️ NULL View Reference", EditorStyles.miniLabel);
				EditorGUILayout.EndHorizontal();
				return;
			}

			try
			{
				if (view.gameObject == null)
				{
					EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
					var errorStyle = new GUIStyle(EditorStyles.miniLabel);
					errorStyle.normal.textColor = Color.red;
					EditorGUILayout.LabelField($"⚠️ Destroyed: {view.GetType().Name}", errorStyle);
					EditorGUILayout.EndHorizontal();
					return;
				}
			}
			catch (MissingReferenceException)
			{
				EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
				var errorStyle = new GUIStyle(EditorStyles.miniLabel);
				errorStyle.normal.textColor = Color.red;
				EditorGUILayout.LabelField("⚠️ Destroyed View", errorStyle);
				EditorGUILayout.EndHorizontal();
				return;
			}

			EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

			// View icon
			var icon = isTop ? "🔝" : "  ";
			EditorGUILayout.LabelField(icon, GUILayout.Width(25));

			EditorGUILayout.BeginVertical();

			try
			{
				// View name
				var nameStyle = new GUIStyle(EditorStyles.miniLabel);
				if (isTop)
				{
					nameStyle.normal.textColor = Color.green;
				}
				EditorGUILayout.LabelField(view.name, nameStyle);

				// View info
				EditorGUILayout.LabelField($"Type: {view.GetType().Name}", EditorStyles.miniLabel);

				var viewId = GetViewId(view);
				if (!string.IsNullOrEmpty(viewId))
				{
					EditorGUILayout.LabelField($"ID: {viewId}", EditorStyles.miniLabel);
				}

				// Channel info if applicable
				var channel = GetViewChannel(view);
				if (channel.HasValue)
				{
					EditorGUILayout.LabelField($"Channel: {channel}", EditorStyles.miniLabel);
				}
				else
				{
					var fragmentStyle = new GUIStyle(EditorStyles.miniLabel);
					fragmentStyle.normal.textColor = Color.yellow;
					EditorGUILayout.LabelField("Type: Fragment", fragmentStyle);
				}

				// Stack behavior
				var stackBehavior = GetStackBehavior(view);
				EditorGUILayout.LabelField($"Stack Behavior: {stackBehavior}", EditorStyles.miniLabel);

				// Canvas group info
				if (view.GetComponent<CanvasGroup>() is CanvasGroup cg)
				{
					EditorGUILayout.LabelField($"Alpha: {cg.alpha:F2}", EditorStyles.miniLabel);
					EditorGUILayout.LabelField($"Interactable: {cg.interactable}", EditorStyles.miniLabel);
				}

				// Get additional info from registry
				if (viewRegistry != null && viewRegistry.TryGetValue(view, out var record))
				{
					var isStatic = GetIsStaticFromRecord(record);
					var children = GetChildrenFromRecord(record);

					var staticStyle = new GUIStyle(EditorStyles.miniLabel);
					staticStyle.normal.textColor = isStatic ? Color.cyan : Color.yellow;
					EditorGUILayout.LabelField($"Type: {(isStatic ? "Static" : "Dynamic")}", staticStyle);

					if (children.Count > 0)
					{
						EditorGUILayout.LabelField($"Children: {children.Count}", EditorStyles.miniLabel);
					}
				}
			}
			catch (MissingReferenceException)
			{
				EditorGUILayout.LabelField("⚠️ View was destroyed during rendering", EditorStyles.miniLabel);
			}
			catch (Exception ex)
			{
				EditorGUILayout.LabelField($"Error: {ex.Message}", EditorStyles.miniLabel);
			}

			EditorGUILayout.EndVertical();

			// Select button
			try
			{
				if (view != null && view.gameObject != null && GUILayout.Button("Select", GUILayout.Width(50)))
				{
					Selection.activeGameObject = view.gameObject;
				}
			}
			catch
			{
				// Ignore if view was destroyed
			}

			EditorGUILayout.EndHorizontal();
		}

		private string GetViewId(UIView view)
		{
			try
			{
				var field = typeof(UIView).GetField("_viewId", BindingFlags.NonPublic | BindingFlags.Instance);
				return field?.GetValue(view) as string ?? "";
			}
			catch
			{
				return "";
			}
		}

		private UIChannel? GetViewChannel(UIView view)
		{
			try
			{
				var channelComponent = view.GetComponent<UIViewChannel>();
				if (channelComponent != null)
				{
					var sortOrderField = typeof(UIViewChannel).GetProperty("SortOrder");
					if (sortOrderField != null)
					{
						var sortOrder = (int)sortOrderField.GetValue(channelComponent);
						return (UIChannel)sortOrder;
					}
				}
				return null;
			}
			catch
			{
				return null;
			}
		}

		private string GetStackBehavior(UIView view)
		{
			try
			{
				var prop = typeof(UIView).GetProperty("StackBehaviour");
				if (prop != null)
				{
					var value = prop.GetValue(view);
					return value?.ToString() ?? "Unknown";
				}
			}
			catch
			{
			}
			return "Unknown";
		}

		private bool GetIsStaticFromRecord(object record)
		{
			try
			{
				var prop = record.GetType().GetProperty("IsStatic");
				if (prop != null)
					return (bool)prop.GetValue(record);
			}
			catch
			{
			}
			return false;
		}

		private void DrawViewPool(UIViewSystem viewSystem)
		{
			var viewPoolField = typeof(UIViewSystem).GetField("_viewPool",
				BindingFlags.NonPublic | BindingFlags.Instance);

			if (viewPoolField == null)
			{
				EditorGUILayout.HelpBox(
					"Could not access view pool. The UIViewSystem structure may have changed.",
					MessageType.Error);
				return;
			}

			var viewPool = viewPoolField.GetValue(viewSystem);
			if (viewPool == null)
			{
				EditorGUILayout.HelpBox("View pool is null.", MessageType.Warning);
				return;
			}

			// Access the pools
			var poolsField = viewPool.GetType().GetField("_pools",
				BindingFlags.NonPublic | BindingFlags.Instance);

			if (poolsField == null)
			{
				EditorGUILayout.HelpBox(
					"Could not access pools. The ViewPool structure may have changed.",
					MessageType.Error);
				return;
			}

			var pools = poolsField.GetValue(viewPool);
			if (pools == null)
			{
				EditorGUILayout.HelpBox("Pools dictionary is null.", MessageType.Warning);
				return;
			}

			var poolsDict = pools as System.Collections.IDictionary;
			if (poolsDict == null || poolsDict.Count == 0)
			{
				EditorGUILayout.HelpBox("No views in pool.", MessageType.Info);
				return;
			}

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);

			// View pool header with collapse toggle
			EditorGUILayout.BeginHorizontal();
			var wasCollapsed = _viewPoolCollapsed;
			var isCollapsed = EditorGUILayout.Toggle(wasCollapsed, GUILayout.Width(20));
			_viewPoolCollapsed = isCollapsed;

			var headerStyle = new GUIStyle(EditorStyles.boldLabel);
			headerStyle.normal.textColor = Color.orange;
			EditorGUILayout.LabelField($"🔄 View Pool ({poolsDict.Count} types)", headerStyle);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(5);

			if (!isCollapsed)
			{
				int totalViews = 0;

				foreach (System.Collections.DictionaryEntry entry in poolsDict)
				{
					var poolKey = entry.Key;
					var viewStack = entry.Value as Stack<UIView>;

					if (viewStack != null && viewStack.Count > 0)
					{
						totalViews += viewStack.Count;

						// Get pool key type and id
						var keyType = poolKey.GetType();
						var typeField = keyType.GetField("Type", BindingFlags.Public | BindingFlags.Instance);
						var idField = keyType.GetField("ID", BindingFlags.Public | BindingFlags.Instance);

						if (typeField != null && idField != null)
						{
							var viewType = typeField.GetValue(poolKey) as Type;
							var viewId = idField.GetValue(poolKey) as string;

							EditorGUILayout.BeginVertical(EditorStyles.helpBox);

							EditorGUILayout.LabelField($"📦 {viewType?.Name ?? "Unknown"}", EditorStyles.boldLabel);

							if (!string.IsNullOrEmpty(viewId))
							{
								EditorGUILayout.LabelField($"ID: {viewId}", EditorStyles.miniLabel);
							}

							EditorGUILayout.LabelField($"Count: {viewStack.Count}", EditorStyles.miniLabel);

							EditorGUILayout.Space(3);

							// Show first few views in the pool
							var viewArray = viewStack.ToArray();
							int showCount = Mathf.Min(viewArray.Length, 3);
							for (int i = 0; i < showCount; i++)
							{
								var pooledView = viewArray[i];

								EditorGUILayout.BeginHorizontal();
								EditorGUILayout.LabelField($"  • {pooledView?.name ?? "<null>"}", EditorStyles.miniLabel);

								// Check for destroyed views in pool
								if (pooledView == null)
								{
									var errorStyle = new GUIStyle(EditorStyles.miniLabel);
									errorStyle.normal.textColor = Color.red;
									EditorGUILayout.LabelField("⚠️ NULL", errorStyle);
								}
								else if (pooledView.gameObject == null)
								{
									var errorStyle = new GUIStyle(EditorStyles.miniLabel);
									errorStyle.normal.textColor = Color.red;
									EditorGUILayout.LabelField("⚠️ Destroyed", errorStyle);
								}

								EditorGUILayout.EndHorizontal();
							}

							if (viewArray.Length > 3)
							{
								EditorGUILayout.LabelField($"  ... and {viewArray.Length - 3} more", EditorStyles.miniLabel);
							}

							EditorGUILayout.EndVertical();
							EditorGUILayout.Space(3);
						}
					}
				}

				EditorGUILayout.LabelField($"Total Pooled Views: {totalViews}", EditorStyles.miniLabel);
			}

			EditorGUILayout.EndVertical();
			EditorGUILayout.Space(5);
		}

		private void OnInspectorUpdate()
		{
			if (_autoRefresh && EditorApplication.isPlaying)
			{
				Repaint();
			}
		}

		#region Nested Types

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

		#endregion
	}
}

