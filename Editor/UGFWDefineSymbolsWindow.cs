using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AK.Editor
{
    public static class UGFWDefineSymbols
    {
        public readonly struct DefineEntry
        {
            public readonly string Symbol;
            public readonly string Label;
            public readonly string Description;
            public readonly string Group;

            public DefineEntry(string symbol, string label, string description, string group)
            {
                Symbol = symbol;
                Label = label;
                Description = description;
                Group = group;
            }
        }

        public static readonly DefineEntry[] Defines =
        {
            new("ADMOB_ENABLED", "AdMob Ads", "Google Mobile Ads SDK (requires AdMob package)", "Ads"),
            new("FIREBASE_INITIALIZATION", "Firebase Core", "Firebase Core SDK — initialization and dependency checking", "Firebase"),
            new("FIREBASE_ANALYTICS", "Firebase Analytics", "Firebase Analytics SDK (requires Firebase Analytics package)", "Firebase"),
            new("FIREBASE_REMOTE_CONFIG", "Firebase Remote Config", "Firebase Remote Config SDK (requires Firebase Remote Config package)", "Firebase"),
            new("GAME_ANALYTICS", "GameAnalytics", "GameAnalytics SDK (requires GameAnalytics package)", "Analytics"),
            new("IAP", "Unity IAP", "Unity In-App Purchasing (requires Unity Purchasing package)", "Monetization"),
        };

        public static HashSet<string> GetActiveSymbols()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var group = BuildPipeline.GetBuildTargetGroup(target);
            var namedTarget = BuildTargetGroupToNamedBuildTarget(group);
            PlayerSettings.GetScriptingDefineSymbols(namedTarget, out var symbols);
            return new HashSet<string>(symbols);
        }

        public static void SetSymbolEnabled(string symbol, bool enabled)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var group = BuildPipeline.GetBuildTargetGroup(target);
            var namedTarget = BuildTargetGroupToNamedBuildTarget(group);
            PlayerSettings.GetScriptingDefineSymbols(namedTarget, out var symbols);

            var set = new HashSet<string>(symbols);

            if (enabled)
            {
                set.Add(symbol);
            }
            else
            {
                set.Remove(symbol);
            }

            PlayerSettings.SetScriptingDefineSymbols(namedTarget, set.ToArray());
        }

        public static bool IsSymbolEnabled(string symbol)
        {
            return GetActiveSymbols().Contains(symbol);
        }

        private static NamedBuildTarget BuildTargetGroupToNamedBuildTarget(BuildTargetGroup group)
        {
            return group == BuildTargetGroup.Standalone
                ? NamedBuildTarget.Standalone
                : NamedBuildTarget.FromBuildTargetGroup(group);
        }
    }

    public class UGFWDefineSymbolsWindow : EditorWindow
    {
        private Vector2 _scrollPos;

        [MenuItem("Tools/UGFW/Define Symbols")]
        private static void ShowWindow()
        {
            var window = GetWindow<UGFWDefineSymbolsWindow>();
            window.titleContent = new GUIContent("UGFW Define Symbols");
            window.minSize = new Vector2(350, 200);
        }

        private void OnGUI()
        {
            var activeSymbols = UGFWDefineSymbols.GetActiveSymbols();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("UGFW Service Toggles", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Toggle scripting define symbols for UGFW services. " +
                "Make sure you have installed the corresponding SDK/package before enabling a symbol.",
                MessageType.Info);
            EditorGUILayout.Space(4);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            string currentGroup = null;
            foreach (var entry in UGFWDefineSymbols.Defines)
            {
                if (entry.Group != currentGroup)
                {
                    if (currentGroup != null) EditorGUILayout.Space(8);
                    currentGroup = entry.Group;
                    EditorGUILayout.LabelField(currentGroup, EditorStyles.miniBoldLabel);
                }

                var wasEnabled = activeSymbols.Contains(entry.Symbol);
                var isEnabled = EditorGUILayout.ToggleLeft(
                    new GUIContent(entry.Label, entry.Description),
                    wasEnabled);

                if (isEnabled != wasEnabled)
                {
                    UGFWDefineSymbols.SetSymbolEnabled(entry.Symbol, isEnabled);
                    // Refresh the active symbols after change
                    activeSymbols = UGFWDefineSymbols.GetActiveSymbols();
                }

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(entry.Description, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(12);

            EditorGUILayout.LabelField("Current Defines", EditorStyles.miniBoldLabel);
            var allSymbols = UGFWDefineSymbols.GetActiveSymbols();
            if (allSymbols.Count > 0)
            {
                EditorGUILayout.LabelField(string.Join("; ", allSymbols.OrderBy(s => s)),
                    EditorStyles.wordWrappedMiniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("(none)", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
