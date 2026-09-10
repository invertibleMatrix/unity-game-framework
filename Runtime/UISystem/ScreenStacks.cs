using System.Collections.Generic;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// One navigation stack per <see cref="UIChannel"/> for screens (views with a
	/// <see cref="UIViewChannel"/>). The default channel always exists. Pushing gives a
	/// screen's canvas its depth-based sorting order; popping and mid-stack removal re-sort
	/// the survivors so a later push can never collide with a baked-in order.
	/// </summary>
	internal sealed class ScreenStacks
	{
		private readonly Dictionary<UIChannel, ViewStack> _stacks = new();
		private readonly Camera                           _uiCamera;

		public ScreenStacks(Camera uiCamera)
		{
			_uiCamera = uiCamera;
			_stacks[UIChannel.HUD] = new ViewStack();
		}

		public IReadOnlyDictionary<UIChannel, ViewStack> Stacks => _stacks;

		public Dictionary<UIChannel, ViewStack>.ValueCollection All => _stacks.Values;

		public bool TryGet(UIChannel channel, out ViewStack stack) => _stacks.TryGetValue(channel, out stack);

		/// <summary>Pushes a screen onto its channel and initialises its canvas at that depth (bottom is depth 1).</summary>
		public ViewStack Push(UIView screen, UIChannel channel)
		{
			if (!_stacks.TryGetValue(channel, out var stack))
			{
				stack = new ViewStack();
				_stacks[channel] = stack;
			}

			stack.Push(screen);
			screen.Channel.Initialize(_uiCamera, stack.Count);
			return stack;
		}

		public void Pop(ViewStack stack)
		{
			stack.Pop();
			Resort(stack);
		}

		/// <summary>Removes a screen from wherever it sits in <paramref name="stack"/>; false when it was not there.</summary>
		public bool Remove(ViewStack stack, UIView screen)
		{
			if (!stack.Remove(screen)) return false;
			Resort(stack);
			return true;
		}

		public void RemoveEverywhere(UIView view)
		{
			foreach (var stack in _stacks.Values)
			{
				stack.Remove(view);
			}
		}

		/// <summary>
		/// Host for a fragment shown without an explicit parent: the top screen of the
		/// highest non-empty channel (overlays over menus over HUD), or of
		/// <paramref name="preferred"/> alone when one is requested.
		/// </summary>
		public UIView FindBestHost(UIChannel? preferred)
		{
			UIView    best      = null;
			UIChannel bestOrder = (UIChannel)(-1);   // below HUD (0) so HUD can win as the fallback

			foreach ((UIChannel channel, ViewStack stack) in _stacks)
			{
				if (stack.Count == 0) continue;
				if (preferred.HasValue && channel != preferred.Value) continue;

				if (channel > bestOrder)
				{
					bestOrder = channel;
					best = stack.Peek();
				}
			}

			return best;
		}

		private static void Resort(ViewStack stack)
		{
			for (int i = 0; i < stack.Count; i++)
			{
				var screen = stack[i];
				if (screen != null && screen.HasChannel)
				{
					screen.Channel.UpdateSortingOrder(i + 1);
				}
			}
		}
	}
}
