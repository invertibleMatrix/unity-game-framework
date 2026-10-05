using System;
using System.Collections.Generic;
using System.Text;
using AK.Kernel.Collections;
using AK.Kernel.Timing;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Keeps pointer input from reaching anything while someone holds it: a tutorial step until
	/// its presentation is up, a spotlight while its iris plays. While any hold is outstanding,
	/// the UISystem's <see cref="UIInputBlocker"/> answers every pointer ahead of all other UI
	/// and world raycasters, so presses and clicks land on nothing. The EventSystem stays on:
	/// drags already under way carry on, and code that asks whether the pointer is over UI
	/// (camera pans, world taps) is told it is.
	///
	/// <see cref="Hold(string)"/> returns an <see cref="InputHold"/> that ends exactly that hold,
	/// once: releasing it again, or after <see cref="ReleaseAll"/>, does nothing and can't end
	/// anyone else's hold. A hold may carry a timeout. When it runs out, the gate ends the hold
	/// itself and logs a warning naming its owner, so a holder that never gets to release can't
	/// leave the game unresponsive. Timeouts count unscaled time, advanced by <see cref="Tick"/>,
	/// so a game paused at timeScale 0 still gets its input back.
	///
	/// One gate per UISystem, shared by everything on it through <see cref="IUISystem.InputGate"/>.
	/// Main thread only.
	/// </summary>
	public sealed class UIInputGate
	{
		/// <summary>
		/// The most one <see cref="Tick"/> advances the gate's clock: <see cref="ForegroundTime.MaxFrameSeconds"/>.
		/// A longer frame, such as the first one after the app returns from the background, counts
		/// as this long, so a stall can't run a hold out before its holder has had a frame to release it.
		/// </summary>
		public const float MaxFrameGapSeconds = (float)ForegroundTime.MaxFrameSeconds;

		private const string Unnamed = "(unnamed)";

		private readonly HoldCounter<string> _holds   = new();
		private readonly List<string>        _scratch = new(4);
		private double _now;

		/// <summary>True while any hold is outstanding.</summary>
		public bool IsHeld => _holds.IsHeld;

		/// <summary>Holds outstanding.</summary>
		public int HoldCount => _holds.Count;

		/// <summary>
		/// Blocks input until the returned hold is released. <paramref name="owner"/> names the
		/// holder in diagnostics.
		/// </summary>
		public InputHold Hold(string owner)
		{
			return new InputHold(this, _holds.Acquire(owner ?? Unnamed));
		}

		/// <summary>
		/// Blocks input until the returned hold is released, or until <paramref name="timeoutSeconds"/>
		/// of unscaled time have passed, whichever comes first. A hold that times out is logged as
		/// a warning: its holder should have released it. <see cref="float.PositiveInfinity"/>
		/// means no timeout.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutSeconds"/> is negative or NaN.</exception>
		public InputHold Hold(string owner, float timeoutSeconds)
		{
			if (!(timeoutSeconds >= 0f))
			{
				throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "A hold's timeout must be zero or more seconds.");
			}

			return new InputHold(this, _holds.Acquire(owner ?? Unnamed, _now + timeoutSeconds));
		}

		/// <summary>
		/// Ends every hold at once. For recovery when the holders are gone; a holder that is
		/// still around should release its own hold.
		/// </summary>
		public void ReleaseAll()
		{
			_holds.ReleaseAll();
		}

		/// <summary>
		/// Advances the gate's clock by <paramref name="unscaledDeltaSeconds"/>, counted as
		/// <see cref="ForegroundTime.FrameStep"/> counts a frame, and ends the holds whose timeout
		/// has run out. A delta that is negative or NaN leaves the clock where it is. The UISystem's
		/// <see cref="UIInputBlocker"/> calls this every frame with <c>Time.unscaledDeltaTime</c>.
		/// </summary>
		public void Tick(float unscaledDeltaSeconds)
		{
			_now += ForegroundTime.FrameStep(unscaledDeltaSeconds);

			if (!_holds.IsHeld || _holds.ReleaseExpired(_now, _scratch) == 0) return;

			for (int i = 0; i < _scratch.Count; i++)
			{
				Debug.LogWarning($"[UIInputGate] The input hold of '{_scratch[i]}' timed out and was ended by the gate; its holder never released it.");
			}

			_scratch.Clear();
		}

		/// <summary>The owners of the outstanding holds, for diagnostics: "none" when nothing holds.</summary>
		public string DescribeHolders()
		{
			if (!_holds.IsHeld) return "none";

			_holds.CopyTagsTo(_scratch);
			var text = new StringBuilder();
			for (int i = 0; i < _scratch.Count; i++)
			{
				if (i > 0) text.Append(", ");
				text.Append(_scratch[i]);
			}

			_scratch.Clear();
			return text.ToString();
		}

		internal bool IsLive(HoldToken token) => _holds.IsLive(token);

		internal bool Release(HoldToken token) => _holds.Release(token);
	}
}
