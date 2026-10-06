using AK.Kernel.Timing;
using AK.Systems;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEditor;

namespace AK.Tests
{
	/// <summary>
	/// The UISystem's time reaches its views: each view reads the system's, and the system
	/// hands it to every entrance and exit it plays.
	/// </summary>
	public class UITimeDomainTests
	{
		private UISystemHarness _h;

		[SetUp]    public void SetUp()    => _h = new UISystemHarness();
		[TearDown] public void TearDown() => _h.Dispose();

		private void SetSystemTime(TimeDomain domain)
		{
			var so = new SerializedObject(_h.System);
			so.FindProperty("_timeDomain").enumValueIndex = (int)domain;
			so.ApplyModifiedPropertiesWithoutUndo();
		}

		[Test]
		public void TheSystem_RunsOnUnscaledTime_ByDefault()
		{
			Assert.That(_h.System.TimeDomain, Is.EqualTo(TimeDomain.Unscaled));
		}

		[Test]
		public void AViewOutsideTheSystem_RunsOnUnscaledTime()
		{
			SetSystemTime(TimeDomain.Scaled);
			var prefab = _h.MakePrefab<RecordingScreen>(screen: true);

			Assert.That(prefab.TimeDomain, Is.EqualTo(TimeDomain.Unscaled));
		}

		[TestCase(TimeDomain.Unscaled)]
		[TestCase(TimeDomain.Scaled)]
		public void EntrancesAndExits_RunOnTheSystemsTime(TimeDomain domain)
		{
			SetSystemTime(domain);
			_h.AddHoldAnimation(_h.MakePrefab<RecordingScreen>(screen: true));

			var screen = _h.System.Show<RecordingScreen>();
			var hold = screen.GetComponent<HoldAnimation>();

			Assert.That(screen.TimeDomain, Is.EqualTo(domain));
			Assert.That(hold.LastTime, Is.EqualTo(domain), "the entrance");
			hold.Release();

			_h.System.Close(screen);

			Assert.That(hold.Starts, Is.EqualTo(2), "the exit is in flight");
			Assert.That(hold.LastTime, Is.EqualTo(domain), "the exit");
			hold.Release();
			Assert.That(screen == null || screen.State == ViewState.Hidden, Is.True);
		}
	}
}
