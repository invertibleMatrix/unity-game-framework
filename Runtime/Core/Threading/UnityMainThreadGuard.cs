using AK.Kernel.Threading;
using UnityEngine;

namespace AK.Core.Threading
{
	/// <summary>
	/// Names Unity's main thread to <see cref="MainThreadGuard"/>, in the editor and in players,
	/// and reports calls from other threads as errors in the console.
	/// </summary>
	internal static class UnityMainThreadGuard
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void SetAtRuntime() => MainThreadGuard.SetMainThread(Report);

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
		private static void SetInEditor() => MainThreadGuard.SetMainThread(Report);
#endif

		private static void Report(string message) => Debug.LogError(message);
	}
}
