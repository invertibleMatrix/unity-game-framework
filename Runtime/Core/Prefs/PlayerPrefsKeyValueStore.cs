using System;
using AK.Kernel.Persistence;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// <see cref="IKeyValueStore"/> over Unity's PlayerPrefs.
	///
	/// Writing to disk (<c>PlayerPrefs.Save</c>) is batched. In Play mode the writes of a frame
	/// are flushed once, at the end of that frame. Writes are also flushed when the app loses
	/// focus, which is what a mobile app gets before it is suspended and perhaps killed, when it
	/// quits, and on an explicit <see cref="Flush"/>. Outside Play mode every write is flushed
	/// at once, since there are no frames to wait for.
	///
	/// Main thread only, as PlayerPrefs is.
	/// </summary>
	public sealed class PlayerPrefsKeyValueStore : IKeyValueStore, IPlayerLoopItem, IDisposable
	{
		private bool _dirty;
		private bool _flushScheduled;
		private bool _disposed;

		public PlayerPrefsKeyValueStore()
		{
			Application.focusChanged += OnFocusChanged;
			Application.quitting     += Flush;
		}

		/// <summary>True while some write hasn't been flushed yet.</summary>
		public bool HasUnflushedWrites => _dirty;

		public bool TryGet(string key, out string value)
		{
			ThrowIfDisposed();

			if (!PlayerPrefs.HasKey(key))
			{
				value = null;
				return false;
			}

			value = PlayerPrefs.GetString(key);
			return true;
		}

		public bool Contains(string key)
		{
			ThrowIfDisposed();
			return PlayerPrefs.HasKey(key);
		}

		public void Set(string key, string value)
		{
			ThrowIfDisposed();

			PlayerPrefs.SetString(key, value);
			MarkDirty();
		}

		public bool Delete(string key)
		{
			ThrowIfDisposed();
			if (!PlayerPrefs.HasKey(key)) return false;

			PlayerPrefs.DeleteKey(key);
			MarkDirty();
			return true;
		}

		public void Flush()
		{
			if (!_dirty) return;

			_dirty = false;
			PlayerPrefs.Save();
		}

		/// <summary>
		/// Forgets a flush that was scheduled for a frame that will never come, as when the
		/// editor leaves Play mode first, so writes in the next Play session schedule their own.
		/// Flushes anything unflushed first.
		/// </summary>
		public void ResetFlushSchedule()
		{
			Flush();
			_flushScheduled = false;
		}

		/// <summary>Flushes, then stops following focus and quit. Further use throws.</summary>
		public void Dispose()
		{
			if (_disposed) return;

			Flush();
			_disposed = true;

			Application.focusChanged -= OnFocusChanged;
			Application.quitting     -= Flush;
		}

		private void MarkDirty()
		{
			_dirty = true;

			if (!Application.isPlaying)
			{
				Flush();
				return;
			}

			if (_flushScheduled) return;

			_flushScheduled = true;
			PlayerLoopHelper.AddAction(PlayerLoopTiming.LastPostLateUpdate, this);
		}

		// The end-of-frame flush. Returning false removes this item from the player loop.
		bool IPlayerLoopItem.MoveNext()
		{
			_flushScheduled = false;
			if (!_disposed) Flush();

			return false;
		}

		private void OnFocusChanged(bool hasFocus)
		{
			if (!hasFocus) Flush();
		}

		private void ThrowIfDisposed()
		{
			if (_disposed) throw new ObjectDisposedException(nameof(PlayerPrefsKeyValueStore));
		}
	}
}
