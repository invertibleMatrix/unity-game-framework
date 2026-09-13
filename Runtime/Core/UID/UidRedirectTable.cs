using System;
using System.Collections.Generic;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// Human-authored identity replacements. When content is deliberately replaced — an
	/// asset deleted and recreated — the old identity lives on in saves, servers, and
	/// other branches. An entry here says "old now means new", and every resolver consults
	/// it on a miss. Nothing is ever redirected automatically; a redirect is a decision,
	/// and this table is where the decision is recorded and reviewed.
	///
	/// Chains resolve transitively (a→b, b→c yields a→c) with a bounded hop count so a
	/// malformed cycle can never hang a lookup.
	/// </summary>
	[CreateAssetMenu(fileName = "UidRedirectTable", menuName = "AK/UID/Redirect Table")]
	public sealed class UidRedirectTable : ScriptableObject
	{
		private const int MaxHops = 8;

		[Serializable]
		public struct Entry
		{
			public Uid    From;
			public Uid    To;
			public string Reason;
			public string Date;
		}

		[SerializeField] private List<Entry> _entries = new();

		private Dictionary<Uid, Uid> _map;

		public IReadOnlyList<Entry> Entries => _entries;

		/// <summary>Follows redirects from <paramref name="id"/>. Returns the input when no entry applies.</summary>
		public Uid Follow(Uid id)
		{
			if (_map == null) Build();
			if (_map.Count == 0) return id;

			Uid current = id;
			for (int hop = 0; hop < MaxHops; hop++)
			{
				if (!_map.TryGetValue(current, out Uid next) || next == current) return current;
				current = next;
			}

			Debug.LogError($"[UidRedirectTable] Redirect chain from {id} exceeded {MaxHops} hops — possible cycle. Returning last reached identity.", this);
			return current;
		}

		public bool TryFollow(Uid id, out Uid target)
		{
			target = Follow(id);
			return target != id;
		}

		public bool Contains(Uid from)
		{
			if (_map == null) Build();
			return _map.ContainsKey(from);
		}

		private void Build()
		{
			_map = new Dictionary<Uid, Uid>(_entries.Count);
			foreach (Entry e in _entries)
			{
				if (e.From.IsNone || e.To.IsNone || e.From == e.To) continue;
				_map[e.From] = e.To;
			}
		}

		private void OnEnable()  => _map = null;
		private void OnValidate() => _map = null;

#if UNITY_EDITOR
		internal void Editor_Add(Uid from, Uid to, string reason)
		{
			_entries.RemoveAll(e => e.From == from);
			_entries.Add(new Entry { From = from, To = to, Reason = reason ?? string.Empty, Date = DateTime.UtcNow.ToString("yyyy-MM-dd") });
			_map = null;
		}

		internal bool Editor_Remove(Uid from)
		{
			int removed = _entries.RemoveAll(e => e.From == from);
			_map = null;
			return removed > 0;
		}
#endif
	}
}
