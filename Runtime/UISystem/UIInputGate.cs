using UnityEngine;
using UnityEngine.EventSystems;

namespace AK.Systems
{
	/// <summary>
	/// Ref-counted global UI input block: Hold() stops all pointer/navigation
	/// events by disabling the EventSystem, Release() restores it. The blocked
	/// system is cached (disabling the current one clears EventSystem.current)
	/// together with its prior enabled state, so a release never enables a
	/// system someone else had disabled. Tutorials hold this for the gap
	/// between a step kicking in and its presentation taking over input.
	/// </summary>
	public sealed class UIInputGate
	{
		private int _holds;
		private EventSystem _blocked;
		private bool _wasEnabled;

		public bool IsHeld => _holds > 0;

		public void Hold()
		{
			if (_holds++ > 0) return;

			_blocked = EventSystem.current;
			if (_blocked == null) return;

			_wasEnabled = _blocked.enabled;
			if (_wasEnabled) _blocked.enabled = false;
		}

		public void Release()
		{
			if (_holds == 0 || --_holds > 0) return;

			if (_blocked != null && _wasEnabled)
			{
				_blocked.enabled = true;
			}
			_blocked = null;
		}
	}
}
