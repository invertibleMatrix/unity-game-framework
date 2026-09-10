using System.Collections.Generic;

namespace AK.Systems
{
	/// <summary>
	/// Navigation stack for screens and fragments. Bottom is index 0, top is
	/// <c>Count - 1</c>. Unlike <see cref="Stack{T}"/> it supports the operations the UI
	/// system actually performs — remove from the middle, look below a view, scan the views
	/// above one — without rebuilding the stack or copying it to an array.
	/// </summary>
	public sealed class ViewStack
	{
		private readonly List<UIView> _items = new(8);

		public int Count => _items.Count;

		/// <summary>Bottom-to-top indexed access.</summary>
		public UIView this[int index] => _items[index];

		public UIView Peek() => _items[_items.Count - 1];

		public UIView PeekOrNull() => _items.Count > 0 ? _items[_items.Count - 1] : null;

		/// <summary>The view directly under the top, or null when fewer than two views.</summary>
		public UIView PeekBelowTopOrNull() => _items.Count > 1 ? _items[_items.Count - 2] : null;

		public void Push(UIView view) => _items.Add(view);

		public UIView Pop()
		{
			int last = _items.Count - 1;
			UIView top = _items[last];
			_items.RemoveAt(last);
			return top;
		}

		/// <summary>Removes the top-most occurrence. Returns false when the view is not present.</summary>
		public bool Remove(UIView view)
		{
			int index = IndexOf(view);
			if (index < 0) return false;
			_items.RemoveAt(index);
			return true;
		}

		/// <summary>Removes a view if present, then pushes it on top — "bring to front".</summary>
		public void MoveToTop(UIView view)
		{
			Remove(view);
			_items.Add(view);
		}

		public bool Contains(UIView view) => IndexOf(view) >= 0;

		/// <summary>Index of the top-most occurrence, or -1. Reference equality only.</summary>
		public int IndexOf(UIView view)
		{
			for (int i = _items.Count - 1; i >= 0; i--)
			{
				if (ReferenceEquals(_items[i], view)) return i;
			}

			return -1;
		}

		/// <summary>The view directly beneath <paramref name="view"/>, or null at the bottom / not found.</summary>
		public UIView BelowOrNull(UIView view)
		{
			int index = IndexOf(view);
			return index > 0 ? _items[index - 1] : null;
		}

		/// <summary>
		/// True when any view above <paramref name="view"/> hides or blocks what is beneath it.
		/// Used after a mid-stack removal to decide whether the view below must be resumed.
		/// </summary>
		public bool IsCoveredAbove(UIView view)
		{
			int index = IndexOf(view);
			if (index < 0) return false;

			for (int i = index + 1; i < _items.Count; i++)
			{
				var above = _items[i];
				if (above != null && Covers(above.StackBehaviour)) return true;
			}

			return false;
		}

		public void Clear() => _items.Clear();

		/// <summary>Copies top-first into <paramref name="destination"/>, matching <c>Stack&lt;T&gt;.ToArray()</c> order.</summary>
		public void CopyTopFirst(List<UIView> destination)
		{
			for (int i = _items.Count - 1; i >= 0; i--)
			{
				destination.Add(_items[i]);
			}
		}

		public Enumerator GetEnumerator() => new(_items);

		private static bool Covers(ViewStackBehaviour behaviour) =>
			behaviour is ViewStackBehaviour.HideBelow
				or ViewStackBehaviour.PauseAndHideBelow
				or ViewStackBehaviour.PauseOnlyBelow;

		/// <summary>Top-first enumeration, the same order <c>Stack&lt;T&gt;</c> yields.</summary>
		public struct Enumerator
		{
			private readonly List<UIView> _items;
			private          int          _index;

			internal Enumerator(List<UIView> items)
			{
				_items = items;
				_index = items.Count;
			}

			public UIView Current => _items[_index];

			public bool MoveNext() => --_index >= 0;
		}
	}
}
