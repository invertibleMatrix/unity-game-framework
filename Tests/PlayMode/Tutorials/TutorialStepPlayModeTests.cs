using System;
using System.Collections;
using System.Threading;
using AK.Core;
using AK.Systems;
using AK.Tests.Support;
using AK.Tutorials;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AK.Tests.Tutorials
{
	/// <summary>
	/// Steps presenting through a running UISystem. A fragment step waits out its start delay
	/// before it shows anything. A tooltip step waits for its target and declines when it never
	/// registers; one with no close time lasts until the player closes the tooltip.
	/// </summary>
	public class TutorialStepPlayModeTests
	{
		private const float StartDelaySeconds = 0.3f;
		private const float WaitLimitSeconds  = 10f;

		private sealed class Host : UIView { }

		/// <summary>A tutorial fragment that finishes when told to.</summary>
		private sealed class StepFragment : UIView, ITutorialStepView
		{
			private readonly UniTaskCompletionSource _finished = new();

			public void Finish() => _finished.TrySetResult();

			public UniTask WaitUntilFinish() => _finished.Task;
		}

		private sealed class TestFragmentStep : FragmentStep
		{
			public void Configure(Type fragment) => FragmentType = new TypeRef(fragment);
		}

		private LiveUISystem     _ui;
		private UITargetRegistry _targets;
		private Host             _host;

		private TutorialStepContext Context => new(_ui.System, _targets, null);

		[SetUp]
		public void SetUp()
		{
			_ui = new LiveUISystem();
			_targets = new UITargetRegistry();
			_ui.AddScreenPrefab<Host>();
		}

		[TearDown]
		public void TearDown() => _ui.Dispose();

		/// <summary>Waits frame by frame until <paramref name="condition"/> holds, for at most <see cref="WaitLimitSeconds"/> of real time.</summary>
		private static IEnumerator WaitUntil(Func<bool> condition)
		{
			float limit = Time.realtimeSinceStartup + WaitLimitSeconds;
			while (!condition() && Time.realtimeSinceStartup < limit)
			{
				yield return null;
			}
		}

		private T MakeStep<T>() where T : TutorialStep
		{
			var step = ScriptableObject.CreateInstance<T>();
			_ui.Track(step);
			return step;
		}

		/// <summary>A target id with an identity of its own, as a created asset has.</summary>
		private UITargetId MakeTargetId()
		{
			var id = ScriptableObject.CreateInstance<UITargetId>();
			_ui.Track(id);
			LiveUISystem.SetField(id, "_id", Uid.NewRandom());
			return id;
		}

		private RectTransform MakeTarget()
		{
			var go = new GameObject("Target", typeof(RectTransform));
			go.transform.SetParent(_host.transform, false);
			return (RectTransform)go.transform;
		}

		[UnityTest]
		public IEnumerator AFragmentStep_ShowsItsFragment_OnlyAfterItsStartDelay()
		{
			_ui.AddFragmentPrefab<StepFragment>();
			_host = _ui.System.Show<Host>();

			var step = MakeStep<TestFragmentStep>();
			step.Configure(typeof(StepFragment));
			step.StartDelay = StartDelaySeconds;

			float startedAt = Time.realtimeSinceStartup;
			UniTask present = step.PresentAsync(Context, CancellationToken.None);
			Assert.That(_ui.System.GetView<StepFragment>(), Is.Null, "nothing shows during the delay");

			yield return WaitUntil(() => _ui.System.GetView<StepFragment>() != null);
			StepFragment fragment = _ui.System.GetView<StepFragment>();

			Assert.That(fragment, Is.Not.Null, "the fragment shows once the delay is over");
			Assert.That(Time.realtimeSinceStartup - startedAt, Is.GreaterThan(StartDelaySeconds / 2f), "and the delay was waited out");

			fragment.Finish();
			yield return WaitUntil(() => present.Status.IsCompleted());

			Assert.That(present.Status, Is.EqualTo(UniTaskStatus.Succeeded));
			present.GetAwaiter().GetResult();
			Assert.That(_ui.System.GetView<StepFragment>(), Is.Null, "the step closes its fragment when it finishes");
		}

		[UnityTest]
		public IEnumerator ATooltipStep_WhoseTargetNeverRegisters_Declines_AfterWaitingForIt()
		{
			_ui.AddFragmentPrefab<UIViewTooltip>();
			_host = _ui.System.Show<Host>();

			var step = MakeStep<TooltipStep>();
			step.TargetId = MakeTargetId();

			float   startedAt = Time.realtimeSinceStartup;
			UniTask present;
			using (ExpectedLog.Error("not registered within 3s — declining"))
			{
				present = step.PresentAsync(Context, CancellationToken.None);
				yield return WaitUntil(() => present.Status.IsCompleted());
			}

			Assert.That(Time.realtimeSinceStartup - startedAt, Is.GreaterThan(2.5f), "it waited for the target before declining");
			Assert.That(present.Status, Is.EqualTo(UniTaskStatus.Faulted));
			Assert.Throws<TutorialStepDeclinedException>(() => present.GetAwaiter().GetResult());
			Assert.That(_ui.System.GetView<UIViewTooltip>(), Is.Null, "no tooltip was shown");
		}

		[UnityTest]
		public IEnumerator ATooltipStep_WithNoCloseTime_LastsUntilThePlayerClosesTheTooltip()
		{
			_ui.AddFragmentPrefab<UIViewTooltip>();
			_host = _ui.System.Show<Host>();

			UITargetId id = MakeTargetId();
			_targets.Register(id, MakeTarget());

			var step = MakeStep<TooltipStep>();
			step.TargetId = id;
			step.CloseTime = 0f;

			UniTask present = step.PresentAsync(Context, CancellationToken.None);
			yield return WaitUntil(() => _ui.System.GetView<UIViewTooltip>() != null);
			UIViewTooltip tooltip = _ui.System.GetView<UIViewTooltip>();
			Assert.That(tooltip, Is.Not.Null, "the tooltip shows at its target");

			float until = Time.realtimeSinceStartup + 0.5f;
			yield return WaitUntil(() => Time.realtimeSinceStartup >= until);

			Assert.That(present.Status, Is.EqualTo(UniTaskStatus.Pending), "the step waits for the player");
			Assert.That(tooltip.State, Is.EqualTo(ViewState.Shown), "and the tooltip stays up");

			tooltip.Close(); // what its tap-anywhere button does
			yield return WaitUntil(() => present.Status.IsCompleted());

			Assert.That(present.Status, Is.EqualTo(UniTaskStatus.Succeeded), "closing the tooltip ends the step");
			present.GetAwaiter().GetResult();
		}
	}
}
