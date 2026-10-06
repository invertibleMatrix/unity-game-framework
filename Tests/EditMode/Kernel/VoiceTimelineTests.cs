using System;
using AK.Kernel.Audio;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class VoiceTimelineTests
	{
		private const float Tolerance = 1e-5f;

		private static VoiceTiming Timing(double startDelay = 0d, double stopAfter = 0d, double fadeIn = 0d, double fadeOut = 0d,
		                                  double loopInterval = 0d, bool loop = false, bool playEveryClip = false)
		{
			return new VoiceTiming(startDelay, stopAfter, fadeIn, fadeOut, loopInterval, loop, playEveryClip);
		}

		/// <summary>Starts a voice whose clips each play for <paramref name="clipSeconds"/>.</summary>
		private static VoiceTimeline Started(in VoiceTiming timing, int clipCount = 1, double clipSeconds = 10d)
		{
			var voice = new VoiceTimeline();
			if (voice.Start(timing, clipCount) == VoiceCue.StartClip) voice.ClipStarted(clipSeconds);
			return voice;
		}

		/// <summary>Steps the voice, reporting <paramref name="clipSeconds"/> for a clip the step starts.</summary>
		private static VoiceCue Step(VoiceTimeline voice, double seconds, double clipSeconds = 10d)
		{
			VoiceCue cue = voice.Step(seconds);
			if (cue == VoiceCue.StartClip) voice.ClipStarted(clipSeconds);
			return cue;
		}

		// ------------------------------------------------------------------ starting

		[Test]
		public void NewTimeline_IsIdle_AndIgnoresStepsAndStops()
		{
			var voice = new VoiceTimeline();

			Assert.AreEqual(VoiceStage.Idle, voice.Stage);
			Assert.IsFalse(voice.IsPlaying);
			Assert.AreEqual(-1, voice.Clip);
			Assert.AreEqual(0f, voice.Gain);
			Assert.AreEqual(VoiceCue.None, voice.Step(1d));
			Assert.AreEqual(VoiceCue.None, voice.Stop());
		}

		[Test]
		public void Start_WithNoDelayOrFadeIn_StartsTheFirstClipAtFullVolume()
		{
			var voice = new VoiceTimeline();

			Assert.AreEqual(VoiceCue.StartClip, voice.Start(Timing(), 3));
			Assert.AreEqual(0, voice.Clip);
			Assert.AreEqual(VoiceStage.Holding, voice.Stage);
			Assert.AreEqual(1f, voice.Gain);
			Assert.IsTrue(voice.IsPlaying);
		}

		[Test]
		public void FadeIn_RaisesTheGain_ThenHolds()
		{
			VoiceTimeline voice = Started(Timing(fadeIn: 1d));
			Assert.AreEqual(VoiceStage.FadingIn, voice.Stage);
			Assert.AreEqual(0f, voice.Gain, "a clip that fades in starts silent");

			Assert.AreEqual(VoiceCue.None, voice.Step(0.25d));
			Assert.AreEqual(0.25f, voice.Gain, Tolerance);

			Assert.AreEqual(VoiceCue.None, voice.Step(0.75d));
			Assert.AreEqual(1f, voice.Gain);
			Assert.AreEqual(VoiceStage.Holding, voice.Stage);
		}

		[Test]
		public void StartDelay_HoldsTheFirstClip_UntilItRunsOut()
		{
			var voice = new VoiceTimeline();

			Assert.AreEqual(VoiceCue.None, voice.Start(Timing(startDelay: 1d), 1));
			Assert.AreEqual(VoiceStage.Delaying, voice.Stage);
			Assert.AreEqual(-1, voice.Clip);
			Assert.IsTrue(voice.IsPlaying, "a voice is playing through its start delay");

			Assert.AreEqual(VoiceCue.None, voice.Step(0.75d));
			Assert.AreEqual(VoiceCue.StartClip, voice.Step(0.25d));
			Assert.AreEqual(0, voice.Clip);
		}

		[Test]
		public void Start_Again_ReplacesThePlay()
		{
			VoiceTimeline voice = Started(Timing(fadeIn: 1d, playEveryClip: true), clipCount: 3, clipSeconds: 1d);
			Step(voice, 2d, 1d);
			Assert.AreEqual(1, voice.Clip);

			Assert.AreEqual(VoiceCue.StartClip, voice.Start(Timing(fadeIn: 1d), 3));
			Assert.AreEqual(0, voice.Clip);
			Assert.AreEqual(0f, voice.Gain);
			Assert.AreEqual(VoiceStage.FadingIn, voice.Stage);
		}

		[Test]
		public void Start_WithNoClips_Throws()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => new VoiceTimeline().Start(Timing(), 0));
		}

		[Test]
		public void Timing_BelowZeroOrNaN_CountsAsZero()
		{
			var timing = new VoiceTiming(-1d, double.NaN, double.NegativeInfinity, -0.5d, double.NaN, false, false);

			Assert.AreEqual(0d, timing.StartDelay);
			Assert.AreEqual(0d, timing.StopAfter);
			Assert.AreEqual(0d, timing.FadeIn);
			Assert.AreEqual(0d, timing.FadeOut);
			Assert.AreEqual(0d, timing.LoopInterval);
		}

		// ------------------------------------------------------------------ clips

		[Test]
		public void AClip_IsHeldForItsPlayingTimeLessTheFadeOut_ThenFadesOut()
		{
			VoiceTimeline voice = Started(Timing(fadeOut: 0.5d), clipSeconds: 2d);

			Assert.AreEqual(VoiceCue.None, voice.Step(1.4d));
			Assert.AreEqual(VoiceStage.Holding, voice.Stage);

			Assert.AreEqual(VoiceCue.None, voice.Step(0.35d));
			Assert.AreEqual(VoiceStage.FadingOut, voice.Stage);
			Assert.AreEqual(0.5f, voice.Gain, Tolerance, "0.25 s into a 0.5 s fade-out");

			Assert.AreEqual(VoiceCue.End, voice.Step(0.25d));
			Assert.AreEqual(VoiceStage.Idle, voice.Stage);
			Assert.AreEqual(0f, voice.Gain);
		}

		[TestCase(false, false, 1, Description = "one clip")]
		[TestCase(false, true, 3, Description = "every clip, once")]
		public void AVoiceThatDoesNotLoop_PlaysItsClips_ThenEnds(bool loop, bool playEveryClip, int expectedClips)
		{
			VoiceTimeline voice = Started(Timing(loop: loop, playEveryClip: playEveryClip), clipCount: 3, clipSeconds: 1d);

			int started = 1;
			VoiceCue cue;
			while ((cue = Step(voice, 1d, 1d)) == VoiceCue.StartClip)
			{
				Assert.AreEqual(started, voice.Clip, "clips play in their order");
				started++;
			}

			Assert.AreEqual(VoiceCue.End, cue);
			Assert.AreEqual(expectedClips, started);
		}

		[Test]
		public void ALoopingVoice_StartsOverAfterItsLastClip()
		{
			VoiceTimeline voice = Started(Timing(loop: true), clipCount: 2, clipSeconds: 1d);

			Assert.AreEqual(VoiceCue.StartClip, Step(voice, 1d, 1d));
			Assert.AreEqual(1, voice.Clip);
			Assert.AreEqual(VoiceCue.StartClip, Step(voice, 1d, 1d));
			Assert.AreEqual(0, voice.Clip);
			Assert.AreEqual(VoiceCue.StartClip, Step(voice, 1d, 1d));
			Assert.AreEqual(1, voice.Clip);
		}

		[Test]
		public void LoopInterval_CapsTheHold()
		{
			VoiceTimeline voice = Started(Timing(loopInterval: 0.5d, loop: true), clipSeconds: 2d);

			Assert.AreEqual(VoiceCue.None, voice.Step(0.4d));
			Assert.AreEqual(VoiceCue.StartClip, voice.Step(0.1d));
		}

		[TestCase(0d)]
		[TestCase(-1d)]
		[TestCase(double.NaN)]
		public void AClipWithNoPlayingTime_IsHeldForNoTime(double clipSeconds)
		{
			VoiceTimeline voice = Started(Timing(), clipSeconds: clipSeconds);

			Assert.AreEqual(VoiceCue.End, voice.Step(0d), "a stage of no length ends at the next step");
		}

		[Test]
		public void AClipNeverReported_IsHeldForNoTime()
		{
			var voice = new VoiceTimeline();
			voice.Start(Timing(), 1);

			Assert.AreEqual(VoiceCue.End, voice.Step(0d));
		}

		// ------------------------------------------------------------------ steps

		[TestCase(-1d)]
		[TestCase(double.NaN)]
		[TestCase(double.NegativeInfinity)]
		public void AStepBelowZeroOrNaN_MovesNothing(double seconds)
		{
			VoiceTimeline voice = Started(Timing(fadeIn: 1d));
			voice.Step(0.5d);

			Assert.AreEqual(VoiceCue.None, voice.Step(seconds));
			Assert.AreEqual(0.5f, voice.Gain, Tolerance);
		}

		[Test]
		public void AStep_StartsAtMostOneClip_AndTheNewClipStartsAtThatStep()
		{
			VoiceTimeline voice = Started(Timing(fadeIn: 1d, loop: true), clipCount: 3, clipSeconds: 1d);

			Assert.AreEqual(VoiceCue.StartClip, Step(voice, 10d, 1d), "ten seconds would cover several clips");
			Assert.AreEqual(1, voice.Clip);
			Assert.AreEqual(0f, voice.Gain, "the new clip starts its fade-in at this step");
			Assert.AreEqual(VoiceStage.FadingIn, voice.Stage);
		}

		[Test]
		public void ALongStepWithinAClip_KeepsItsFadesOnTime()
		{
			// Fade in over 1 s, hold 2 s (3 s of clip less the 1 s fade-out), fade out over 1 s.
			VoiceTimeline voice = Started(Timing(fadeIn: 1d, fadeOut: 1d), clipSeconds: 3d);

			Assert.AreEqual(VoiceCue.None, voice.Step(3.5d));
			Assert.AreEqual(VoiceStage.FadingOut, voice.Stage);
			Assert.AreEqual(0.5f, voice.Gain, Tolerance);
		}

		// ------------------------------------------------------------------ stopping

		[Test]
		public void Stop_FadesOutFromTheGainNow_ThenEnds()
		{
			VoiceTimeline voice = Started(Timing(fadeIn: 1d, fadeOut: 1d));
			voice.Step(0.5d);

			Assert.AreEqual(VoiceCue.None, voice.Stop());
			Assert.IsTrue(voice.IsStopping);
			Assert.AreEqual(0.5f, voice.Gain, Tolerance);

			Assert.AreEqual(VoiceCue.None, voice.Step(0.5d));
			Assert.AreEqual(0.25f, voice.Gain, Tolerance);
			Assert.AreEqual(VoiceCue.End, voice.Step(0.5d));
			Assert.IsFalse(voice.IsPlaying);
		}

		[Test]
		public void Stop_Twice_KeepsTheFirstFadeOut()
		{
			VoiceTimeline voice = Started(Timing(fadeOut: 1d));
			voice.Stop();
			voice.Step(0.5d);

			Assert.AreEqual(VoiceCue.None, voice.Stop());
			Assert.AreEqual(0.5f, voice.Gain, Tolerance);
			Assert.AreEqual(VoiceCue.End, voice.Step(0.5d));
		}

		[Test]
		public void Stop_DuringTheStartDelay_EndsAtOnce()
		{
			var voice = new VoiceTimeline();
			voice.Start(Timing(startDelay: 1d, fadeOut: 1d), 1);

			Assert.AreEqual(VoiceCue.End, voice.Stop());
			Assert.IsFalse(voice.IsPlaying);
		}

		[Test]
		public void Stop_WhileSilentAtTheStartOfAFadeIn_EndsAtOnce()
		{
			VoiceTimeline voice = Started(Timing(fadeIn: 1d, fadeOut: 1d));

			Assert.AreEqual(VoiceCue.End, voice.Stop());
		}

		[Test]
		public void Stop_WithNoFadeOut_EndsAtOnce()
		{
			VoiceTimeline voice = Started(Timing());

			Assert.AreEqual(VoiceCue.End, voice.Stop());
		}

		[Test]
		public void StopAfter_EndsTheVoiceAsItsFadeOutEnds_EvenMidClip()
		{
			VoiceTimeline voice = Started(Timing(stopAfter: 3d, fadeOut: 1d, loop: true));

			Assert.AreEqual(VoiceCue.None, voice.Step(1.75d));
			Assert.IsFalse(voice.IsStopping);

			Assert.AreEqual(VoiceCue.None, voice.Step(0.25d));
			Assert.IsTrue(voice.IsStopping, "the fade-out starts a fade-out's length before the stop-after time");
			Assert.AreEqual(1f, voice.Gain, Tolerance);

			Assert.AreEqual(VoiceCue.None, voice.Step(0.5d));
			Assert.AreEqual(0.5f, voice.Gain, Tolerance);
			Assert.AreEqual(VoiceCue.End, voice.Step(0.5d));
		}

		[Test]
		public void StopAfter_CarriesTheTimeOverIntoTheFadeOut()
		{
			VoiceTimeline voice = Started(Timing(stopAfter: 2d, fadeOut: 1d));

			Assert.AreEqual(VoiceCue.None, voice.Step(1.5d));
			Assert.IsTrue(voice.IsStopping);
			Assert.AreEqual(0.5f, voice.Gain, Tolerance);
			Assert.AreEqual(VoiceCue.End, voice.Step(0.5d));
		}

		[Test]
		public void StopAfter_CountsFromTheFirstClip()
		{
			var voice = new VoiceTimeline();
			voice.Start(Timing(startDelay: 1d, stopAfter: 2d), 1);

			Assert.AreEqual(VoiceCue.StartClip, Step(voice, 1d));
			Assert.AreEqual(VoiceCue.None, voice.Step(1.75d));
			Assert.AreEqual(VoiceCue.End, voice.Step(0.25d));
		}

		[Test]
		public void StopAfter_HoldsAcrossClips()
		{
			VoiceTimeline voice = Started(Timing(stopAfter: 2.5d, loop: true), clipCount: 2, clipSeconds: 1d);

			Assert.AreEqual(VoiceCue.StartClip, Step(voice, 1d, 1d));
			Assert.AreEqual(VoiceCue.StartClip, Step(voice, 1d, 1d));
			Assert.AreEqual(VoiceCue.End, voice.Step(0.5d));
		}

		[Test]
		public void Reset_EndsTheVoiceWithoutACue()
		{
			VoiceTimeline voice = Started(Timing(fadeOut: 1d));

			voice.Reset();

			Assert.IsFalse(voice.IsPlaying);
			Assert.AreEqual(-1, voice.Clip);
			Assert.AreEqual(0f, voice.Gain);
			Assert.AreEqual(VoiceCue.None, voice.Step(1d));
		}

		// ------------------------------------------------------------------ cost

		[Test]
		public void StartingAndSteppingVoices_AllocatesNothing()
		{
			var voice = new VoiceTimeline();
			VoiceTiming timing = Timing(fadeIn: 0.1d, fadeOut: 0.1d, loop: true, stopAfter: 3d);

			void PlayOnce()
			{
				voice.Start(timing, 3);
				voice.ClipStarted(1d);
				for (int i = 0; i < 100; i++)
				{
					if (voice.Step(1d / 30d) == VoiceCue.StartClip) voice.ClipStarted(1d);
				}

				voice.Stop();
			}

			PlayOnce();
			Assert.AreEqual(0, GcAllocations.Count(PlayOnce));
		}
	}
}
