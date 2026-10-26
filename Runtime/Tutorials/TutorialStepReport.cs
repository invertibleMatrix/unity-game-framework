namespace AK.Tutorials
{
	/// <summary>What happened to a tutorial step, as <see cref="TutorialProvider.StepReported"/> tells it.</summary>
	public enum TutorialStepAction
	{
		/// <summary>The step began presenting (after its conditions were met and its target was present).</summary>
		Started,

		/// <summary>The step finished and its progress was recorded.</summary>
		Done,

		/// <summary>The step's surface never appeared; a later checkpoint presents it again.</summary>
		Declined,

		/// <summary>The run was abandoned mid-step; a later run presents it again.</summary>
		Abandoned,
	}

	/// <summary>One step's report from a tutorial run, for analytics and debugging.</summary>
	public readonly struct TutorialStepReport
	{
		public readonly TutorialProvider Tutorial;
		/// <summary>The step's index in <see cref="TutorialProvider.Steps"/>.</summary>
		public readonly int StepIndex;
		public readonly TutorialStep Step;
		public readonly TutorialStepAction Action;

		public TutorialStepReport(TutorialProvider tutorial, int stepIndex, TutorialStep step, TutorialStepAction action)
		{
			Tutorial = tutorial;
			StepIndex = stepIndex;
			Step = step;
			Action = action;
		}

		public int StepCount => Tutorial != null ? Tutorial.Steps.Count : 0;

		/// <summary>The run's last step is done: the tutorial is complete.</summary>
		public bool CompletesTutorial => Action == TutorialStepAction.Done && StepIndex == StepCount - 1;
	}
}
