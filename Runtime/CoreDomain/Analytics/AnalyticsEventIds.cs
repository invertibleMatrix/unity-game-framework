using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Analytics
{
	/// <summary>
	/// Typed references to common analytics events, so shared code can raise them without string
	/// ids. The framework raises none of them itself; a game assigns the ones it uses. Typed as the
	/// definition so the picker filters to analytics events and the reference cannot dangle.
	/// </summary>
	[CreateAssetMenu(fileName = "AnalyticsEventIds", menuName = "AK/MetaData/Analytics/AnalyticsEventIds")]
	public class AnalyticsEventIds : MetaDataAsset
	{
		public AnalyticsEventDefinition SessionStart;
		public AnalyticsEventDefinition SessionEnd;
		public AnalyticsEventDefinition LevelStarted;
		public AnalyticsEventDefinition LevelFailed;
		public AnalyticsEventDefinition LevelCompleted;
		public AnalyticsEventDefinition NotificationPermissionYes;
		public AnalyticsEventDefinition NotificationPermissionNo;
		public AnalyticsEventDefinition RatingRequestYes;
		public AnalyticsEventDefinition RatingRequestNo;
		public AnalyticsEventDefinition IAP;
	}
}
