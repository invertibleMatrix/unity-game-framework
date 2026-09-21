using System.Collections.Generic;

namespace AK.Systems
{
	/// <summary>
	/// Every view the system currently owns, plus an index by kind. Each kind bucket is an
	/// intrusive singly linked list threaded through <see cref="ViewRecord.NextOfKind"/>,
	/// so "the static instance of X under P" or "is a dynamic X already open on P" walks
	/// only the instances of that one kind instead of the whole registry.
	/// Registration also links the record into its parent's <see cref="ViewRecord.Children"/>;
	/// removal unlinks it.
	/// </summary>
	internal sealed class ViewRegistry
	{
		private readonly Dictionary<UIView, ViewRecord>  _records = new();
		private readonly Dictionary<ViewKey, ViewRecord> _byKind  = new();

		public int Count => _records.Count;

		public Dictionary<UIView, ViewRecord>.ValueCollection Records => _records.Values;

		public bool Contains(UIView view) => _records.ContainsKey(view);

		public bool TryGet(UIView view, out ViewRecord record) => _records.TryGetValue(view, out record);

		/// <summary>Registered and its GameObject still alive.</summary>
		public bool IsRegistered(UIView view) => view != null && view.gameObject != null && _records.ContainsKey(view);

		public bool IsClosing(UIView view) => _records.TryGetValue(view, out var record) && record.IsClosing;

		public void Add(ViewRecord record)
		{
			_records.Add(record.Instance, record);

			if (_byKind.TryGetValue(record.Kind, out var head))
			{
				record.NextOfKind = head;
			}

			_byKind[record.Kind] = record;

			if (record.Parent != null && _records.TryGetValue(record.Parent, out var parentRecord))
			{
				parentRecord.AddChild(record.Instance);
			}
		}

		/// <summary>Drops the record and unlinks it from its parent and kind bucket. Null when not registered.</summary>
		public ViewRecord Remove(UIView view)
		{
			if (!_records.TryGetValue(view, out var record)) return null;

			_records.Remove(view);

			if (record.Parent != null && _records.TryGetValue(record.Parent, out var parentRecord))
			{
				parentRecord.RemoveChild(view);
			}

			UnlinkKind(record);
			return record;
		}

		public ViewRecord FirstOfKind(ViewKey key)
		{
			return _byKind.TryGetValue(key, out var head) ? head : null;
		}

		/// <summary>
		/// Static instance of a kind that is not tearing down. With a parent, only one
		/// registered under that parent; without, any static of the kind (it re-routes onto
		/// its own host). <paramref name="includeClosing"/> also matches one mid-close —
		/// for a show that wants to queue behind the close instead of spawning a twin.
		/// </summary>
		public ViewRecord FindStatic(ViewKey key, UIView parent, bool includeClosing = false)
		{
			for (var record = FirstOfKind(key); record != null; record = record.NextOfKind)
			{
				if (!record.IsStatic) continue;
				if (parent != null && record.Parent != parent) continue;
				if (record.IsAlive && (!record.IsClosing || includeClosing)) return record;
			}

			return null;
		}

		/// <summary>Dynamic instance of a kind already open under <paramref name="parent"/>, not tearing down.</summary>
		public ViewRecord FindDynamic(ViewKey key, UIView parent)
		{
			for (var record = FirstOfKind(key); record != null; record = record.NextOfKind)
			{
				if (record.IsStatic || record.Parent != parent) continue;
				if (record.IsAlive && !record.IsClosing) return record;
			}

			return null;
		}

		/// <summary>Registered views of a kind, closing or not.</summary>
		public int CountOfKind(ViewKey key)
		{
			int count = 0;
			for (var record = FirstOfKind(key); record != null; record = record.NextOfKind)
			{
				count++;
			}

			return count;
		}

		private void UnlinkKind(ViewRecord record)
		{
			if (!_byKind.TryGetValue(record.Kind, out var head)) return;

			if (ReferenceEquals(head, record))
			{
				if (record.NextOfKind == null) _byKind.Remove(record.Kind);
				else _byKind[record.Kind] = record.NextOfKind;
			}
			else
			{
				for (var current = head; current.NextOfKind != null; current = current.NextOfKind)
				{
					if (ReferenceEquals(current.NextOfKind, record))
					{
						current.NextOfKind = record.NextOfKind;
						break;
					}
				}
			}

			record.NextOfKind = null;
		}
	}
}
