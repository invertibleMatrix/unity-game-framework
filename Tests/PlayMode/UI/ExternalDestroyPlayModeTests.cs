using System.Collections;
using System.Collections.Generic;
using AK.Systems;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AK.Tests.UI
{
	/// <summary>
	/// A view destroyed outside the system, by a direct Destroy or a scene unload, gives back
	/// what it covered as a close would, the frame after the destroy completes. Edit mode never
	/// runs OnDestroy, so only here does the path run end to end.
	/// </summary>
	public class ExternalDestroyPlayModeTests
	{
		/// <summary>Records the pauses and resumes it is given.</summary>
		private abstract class CoveredView : UIView
		{
			public readonly List<string> Log = new();

			public bool Interactable => CanvasGroup.interactable && CanvasGroup.blocksRaycasts;

			public override void OnPause()  => Log.Add("Pause");
			public override void OnResume() => Log.Add("Resume");
		}

		private sealed class BottomScreen : CoveredView { }

		private sealed class TopScreen : CoveredView { }

		private sealed class BottomFragment : CoveredView { }

		private sealed class TopFragment : CoveredView { }

		private LiveUISystem _ui;

		[SetUp]
		public void SetUp() => _ui = new LiveUISystem();

		[TearDown]
		public void TearDown() => _ui.Dispose();

		[UnityTest]
		public IEnumerator DestroyingAScreen_GivesBackTheScreenItCovered(
			[Values(ViewStackBehaviour.HideBelow, ViewStackBehaviour.PauseOnlyBelow)] ViewStackBehaviour behaviour)
		{
			_ui.AddScreenPrefab<BottomScreen>();
			_ui.SetStackBehaviour(_ui.AddScreenPrefab<TopScreen>(), behaviour);

			BottomScreen bottom = _ui.System.Show<BottomScreen>();
			TopScreen    top    = _ui.System.Show<TopScreen>();
			Assert.That(bottom.Interactable, Is.False, "the top screen covers the bottom one");
			bottom.Log.Clear();

			Object.Destroy(top.gameObject);
			yield return null; // the destroy completes at the end of this frame
			yield return null; // and what it covered comes back the frame after

			Assert.That(bottom.Log, Is.EqualTo(new[] { "Resume" }));
			Assert.That(bottom.State, Is.EqualTo(ViewState.Shown));
			Assert.That(bottom.Interactable, Is.True);
			Assert.That(bottom.IsVisible, Is.True);
		}

		[UnityTest]
		public IEnumerator DestroyingAFragment_GivesBackTheFragmentItCovered()
		{
			_ui.AddScreenPrefab<BottomScreen>();
			_ui.AddFragmentPrefab<BottomFragment>();
			_ui.AddFragmentPrefab<TopFragment>();

			BottomScreen   host  = _ui.System.Show<BottomScreen>();
			BottomFragment below = _ui.System.Show<BottomFragment>(ShowOptions.Under(host));
			TopFragment    above = _ui.System.Show<TopFragment>(ShowOptions.Under(host).Serialized(ViewStackBehaviour.HideBelow));
			Assert.That(below.IsVisible, Is.False, "the top fragment hides the one below");
			below.Log.Clear();

			Object.Destroy(above.gameObject);
			yield return null;
			yield return null;

			Assert.That(below.Log, Is.EqualTo(new[] { "Resume" }));
			Assert.That(below.IsVisible, Is.True);
			Assert.That(below.Interactable, Is.True);
			Assert.That(host.Log, Is.Empty, "the screen hosting them was never covered");
		}

		[UnityTest]
		public IEnumerator DestroyingAScreenTogetherWithTheOneItCovered_GivesBackNothing()
		{
			_ui.AddScreenPrefab<BottomScreen>();
			_ui.SetStackBehaviour(_ui.AddScreenPrefab<TopScreen>(), ViewStackBehaviour.HideBelow);

			BottomScreen bottom = _ui.System.Show<BottomScreen>();
			TopScreen    top    = _ui.System.Show<TopScreen>();
			List<string> bottomLog = bottom.Log;
			bottomLog.Clear();

			// A scene unload destroys both in one go.
			Object.Destroy(top.gameObject);
			Object.Destroy(bottom.gameObject);
			yield return null;
			yield return null;

			Assert.That(bottomLog, Is.Empty, "a view destroyed along with the one above it is left alone");
			Assert.That(_ui.System.RegisteredViewCount, Is.Zero);
		}
	}
}
