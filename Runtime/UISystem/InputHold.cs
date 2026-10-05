using System;
using AK.Kernel.Collections;

namespace AK.Systems
{
	/// <summary>
	/// One hold on a <see cref="UIInputGate"/>: input stays blocked until it is released, or
	/// until its timeout runs out. Releasing ends this hold only, and only once. Releasing it
	/// again, or releasing a copy after the original, does nothing. <c>default</c> holds nothing.
	/// </summary>
	public readonly struct InputHold : IDisposable
	{
		private readonly UIInputGate _gate;
		private readonly HoldToken   _token;

		internal InputHold(UIInputGate gate, HoldToken token)
		{
			_gate  = gate;
			_token = token;
		}

		/// <summary>
		/// False for <c>default</c>. True for a hold a gate handed out, even after it ended; ask
		/// <see cref="IsHeld"/> whether it still blocks input.
		/// </summary>
		public bool IsSet => _gate != null;

		/// <summary>True while this hold is still blocking input.</summary>
		public bool IsHeld => _gate != null && _gate.IsLive(_token);

		/// <summary>Ends this hold. True when this call ended it.</summary>
		public bool Release() => _gate != null && _gate.Release(_token);

		/// <summary>Releases the hold, so a <c>using</c> scope can own it.</summary>
		public void Dispose() => Release();
	}
}
