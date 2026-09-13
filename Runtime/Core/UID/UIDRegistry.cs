using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// The one place an identity becomes an asset again. Holds a dense array of tracked
	/// objects and a Uid→slot map; every lookup is one hash probe or, through a
	/// <see cref="UidHandle{T}"/>, one array index. Misses are answered with false, never
	/// with a log or a substitute — the caller knows what a missing definition means here.
	///
	/// Lookups build lazily on first access and rebuild on every mutation, bumping
	/// <see cref="Version"/> so outstanding handles can be validated cheaply.
	/// </summary>
	[Serializable]
	public class UidRegistry<T> where T : UID
	{
		[SerializeField] private List<T> _objects = new();

		[NonSerialized] private Dictionary<Uid, int> _slots;
		[NonSerialized] private T[]                  _dense;
		[NonSerialized] private int                  _version;
		[NonSerialized] private UidRedirectTable     _redirects;

		public IReadOnlyList<T> Objects => _objects;
		public int              Count   => _objects.Count;

		/// <summary>Increments on every rebuild. Zero means lookups have not been built yet.</summary>
		public int Version => _version;

		/// <summary>Optional redirect table consulted on a miss. Set once by the owning repository.</summary>
		public void SetRedirects(UidRedirectTable redirects)
		{
			_redirects = redirects;
		}

		// ---------------------------------------------------------------- resolve

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryResolve(Uid<T> id, out T obj) => TryResolve(id.Value, out obj);

		public bool TryResolve(Uid id, out T obj)
		{
			if (_slots == null) Rebuild();

			if (id.IsSet)
			{
				if (_slots.TryGetValue(id, out int slot))
				{
					obj = _dense[slot];
					return true;
				}

				if (_redirects != null && _redirects.TryFollow(id, out Uid target) && _slots.TryGetValue(target, out slot))
				{
					obj = _dense[slot];
					return true;
				}
			}

			obj = null;
			return false;
		}

		/// <summary>Resolve-or-null. Prefer TryResolve where a miss has meaning at the call site.</summary>
		public T Resolve(Uid<T> id) => TryResolve(id.Value, out T obj) ? obj : null;
		public T Resolve(Uid id)    => TryResolve(id, out T obj) ? obj : null;

		public bool Contains(Uid id)
		{
			if (_slots == null) Rebuild();
			return id.IsSet && _slots.ContainsKey(id);
		}

		// ---------------------------------------------------------------- handles

		/// <summary>Resolves to a dense handle. Invalid when the identity is unknown.</summary>
		public UidHandle<T> GetHandle(Uid<T> id)
		{
			if (_slots == null) Rebuild();

			Uid key = id.Value;
			if (key.IsNone) return UidHandle<T>.Invalid;

			if (!_slots.TryGetValue(key, out int slot))
			{
				if (_redirects == null || !_redirects.TryFollow(key, out Uid target) || !_slots.TryGetValue(target, out slot))
				{
					return UidHandle<T>.Invalid;
				}

				key = target;
			}

			return new UidHandle<T>(new Uid<T>(key), slot, _version);
		}

		/// <summary>
		/// One bounds check and one load when the handle is current. A handle from a previous
		/// version is re-resolved through the map and the refreshed handle is returned by ref.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGet(ref UidHandle<T> handle, out T obj)
		{
			if (_slots == null) Rebuild();

			if (handle.Version == _version && (uint)handle.Slot < (uint)_dense.Length)
			{
				obj = _dense[handle.Slot];
				return true;
			}

			handle = GetHandle(handle.Id);
			if (handle.IsValid)
			{
				obj = _dense[handle.Slot];
				return true;
			}

			obj = null;
			return false;
		}

		// ---------------------------------------------------------------- mutation

		public bool Add(T obj)
		{
			if (obj == null || _objects.Contains(obj)) return false;

			_objects.Add(obj);
			Rebuild();
			return true;
		}

		public bool Remove(T obj)
		{
			if (obj == null || !_objects.Remove(obj)) return false;

			Rebuild();
			return true;
		}

		public int RemoveNullEntries()
		{
			int removed = _objects.RemoveAll(o => o == null);
			if (removed > 0) Rebuild();
			return removed;
		}

		public void ReplaceAll(IEnumerable<T> objects)
		{
			_objects.Clear();
			foreach (T o in objects)
			{
				if (o != null && !_objects.Contains(o)) _objects.Add(o);
			}

			Rebuild();
		}

		/// <summary>Forces the lookups to rebuild on next access. Cheap; call after external list edits.</summary>
		public void Invalidate()
		{
			_slots = null;
		}

		// ---------------------------------------------------------------- build

		private void Rebuild()
		{
			int n = _objects.Count;
			var slots = new Dictionary<Uid, int>(n);
			var dense = new T[n];
			int written = 0;

			for (int i = 0; i < n; i++)
			{
				T obj = _objects[i];
				if (obj == null) continue;

				Uid id = obj.Id;
				if (id.IsNone)
				{
					Diagnostics.SkippedNoIdentity(typeof(T), obj);
					continue;
				}

				if (slots.TryGetValue(id, out int existing))
				{
					Diagnostics.SkippedDuplicate(typeof(T), obj, dense[existing], id);
					continue;
				}

				slots.Add(id, written);
				dense[written++] = obj;
			}

			if (written != n)
			{
				Array.Resize(ref dense, written);
			}

			_slots = slots;
			_dense = dense;
			_version++;
		}

		private static class Diagnostics
		{
			[System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
			public static void SkippedNoIdentity(Type kind, UID obj)
			{
				Debug.LogError($"[{kind.Name} registry] '{obj.name}' has no identity and was skipped.", obj);
			}

			[System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
			public static void SkippedDuplicate(Type kind, UID obj, UID first, Uid id)
			{
				Debug.LogError($"[{kind.Name} registry] '{obj.name}' shares identity {id.ToShortString()} with '{first.name}' and was skipped. Resolve in Tools → UGFW → UID → Audit.", obj);
			}
		}
	}
}
