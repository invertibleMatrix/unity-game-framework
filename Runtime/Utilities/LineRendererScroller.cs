using AK.Core.Extensions;
using AK.Kernel.Timing;
using UnityEngine;

namespace AK.Utilities
{
	/// <summary>
	/// Scrolls a line's main texture along it, for flowing ropes, beams and trails. The line gets
	/// a material of its own, destroyed with the component, so the shared material stays still.
	/// </summary>
	[RequireComponent(typeof(LineRenderer))]
	public class LineRendererScroller : MonoBehaviour
	{
		[SerializeField] private float ScrollSpeed = 2f;
		[Range(-1, 1)] [SerializeField] private int ScrollDirection = -1;

		[SerializeField, Tooltip("The time the scroll runs on.")]
		private TimeDomain _timeDomain = TimeDomain.Scaled;

		private Material _material;

		private void Awake()
		{
			if (!TryGetComponent(out LineRenderer line))
			{
				Debug.LogError($"[LineRendererScroller] '{name}' has no LineRenderer to scroll.", this);
				enabled = false;
				return;
			}

			// The line's own copy; a line without a material has nothing to scroll.
			_material = line.material;
			if (_material == null) enabled = false;
		}

		private void Update()
		{
			// Wrapped while still a double: a float offset that grew with the time would lose
			// the precision to scroll smoothly after a long session.
			double offset = (_timeDomain.Now() * ScrollSpeed * ScrollDirection) % 1d;
			_material.mainTextureOffset = new Vector2((float)offset, 0f);
		}

		private void OnDestroy()
		{
			if (_material != null) Destroy(_material);
		}
	}
}
