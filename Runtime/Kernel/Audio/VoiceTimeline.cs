using System;

namespace AK.Kernel.Audio
{
	/// <summary>
	/// The timing of a voice, in seconds: what a <see cref="VoiceTimeline"/> plays to. A time
	/// that is negative or NaN counts as zero.
	/// </summary>
	public readonly struct VoiceTiming
	{
		/// <summary>Waited out before the first clip starts.</summary>
		public readonly double StartDelay;

		/// <summary>
		/// From the start of the first clip, when the voice stops: its fade-out ends then. Zero
		/// for no limit.
		/// </summary>
		public readonly double StopAfter;

		/// <summary>Each clip fades in over this.</summary>
		public readonly double FadeIn;

		/// <summary>Each clip fades out over this before the next one starts, and the voice fades out over it when it stops.</summary>
		public readonly double FadeOut;

		/// <summary>Above zero, the longest a clip is held at full volume before it fades out.</summary>
		public readonly double LoopInterval;

		/// <summary>Plays the clips over and over until the voice is stopped.</summary>
		public readonly bool Loop;

		/// <summary>Plays every clip in turn. A voice that neither loops nor plays every clip plays only the first.</summary>
		public readonly bool PlayEveryClip;

		public VoiceTiming(double startDelay, double stopAfter, double fadeIn, double fadeOut, double loopInterval,
		                   bool loop, bool playEveryClip)
		{
			StartDelay    = Positive(startDelay);
			StopAfter     = Positive(stopAfter);
			FadeIn        = Positive(fadeIn);
			FadeOut       = Positive(fadeOut);
			LoopInterval  = Positive(loopInterval);
			Loop          = loop;
			PlayEveryClip = playEveryClip;
		}

		private static double Positive(double seconds) => seconds > 0d ? seconds : 0d;
	}

	/// <summary>Where a <see cref="VoiceTimeline"/> is.</summary>
	public enum VoiceStage : byte
	{
		/// <summary>Not playing: never started, or ended.</summary>
		Idle = 0,

		/// <summary>Waiting out the start delay. Nothing sounds yet.</summary>
		Delaying = 1,

		/// <summary>A clip fades in.</summary>
		FadingIn = 2,

		/// <summary>A clip plays at full volume.</summary>
		Holding = 3,

		/// <summary>A clip fades out before the next one starts, or before the voice ends.</summary>
		FadingOut = 4,

		/// <summary>The voice was stopped, or reached its stop-after time, and fades out to its end.</summary>
		Stopping = 5,
	}

	/// <summary>What the player of a <see cref="VoiceTimeline"/> does after a call.</summary>
	public enum VoiceCue : byte
	{
		/// <summary>Nothing beyond applying <see cref="VoiceTimeline.Gain"/>.</summary>
		None = 0,

		/// <summary>
		/// Start the clip at <see cref="VoiceTimeline.Clip"/> in the play order, then report how
		/// long it plays with <see cref="VoiceTimeline.ClipStarted"/>.
		/// </summary>
		StartClip = 1,

		/// <summary>The voice is over: stop its sound.</summary>
		End = 2,
	}

	/// <summary>
	/// When a voice plays its clips, and how loud. The player steps it by the time each frame
	/// adds, and does what each call's <see cref="VoiceCue"/> says.
	///
	/// <para>The voice waits out its start delay. Each clip then fades in, is held until its
	/// playing time less the fade-out has passed (at most <see cref="VoiceTiming.LoopInterval"/>),
	/// and fades out; the next clip starts as the fade-out ends. One pass plays every clip when
	/// the voice loops or plays every clip, and the first one otherwise. A looping voice starts
	/// over after its last clip; any other voice ends there.</para>
	///
	/// <para>Stopping fades out from wherever the voice is, over its fade-out, then ends it: on
	/// request with <see cref="Stop"/>, or by itself at its stop-after time, checked at every step.
	/// A voice that is silent when it stops, as during its start delay, ends at once.</para>
	///
	/// <para>A clip starts at the step that starts it: time a step carries past the end of the
	/// start delay or of a fade-out is not counted into the new clip. Within a clip it is, so a
	/// long step can't push the clip's fades late. A step starts at most one clip.</para>
	///
	/// Deterministic and allocation-free.
	/// </summary>
	public sealed class VoiceTimeline
	{
		private VoiceTiming _timing;
		private int         _clipsPerPass;
		private int         _clip = -1;
		private double      _elapsed;
		private double      _hold;
		private bool        _clipsStarted;
		private double      _sinceFirstClip;
		private double      _stopAt;
		private float       _gain;
		private float       _fadeFrom;
		private VoiceStage  _stage;

		public VoiceStage Stage => _stage;

		/// <summary>From <see cref="Start"/> until the voice ends: its start delay and fade-out included.</summary>
		public bool IsPlaying => _stage != VoiceStage.Idle;

		/// <summary>Fading out to its end, after <see cref="Stop"/> or at its stop-after time.</summary>
		public bool IsStopping => _stage == VoiceStage.Stopping;

		/// <summary>The clip playing, as an index into the play order; -1 before the first one.</summary>
		public int Clip => _clip;

		/// <summary>How loud the voice is now, from 0 to 1, as a share of its full volume.</summary>
		public float Gain => _gain;

