namespace AK.CoreDomain.Analytics
{
	/// <summary>
	/// How an event should be mapped onto a provider's native taxonomy.
	/// Unspecified lets the provider infer (design, unless progression/ad fields are set).
	/// </summary>
	public enum AnalyticsEventKind
	{
		Unspecified = 0,
		Design = 1,
		Progression = 2,
		Resource = 3,
		Ad = 4,
		Business = 5,
		Error = 6
	}

	public enum AnalyticsProgressionStatus
	{
		Start = 1,
		Complete = 2,
		Fail = 3
	}

	public enum AnalyticsAdAction
	{
		Clicked = 1,
		Show = 2,
		FailedShow = 3,
		RewardReceived = 4,
		Request = 5,
		Loaded = 6
	}

	public enum AnalyticsResourceFlow
	{
		Source = 1,
		Sink = 2
	}

	public enum AnalyticsErrorSeverity
	{
		Debug = 1,
		Info = 2,
		Warning = 3,
		Error = 4,
		Critical = 5
	}
}
