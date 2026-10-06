#if UGFW_ADDRESSABLES
using System;
using System.Collections;
using System.Collections.Generic;

namespace AK.Core.ResourceManagement
{
	/// <summary>
	/// The assets one group load by <see cref="UniResources"/> returned, read-only. Disposing the
	/// group through the loading strategy releases them and empties it.
	/// </summary>
	public sealed class AssetsGroup<T> : IReadOnlyList<T>
	{
		/// <summary>An empty group that holds no load. Disposing it does nothing.</summary>
		public static readonly AssetsGroup<T> Default = new(Array.Empty<T>());

		private readonly List<T> _assets;

		/// <summary>Identifies the load behind the group.</summary>
		public readonly Guid Guid;

		internal AssetsGroup(IEnumerable<T> assets)
		{
			_assets = new List<T>(assets);
			Guid    = Guid.NewGuid();
		}

		public int Count => _assets.Count;

		public T this[int index] => _assets[index];

		public List<T>.Enumerator GetEnumerator() => _assets.GetEnumerator();

		IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		/// <summary>Empties the group once its load is released.</summary>
		internal void DisposeAssets() => _assets.Clear();
	}
}
#endif
