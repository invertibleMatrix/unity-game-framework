using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AK.Systems
{
	/// <summary>
	/// The scene side of a <see cref="UIInputGate"/>, created by the UISystem.
	///
	/// While the gate is held, this raycaster reports a hit at every screen point. It has no
	/// camera and the highest sort priority, so the EventSystem ranks its hit ahead of every
	/// canvas and physics raycaster, and the input module sends presses and clicks to this
	/// object, which handles none of them. A press that started before the hold still gets its
	/// pointer-up but no click, and a drag under way keeps receiving its drag events. While the
	/// gate is free it reports nothing. It draws nothing and needs no canvas.
	///
	/// It also runs the gate's clock on unscaled time, which is what times holds out.
	/// </summary>
	[AddComponentMenu("")]
	[DisallowMultipleComponent]
	internal sealed class UIInputBlocker : BaseRaycaster
	{
		private UIInputGate _gate;

		public override Camera eventCamera => null;

		public override int sortOrderPriority => int.MaxValue;

		public override int renderOrderPriority => int.MaxValue;

		internal UIInputGate Gate => _gate;

		internal void Bind(UIInputGate gate)
		{
			_gate = gate;
		}

		public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
		{
			if (_gate == null || !_gate.IsHeld) return;

			resultAppendList.Add(new RaycastResult
			{
				gameObject     = gameObject,
				module         = this,
				distance       = 0f,
				index          = resultAppendList.Count,
				depth          = int.MaxValue,
				sortingOrder   = int.MaxValue,
				worldNormal    = Vector3.up,
				screenPosition = eventData.position,
				displayIndex   = eventData.displayIndex,
			});
		}

		private void Update()
		{
			_gate?.Tick(Time.unscaledDeltaTime);
		}
	}
}
