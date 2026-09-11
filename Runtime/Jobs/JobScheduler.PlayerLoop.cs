using System;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace AK.Jobs
{
	public sealed partial class JobScheduler
	{
		/// <summary>Marker type of the player-loop system this scheduler inserts; one system per attached scheduler.</summary>
		public struct PlayerLoopTick { }

		private bool _attached;

		public bool IsAttachedToPlayerLoop => _attached;

		/// <summary>
		/// Ticks the barrier at the start of <c>EarlyUpdate</c> every frame — before any script's
		/// <c>Update</c> — using the engine's frame timing. Detached automatically by <see cref="Dispose"/>,
		/// on <c>Application.quitting</c>, and when the editor leaves play mode.
		/// </summary>
		public void AttachToPlayerLoop()
		{
			ThrowIfNotMainThread();
			ThrowIfDisposed();
			if (_attached) return;

			PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
			if (!InsertIntoEarlyUpdate(ref loop, new PlayerLoopSystem { type = typeof(PlayerLoopTick), updateDelegate = TickFromPlayerLoop }))
			{
				throw new InvalidOperationException("the current player loop has no EarlyUpdate phase to attach to");
			}

			PlayerLoop.SetPlayerLoop(loop);
			_attached = true;

			Application.quitting += OnApplicationQuitting;
#if UNITY_EDITOR
			UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
		}

		public void DetachFromPlayerLoop()
		{
			if (!_attached) return;
			ThrowIfNotMainThread();

			PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
			if (RemoveFromEarlyUpdate(ref loop, this)) PlayerLoop.SetPlayerLoop(loop);
			_attached = false;

			Application.quitting -= OnApplicationQuitting;
#if UNITY_EDITOR
			UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
		}

		private void TickFromPlayerLoop()
		{
			if (_disposed) return;
			Tick(new FrameContext(Time.frameCount, Time.time, Time.unscaledTime, Time.deltaTime));
		}

		private void OnApplicationQuitting() => Dispose();

#if UNITY_EDITOR
		private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
		{
			if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode) Dispose();
		}
#endif

		private static bool InsertIntoEarlyUpdate(ref PlayerLoopSystem root, PlayerLoopSystem system)
		{
			PlayerLoopSystem[] phases = root.subSystemList;
			if (phases == null) return false;

			for (int i = 0; i < phases.Length; i++)
			{
				if (phases[i].type != typeof(EarlyUpdate)) continue;

				PlayerLoopSystem[] old  = phases[i].subSystemList ?? Array.Empty<PlayerLoopSystem>();
				var                list = new PlayerLoopSystem[old.Length + 1];
				list[0] = system;
				Array.Copy(old, 0, list, 1, old.Length);

				phases[i].subSystemList = list;
				return true;
			}

			return false;
		}

		private static bool RemoveFromEarlyUpdate(ref PlayerLoopSystem root, JobScheduler owner)
		{
			PlayerLoopSystem[] phases = root.subSystemList;
			if (phases == null) return false;

			for (int i = 0; i < phases.Length; i++)
			{
				if (phases[i].type != typeof(EarlyUpdate) || phases[i].subSystemList == null) continue;

				PlayerLoopSystem[] old   = phases[i].subSystemList;
				int                keep  = 0;
				var                list  = new PlayerLoopSystem[old.Length];

				for (int j = 0; j < old.Length; j++)
				{
					if (old[j].type == typeof(PlayerLoopTick) && ReferenceEquals(old[j].updateDelegate?.Target, owner)) continue;
					list[keep++] = old[j];
				}

				if (keep == old.Length) return false;

				Array.Resize(ref list, keep);
				phases[i].subSystemList = list;
				return true;
			}

			return false;
		}
	}
}
