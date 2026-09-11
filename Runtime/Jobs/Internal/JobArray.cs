using System;
using System.Runtime.CompilerServices;

namespace AK.Jobs
{
	/// <summary>
	/// Growable array with an explicit count, owned by exactly one thread at a time. Used instead of
	/// <c>List&lt;T&gt;</c> so the worker loop reads a plain array and so two instances can be swapped
	/// by reference at the barrier without copying.
	/// </summary>
	internal sealed class JobArray<T>
	{
		public T[] Items;
		public int Count;

		public JobArray(int capacity)
		{
			Items = new T[Math.Max(capacity, 4)];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(in T item)
		{
			if (Count == Items.Length) Array.Resize(ref Items, Items.Length * 2);
			Items[Count++] = item;
		}

		/// <summary>Removes one entry and keeps the order of the rest.</summary>
		public void RemoveAtOrdered(int index)
		{
			int tail = Count - index - 1;
			if (tail > 0) Array.Copy(Items, index + 1, Items, index, tail);
			Count--;
			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) Items[Count] = default;
		}

		public void Clear()
		{
			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) Array.Clear(Items, 0, Count);
			Count = 0;
		}

		/// <summary>Drops entries past <paramref name="count"/>, releasing their references.</summary>
		public void Truncate(int count)
		{
			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() && count < Count) Array.Clear(Items, count, Count - count);
			Count = count;
		}
	}
}
