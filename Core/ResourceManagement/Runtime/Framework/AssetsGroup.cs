using System;
using System.Collections.Generic;

namespace AK.Core.ResourceManagement
{
	/// <summary>
	/// <see cref="AssetsGroup{T}"/> Is Proxy To Track The List Of Assets Loaded By <see cref="UniResources"/>
	/// </summary>
	/// <typeparam name="T"></typeparam>
	[System.Serializable]
	public sealed class AssetsGroup<T> : List<T>
	{
		public static readonly AssetsGroup<T> Default = new(ArraySegment<T>.Empty);
		
		public readonly Guid Guid;
		internal AssetsGroup(IEnumerable<T> data) : base(data) => Guid = Guid.NewGuid();

		public void DisposeAssets() => this.Clear();
	}
}