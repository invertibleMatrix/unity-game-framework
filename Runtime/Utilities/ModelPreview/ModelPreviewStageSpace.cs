using AK.Kernel.Collections;
using UnityEngine;

namespace AK.Utilities.Previews
{
	/// <summary>
	/// The offscreen space shared by every booth of one <see cref="ModelPreviewService"/>,
	/// across all its sessions.
	///
	/// Each booth gets a generational handle, which is its identity (a handle kept past the
	/// booth's release stops resolving instead of reaching whatever reuses the slot), and a slot
	/// index, which is its stage position. Freed slots are reused, so positions stay bounded by
	/// the number of booths open at once. Slots sit <see cref="SlotSpacing"/> apart along X, so
	/// a booth camera can't see its neighbours unless a model is hundreds of units across.
	///
	/// Distance doesn't separate directional lights, which reach every model on their layers.
	/// So only one booth at a time keeps its stage's directional lights on, and hands them to
	/// another booth when it goes. Every booth of a service comes from the same stage prefab, so
	/// that one set lights each model as the stage was authored to light a single one.
	/// </summary>
	internal sealed class ModelPreviewStageSpace
	{
		/// <summary>Slot 0's position: far below any gameplay space.</summary>
		public static readonly Vector3 Origin = new(0f, -3000f, 0f);

		/// <summary>Distance between neighbouring slots along X.</summary>
		public const float SlotSpacing = 1000f;

		private readonly SlotMap<ModelPreviewBooth> _booths = new(8);

		/// <summary>The booth whose directional lights are on.</summary>
		private Handle<ModelPreviewBooth> _lit;

		/// <summary>Booths currently placed, across every session.</summary>
		public int Count => _booths.Count;

		public Handle<ModelPreviewBooth> Add(ModelPreviewBooth booth)
		{
			Handle<ModelPreviewBooth> handle = _booths.Add(booth);

			bool lights = !_booths.Contains(_lit);
			booth.SetDirectionalLights(lights);
			if (lights)
			{
				_lit = handle;
			}

			return handle;
		}

		public bool Remove(Handle<ModelPreviewBooth> handle)
		{
			if (!_booths.Remove(handle, out ModelPreviewBooth booth))
			{
				return false;
			}

			if (handle == _lit)
			{
				// Off now, not when the stage is destroyed at the end of the frame.
				booth.SetDirectionalLights(false);
				_lit = Handle<ModelPreviewBooth>.Invalid;

				SlotMap<ModelPreviewBooth>.Enumerator next = _booths.GetEnumerator();
				if (next.MoveNext())
				{
					_lit = next.CurrentHandle;
					next.Current.SetDirectionalLights(true);
				}
			}

			return true;
		}

		public bool TryGet(Handle<ModelPreviewBooth> handle, out ModelPreviewBooth booth) => _booths.TryGet(handle, out booth);

		public static Vector3 PositionOf(Handle<ModelPreviewBooth> handle) => Origin + new Vector3(handle.Index * SlotSpacing, 0f, 0f);
	}
}