		/// <summary>
		/// Starts the voice over the first <paramref name="clipCount"/> clips of its play order,
		/// replacing anything it was doing. Starts the first clip at once when there is no start
		/// delay.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="clipCount"/> is below 1.</exception>
		public VoiceCue Start(in VoiceTiming timing, int clipCount)
		{
			if (clipCount < 1) throw new ArgumentOutOfRangeException(nameof(clipCount), clipCount, "A voice needs a clip to play.");

			_timing         = timing;
			_clipsPerPass   = timing.Loop || timing.PlayEveryClip ? clipCount : 1;
			_clip           = -1;
			_elapsed        = 0d;
			_hold           = 0d;
			_clipsStarted   = false;
			_sinceFirstClip = 0d;
			_stopAt         = Math.Max(0d, timing.StopAfter - timing.FadeOut);
			_gain           = 0f;
			_fadeFrom       = 0f;

			if (timing.StartDelay > 0d)
			{
				_stage = VoiceStage.Delaying;
				return VoiceCue.None;
			}

			return BeginClip(0);
		}

		/// <summary>
		/// Reports how long the clip just started plays, in seconds: its length at the speed it
		/// plays at. Call it after every <see cref="VoiceCue.StartClip"/>, before the next step; a
		/// clip never reported is held for no time. Zero, less or NaN counts as zero.
		/// </summary>
		public void ClipStarted(double playingSeconds)
		{
			double hold = playingSeconds - _timing.FadeOut;
			if (!(hold > 0d)) hold = 0d;

			if (_timing.LoopInterval > 0d && hold > _timing.LoopInterval) hold = _timing.LoopInterval;

			_hold = hold;
		}

		/// <summary>
		/// Moves the voice on by <paramref name="seconds"/>. Zero, less or NaN moves it by nothing,
		/// but still lets a stage of no length end.
		/// </summary>
		public VoiceCue Step(double seconds)
		{
			if (_stage == VoiceStage.Idle) return VoiceCue.None;

			double step = seconds > 0d ? seconds : 0d;
			_elapsed += step;

			if (_clipsStarted)
			{
				_sinceFirstClip += step;

				if (_stage != VoiceStage.Stopping && _timing.StopAfter > 0d && _sinceFirstClip >= _stopAt)
				{
					return BeginStopping(_sinceFirstClip - _stopAt);
				}
			}

			while (true)
			{
				switch (_stage)
				{
					case VoiceStage.Delaying:
						if (_elapsed < _timing.StartDelay) return VoiceCue.None;
						return BeginClip(0);

					case VoiceStage.FadingIn:
						if (_elapsed < _timing.FadeIn)
						{
							_gain = (float)(_elapsed / _timing.FadeIn);
							return VoiceCue.None;
						}

						_elapsed -= _timing.FadeIn;
						_gain = 1f;
						_stage = VoiceStage.Holding;
						break;

					case VoiceStage.Holding:
						if (_elapsed < _hold) return VoiceCue.None;

						_elapsed -= _hold;
						_fadeFrom = _gain;
						_stage = VoiceStage.FadingOut;
						break;

					case VoiceStage.FadingOut:
						if (_elapsed < _timing.FadeOut)
						{
							_gain = Faded(_fadeFrom, _elapsed, _timing.FadeOut);
							return VoiceCue.None;
						}

						_gain = 0f;
						return NextClip();

					case VoiceStage.Stopping:
						if (_elapsed < _timing.FadeOut)
						{
							_gain = Faded(_fadeFrom, _elapsed, _timing.FadeOut);
							return VoiceCue.None;
						}

						return End();

					default:
						return VoiceCue.None;
				}
			}
		}

		/// <summary>
		/// Stops the voice: it fades out from where it is, or ends at once when it is silent or
		/// has no fade-out. Does nothing once it is stopping or idle.
		/// </summary>
		public VoiceCue Stop()
		{
			if (_stage == VoiceStage.Idle || _stage == VoiceStage.Stopping) return VoiceCue.None;

			return BeginStopping(0d);
		}

		/// <summary>Ends the voice without a cue, as a voice that never played.</summary>
		public void Reset()
		{
			_stage = VoiceStage.Idle;
			_clip  = -1;
			_gain  = 0f;
		}

		private VoiceCue BeginClip(int clip)
		{
			bool fadesIn = _timing.FadeIn > 0d;

			_clip    = clip;
			_stage   = fadesIn ? VoiceStage.FadingIn : VoiceStage.Holding;
			_elapsed = 0d;
			_hold    = 0d;
			_gain    = fadesIn ? 0f : 1f;

			if (!_clipsStarted)
			{
				_clipsStarted   = true;
				_sinceFirstClip = 0d;
			}

			return VoiceCue.StartClip;
		}

		private VoiceCue NextClip()
		{
			int next = _clip + 1;
			if (next >= _clipsPerPass)
			{
				if (!_timing.Loop) return End();
				next = 0;
			}

			return BeginClip(next);
		}

		/// <summary>Fades out from the gain now, <paramref name="elapsed"/> into the fade-out. A silent voice ends at once.</summary>
		private VoiceCue BeginStopping(double elapsed)
		{
			if (_gain <= 0f || !(elapsed < _timing.FadeOut)) return End();

			_fadeFrom = _gain;
			_elapsed  = elapsed;
			_stage    = VoiceStage.Stopping;
			_gain     = Faded(_fadeFrom, elapsed, _timing.FadeOut);
			return VoiceCue.None;
		}

		private VoiceCue End()
		{
			_stage = VoiceStage.Idle;
			_gain  = 0f;
			return VoiceCue.End;
		}

		private static float Faded(float from, double elapsed, double duration) => (float)(from * (1d - elapsed / duration));
	}
}
