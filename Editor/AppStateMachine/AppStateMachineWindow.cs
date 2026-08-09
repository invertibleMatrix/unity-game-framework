using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// Live AppStateMachine inspector: current/previous state, pause stack, and — when
	/// Record History is enabled — a capped transition log (from → to, pause/resume
	/// markers, timestamps) captured via the machine's OnTransition hook. Recording
	/// happens only while the window is open and the toggle is on; nothing is recorded
	/// otherwise, and nothing touches play-mode performance beyond a repaint.
	/// </summary>
	public class AppStateMachineWindow : EditorWindow
	{
		private const string DebugModePrefsKey = "UGFW.AppStateMachine.RecordHistory";
		private const int    MaxHistoryEntries = 200;

		private class HistoryEntry
		{
			public float    Time;
			public AppState From;
			public AppState To;
			public bool     PreviousPaused;
			public bool     Resumed;
			public bool     IsSnapshot;
		}

		private AppStateMachine _machine;
		private bool            _debugMode;
		private float           _lastTransitionTime;
		private readonly List<HistoryEntry> _history = new();
		private Vector2 _scroll;
		private bool    _followHistory = true;

		[MenuItem("Tools/UGFW/App State Machine")]
		private static void Open()
		{
			GetWindow<AppStateMachineWindow>("App State Machine");
		}

		private void OnEnable()
		{
			_debugMode = EditorPrefs.GetBool(DebugModePrefsKey, false);
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
			TryHookMachine();
		}

		private void OnDisable()
		{
			EditorApplication.playModeStateChanged -= OnPlayModeChanged;
			Unhook();
		}

		private void OnPlayModeChanged(PlayModeStateChange change)
		{
			if (change == PlayModeStateChange.EnteredPlayMode)
			{
				TryHookMachine();
			}
			else if (change == PlayModeStateChange.EnteredEditMode)
			{
				Unhook();
				_machine = null;
				_history.Clear();
			}

			Repaint();
		}

		private void TryHookMachine()
		{
			Unhook();

			if (!Application.isPlaying) return;

			_machine = FindFirstObjectByType<AppStateMachine>(FindObjectsInactive.Include);
			if (_machine == null) return;

			_machine.OnTransition += HandleTransition;
			_lastTransitionTime = Time.realtimeSinceStartup;

			// The boot transition predates this subscription — seed a snapshot so the
			// history never starts blank when recording is on.
			if (_debugMode && _machine.CurrentState != null && _history.Count == 0)
			{
				_history.Add(new HistoryEntry
				{
					Time = _lastTransitionTime,
					To = _machine.CurrentState,
					IsSnapshot = true
				});
			}
		}

		private void Unhook()
		{
			if (_machine != null)
			{
				_machine.OnTransition -= HandleTransition;
			}
		}

		private void HandleTransition(StateTransitionInfo info)
		{
			_lastTransitionTime = Time.realtimeSinceStartup;

			if (_debugMode)
			{
				_history.Add(new HistoryEntry
				{
					Time = _lastTransitionTime,
					From = info.From,
					To = info.To,
					PreviousPaused = info.PreviousPaused,
					Resumed = info.Resumed
				});

				if (_history.Count > MaxHistoryEntries)
				{
					_history.RemoveAt(0);
				}
			}

			Repaint();
		}

		private void OnInspectorUpdate()
		{
			Repaint();
		}

		private void OnGUI()
		{
			if (!Application.isPlaying)
			{
				EditorGUILayout.HelpBox("Enter Play Mode to inspect the AppStateMachine.", MessageType.Info);
				return;
			}

			if (_machine == null)
			{
				EditorGUILayout.HelpBox("No AppStateMachine found in the active scene.", MessageType.Warning);
				if (GUILayout.Button("Retry"))
				{
					TryHookMachine();
				}
				return;
			}

			DrawCurrentState();
			EditorGUILayout.Space();
			DrawHistoryControls();

			if (_debugMode)
			{
				DrawHistory();
			}
			else
			{
				EditorGUILayout.HelpBox("History recording is off. Enable Record History to capture transitions while this window is open.", MessageType.None);
			}
		}

		private void DrawCurrentState()
		{
			EditorGUILayout.BeginVertical("box");

			EditorGUILayout.LabelField("Current State", EditorStyles.boldLabel);
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(_machine.CurrentState != null ? _machine.CurrentState.name : "—", EditorStyles.largeLabel);
			EditorGUILayout.LabelField(FormatTime(Time.realtimeSinceStartup - _lastTransitionTime), EditorStyles.miniLabel, GUILayout.Width(60));
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Previous:", GUILayout.Width(70));
			DrawStateLink(_machine.PreviousState);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Paused stack:", GUILayout.Width(70));
			if (_machine.PausedStates.Count == 0)
			{
				EditorGUILayout.LabelField("—", EditorStyles.miniLabel);
			}
			else
			{
				foreach (var paused in _machine.PausedStates)
				{
					DrawStateLink(paused);
				}
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.EndVertical();
		}

		private void DrawStateLink(AppState state)
		{
			if (state == null)
			{
				EditorGUILayout.LabelField("—", EditorStyles.miniLabel);
				return;
			}

			if (GUILayout.Button(state.name, EditorStyles.linkLabel))
			{
				Selection.activeObject = state;
				EditorGUIUtility.PingObject(state);
			}
		}

		private void DrawHistoryControls()
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

			bool record = GUILayout.Toggle(_debugMode, " Record History", EditorStyles.toolbarButton);
			if (record != _debugMode)
			{
				_debugMode = record;
				EditorPrefs.SetBool(DebugModePrefsKey, _debugMode);

				if (_debugMode)
				{
					TryHookMachine(); // re-seed with a snapshot of where we joined
				}
			}

			_followHistory = GUILayout.Toggle(_followHistory, " Follow Latest", EditorStyles.toolbarButton);

			GUILayout.FlexibleSpace();

			if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
			{
				_history.Clear();
			}

			EditorGUILayout.EndHorizontal();
		}

		private void DrawHistory()
		{
			EditorGUILayout.BeginVertical("box");
			EditorGUILayout.LabelField($"History ({_history.Count})", EditorStyles.boldLabel);

			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			foreach (var entry in _history)
			{
				EditorGUILayout.BeginHorizontal();

				EditorGUILayout.LabelField(FormatTime(entry.Time), EditorStyles.miniLabel, GUILayout.Width(60));

				if (entry.IsSnapshot)
				{
					EditorGUILayout.LabelField($"(joined at {entry.To.name})", EditorStyles.centeredGreyMiniLabel);
				}
				else
				{
					DrawStateLink(entry.From);
					EditorGUILayout.LabelField("→", GUILayout.Width(16));
					DrawStateLink(entry.To);

					if (entry.Resumed)
					{
						EditorGUILayout.LabelField("[resumed]", EditorStyles.miniLabel, GUILayout.Width(60));
					}
					if (entry.PreviousPaused)
					{
						EditorGUILayout.LabelField("[paused prev]", EditorStyles.miniLabel, GUILayout.Width(80));
					}
				}

				EditorGUILayout.EndHorizontal();
			}

			if (_followHistory && Event.current.type == EventType.Layout)
			{
				_scroll.y = float.MaxValue;
			}

			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
		}

		private static string FormatTime(float seconds)
		{
			int totalSeconds = (int)seconds;
			return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
		}
	}
}
