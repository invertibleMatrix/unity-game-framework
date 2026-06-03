using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Analytics
{
	
	[CreateAssetMenu(fileName = "AnalyticsEventIds", menuName = "AK/MetaData/Analytics/AnalyticsEventIds")]
	public class AnalyticsEventIds : MetaDataAsset
	{
		public UID SessionStart;
		public UID SessionEnd;
		public UID LevelStarted;
		public UID LevelFailed;
		public UID LevelCompleted;
		public UID BoosterUsed;
		public UID PowerupUsed;
		public UID GachaBoxOpened;
		public UID DailyRewardClaimed;
		public UID WheelSpun;
		public UID NotificationPermissionYes;
		public UID NotificationPermissionNo;
		public UID RatingRequestYes;
		public UID RatingRequestNo;
		public UID IAP;
	}
}