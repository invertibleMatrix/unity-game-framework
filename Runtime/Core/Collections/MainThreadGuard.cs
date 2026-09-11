using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace AK.Core.Collections
{
	/// <summary>
	/// Editor and development-build assertion that a main-thread-only collection is used from the
	/// main thread. The static pools here are not thread-safe; a worker renting from one corrupts it
	/// silently and the failure shows up later on the main thread, so the check has to be at the call.
	/// Compiles to nothing in release players.
	/// </summary>
	internal static class MainThreadGuard
	{
		private static int _mainThreadId;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void CaptureAtRuntime() => _mainThreadId = Thread.CurrentThread.ManagedThreadId;

#if UNITY_EDITOR
		[UnityEditor.InitializeOnLoadMethod]
		private static void CaptureInEditor() => _mainThreadId = Thread.CurrentThread.ManagedThreadId;
#endif

		[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
		public static void Assert(string what)
		{
			if (_mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId != _mainThreadId)
			{
				UnityEngine.Debug.LogError($"{what} is main-thread only; called from '{Thread.CurrentThread.Name ?? "unnamed thread"}'.");
			}
		}
	}
}
