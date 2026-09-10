using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Analytics
{
	/// <summary>
	/// Well-known analytics events the framework itself raises. Typed as the definition so
	/// the picker filters to analytics events and the reference cannot dangle.
	/// </summary>
	[CreateAssetMenu(fileName = "AnalyticsEventIds", menuName = "AK/MetaData/Analytics/AnalyticsEventIds")]
	public class AnalyticsEventIds : MetaDataAsset
	{
		public AnalyticsEventDefinition SessionStart;
		public AnalyticsEventDefinition SessionEnd;
		public AnalyticsEventDefinition LevelStarted;
		public AnalyticsEventDefinition LevelFailed;
		public AnalyticsEventDefinition LevelCompleted;
		public AnalyticsEventDefinition BoosterUsed;
		public AnalyticsEventDefinition PowerupUsed;
		public AnalyticsEventDefinition GachaBoxOpened;
		public AnalyticsEventDefinition DailyRewardClaimed;
		public AnalyticsEventDefinition WheelSpun;
		public AnalyticsEventDefinition NotificationPermissionYes;
		public AnalyticsEventDefinition NotificationPermissionNo;
		public AnalyticsEventDefinition RatingRequestYes;
		public AnalyticsEventDefinition RatingRequestNo;
		public AnalyticsEventDefinition IAP;
	}
}
