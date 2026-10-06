using System.Collections.Generic;
using AK.Kernel.Timing;
using NUnit.Framework;

namespace AK.Tests.Kernel
{
	public class ForegroundTimeTests
	{
		// Each case: the real time a frame took, then the time it counts for.
		private static IEnumerable<TestCaseData> Frames()
		{
			yield return new TestCaseData(1d / 60d, 1d / 60d).SetName("A frame counts its real duration");
			yield return new TestCaseData(0.5d, 0.5d).SetName("A frame at the cap counts in full");
			yield return new TestCaseData(0.75d, 0.5d).SetName("A long frame counts the cap");
			yield return new TestCaseData(3600d, 0.5d).SetName("An hour in the background counts the cap");
			yield return new TestCaseData(double.PositiveInfinity, 0.5d).SetName("An endless stall counts the cap");
			yield return new TestCaseData(0d, 0d).SetName("No time counts nothing");
			yield return new TestCaseData(-1d, 0d).SetName("A clock stepping back counts nothing");
			yield return new TestCaseData(double.NegativeInfinity, 0d).SetName("A clock stepping back without end counts nothing");
			yield return new TestCaseData(double.NaN, 0d).SetName("A reading that is not a number counts nothing");
		}

		[TestCaseSource(nameof(Frames))]
		public void FrameStep_CountsAFrame_UpToTheCap(double realSeconds, double counted)
		{
			Assert.AreEqual(counted, ForegroundTime.FrameStep(realSeconds));
		}
	}
}
