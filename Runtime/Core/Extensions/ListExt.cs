using System;
using System.Collections.Generic;
using System.Linq;

namespace AK.Core.Extensions
{
	public static class ListExt
	{
		/// <summary>
		/// Removes Last Element from provided List
		/// </summary>
		public static T Pop<T>(this IList<T> list)
		{
			if (list.Any<T>() == false) throw new InvalidOperationException("Attempting to pop item on empty list.");

			var idx = list.Count - 1;
			var item = list[idx];
			list.RemoveAt(idx);

			return item;
		}

		/// <summary>
		/// Moves provided Element to the end of the list
		/// </summary>
		public static void MoveToEnd<T>(this IList<T> list, T item)
		{
			if (list.Contains(item))
			{
				// Remove the item from its current position
				list.Remove(item);
			}

			// Add the item to the end of the list
			list.Add(item);
		}
	}
}
