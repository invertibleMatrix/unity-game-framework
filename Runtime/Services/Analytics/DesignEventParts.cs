using System;
using System.Collections;
using System.Collections.Generic;

namespace AK.Services.Analytics
{
	/// <summary>
	/// The parts of a design event id, split on ':' with empty parts dropped: "level:start:w2"
	/// is level, start and w2. Prefix checks ignore case. Immutable.
	/// </summary>
	public sealed class DesignEventParts : IReadOnlyList<string>
	{
		public const char Separator = ':';

		/// <summary>An id with no parts.</summary>
		public static readonly DesignEventParts Empty = new(Array.Empty<string>());

		private readonly string[] _parts;

		private DesignEventParts(string[] parts)
		{
			_parts = parts;
		}

		/// <summary>The parts of <paramref name="id"/>; <see cref="Empty"/> for null or "".</summary>
		public static DesignEventParts Parse(string id)
		{
			int count = CountParts(id);
			if (count == 0)
			{
				return Empty;
			}

			var parts = new string[count];
			int next = 0;
			int start = 0;
			for (int i = 0; i <= id.Length; i++)
			{
				if (i < id.Length && id[i] != Separator)
				{
					continue;
				}

				if (i > start)
				{
					parts[next++] = id.Substring(start, i - start);
				}

				start = i + 1;
			}

			return new DesignEventParts(parts);
		}

		public int Count => _parts.Length;

		public string this[int index] => _parts[index];

		/// <summary>The part at <paramref name="index"/>, or null past either end.</summary>
		public string At(int index) => (uint)index < (uint)_parts.Length ? _parts[index] : null;

		public string Join(string separator) => string.Join(separator, _parts);

		public bool StartsWith(string part0) =>
			_parts.Length >= 1 && Matches(0, part0);

		public bool StartsWith(string part0, string part1) =>
			_parts.Length >= 2 && Matches(0, part0) && Matches(1, part1);

		public bool StartsWith(string part0, string part1, string part2) =>
			_parts.Length >= 3 && Matches(0, part0) && Matches(1, part1) && Matches(2, part2);

		/// <summary>True when the first parts are <paramref name="prefix"/>'s, ignoring case.</summary>
		/// <exception cref="ArgumentNullException"><paramref name="prefix"/> is null.</exception>
		public bool StartsWith(DesignEventParts prefix)
		{
			if (prefix == null)
			{
				throw new ArgumentNullException(nameof(prefix));
			}

			if (_parts.Length < prefix._parts.Length)
			{
				return false;
			}

			for (int i = 0; i < prefix._parts.Length; i++)
			{
				if (!Matches(i, prefix._parts[i]))
				{
					return false;
				}
			}

			return true;
		}

		public override string ToString() => Join(":");

		public Enumerator GetEnumerator() => new(_parts);

		IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		private bool Matches(int index, string part) =>
			string.Equals(_parts[index], part, StringComparison.OrdinalIgnoreCase);

		private static int CountParts(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return 0;
			}

			int count = 0;
			int start = 0;
			for (int i = 0; i <= id.Length; i++)
			{
				if (i < id.Length && id[i] != Separator)
				{
					continue;
				}

				if (i > start)
				{
					count++;
				}

				start = i + 1;
			}

			return count;
		}

		/// <summary>Walks the parts without allocating.</summary>
		public struct Enumerator : IEnumerator<string>
		{
			private readonly string[] _parts;
			private int _index;

			internal Enumerator(string[] parts)
			{
				_parts = parts;
				_index = -1;
			}

			public string Current => _parts[_index];

			object IEnumerator.Current => Current;

			public bool MoveNext() => ++_index < _parts.Length;

			public void Reset() => _index = -1;

			public void Dispose()
			{
			}
		}
	}
}
