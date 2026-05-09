using System;
using System.Collections.Generic;
using AK.Core;
using GameplayCore.MetaData.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayCore.MetaData.Progression
{
	/// <summary>
	/// Defines a milestone that players can reach during progression.
	/// </summary>
	[CreateAssetMenu(fileName = "MilestoneDefinition", menuName = "Gameplay/MetaData/Progression/MilestoneDefinition")]
	public class MilestoneDefinition : MetaDataAsset
	{
		[Header("Milestone Information")] [Tooltip("Unique identifier for this milestone.")]
		public string MilestoneID;

		[Tooltip("Display name shown in UI.")]
		public string DisplayName;

		[Tooltip("Description of this milestone.")] [TextArea(2, 4)]
		public string Description;

		[Tooltip("Icon displayed in UI.")]
		public Sprite Icon;

		[Header("Progression Requirements")] [Tooltip("Player level required to reach this milestone.")]
		public int RequiredLevel = 1;

		[Tooltip("Total XP required to reach this milestone.")]
		public long RequiredXP = 0;

		[Header("Rewards")] [Tooltip("Rewards granted when this milestone is reached.")] [InlineEditor]
		public RewardDefinition Reward;

		[Tooltip("Additional bonus rewards.")]
		public List<RewardDefinition> BonusRewards;

		[Header("Display")] [Tooltip("Display priority in UI (lower = higher priority).")] [Range(0, 100)]
		public int DisplayPriority = 0;

		[Tooltip("Is this milestone hidden until reached?")]
		public bool IsHidden = false;

		[Tooltip("Show notification when reached?")]
		public bool ShowNotification = true;

		[Header("Analytics")] [Tooltip("Custom analytics event name for tracking milestone completion.")]
		public string AnalyticsEventName;

		public UID UniqueID => UID;

		/// <summary>
		/// Gets all rewards from this milestone.
		/// </summary>
		public List<RewardDefinition> GetAllRewards()
		{
			List<RewardDefinition> rewards = new();

			if (Reward != null)
			{
				rewards.Add(Reward);
			}

			if (BonusRewards != null)
			{
				rewards.AddRange(BonusRewards);
			}

			return rewards;
		}

		/// <summary>
		/// Checks if this milestone is reached based on player level and XP.
		/// </summary>
		public bool IsReached(int playerLevel, long playerXP)
		{
			return playerLevel >= RequiredLevel && playerXP >= RequiredXP;
		}

		public UID UID => this;
	}
}