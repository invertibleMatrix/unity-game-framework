using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.CoreDomain.Tutorial
{
	/// <summary>
	/// Defines a tutorial step or complete tutorial sequence.
	/// Tutorials can be shown at specific points in the game to teach players mechanics.
	/// </summary>
	[CreateAssetMenu(fileName = "TutorialDefinition", menuName = "Gameplay/MetaData/Tutorial/TutorialDefinition")]
	public class TutorialDefinition : MetaDataAsset
	{
		[Header("Identification")]
		[Tooltip("Display name for the tutorial")]
		public string DisplayName;
		
		[Tooltip("Internal name for reference")]
		public string InternalName;
		
		[Header("Classification")]
		[Tooltip("Type of tutorial for categorization")]
		public TutorialType Type;
		
		[Tooltip("Priority for showing tutorials (lower = higher priority)")]
		[Range(0, 100)]
		public int Priority = 50;
		
		[Header("Trigger Conditions")]
		[Tooltip("Minimum level required to show this tutorial")]
		[Min(1)]
		public int MinLevel = 1;
		
		[Tooltip("Maximum level after which this tutorial won't show")]
		[Min(1)]
		public int MaxLevel = 999;
		
		[Tooltip("Minimum player level required")]
		[Min(1)]
		public int MinPlayerLevel = 1;
		
		[Tooltip("Show only once per player")]
		public bool ShowOnce = true;
		
		[Tooltip("Can be skipped by the player")]
		public bool CanSkip = true;
		
		[Tooltip("Can be replayed from settings")]
		public bool CanReplay = false;
		
		[Header("Tutorial Content")]
		[Tooltip("Title text shown to the player")]
		[TextArea(1, 2)]
		public string Title;
		
		[Tooltip("Description text shown to the player")]
		[TextArea(3, 6)]
		public string Description;
		
		[Tooltip("Tutorial steps in sequence")]
		public List<TutorialStep> Steps = new();
		
		[Header("Rewards")]
		[Tooltip("Rewards granted upon completing the tutorial")]
		public List<RewardDefinition> CompletionRewards = new();
		
		[Header("Timing")]
		[Tooltip("Delay before showing tutorial (in seconds)")]
		[Min(0)]
		public float ShowDelay = 0f;
		
		[Tooltip("Minimum time between showing this tutorial again (in seconds)")]
		[Min(0)]
		public float CooldownTime = 0f;
		
		[Tooltip("Auto-advance to next step after this time (0 = manual)")]
		[Min(0)]
		public float AutoAdvanceTime = 0f;
		
		[Header("Visual Settings")]
		[Tooltip("Highlight specific UI elements")]
		public List<UID> HighlightElements = new();
		
		[Tooltip("Block gameplay while tutorial is active")]
		public bool BlockGameplay = true;
		
		[Tooltip("Show dim overlay behind tutorial")]
		public bool ShowOverlay = true;
		
		[Tooltip("Tutorial panel position")]
		public TutorialPosition Position = TutorialPosition.Center;
		
		[Header("Prerequisites")]
		[Tooltip("Tutorials that must be completed before this one")]
		public List<UID> PrerequisiteTutorials = new();
		
		[Tooltip("Achievements that must be unlocked")]
		public List<UID> RequiredAchievements = new();
		
		[Header("Analytics")]
		[Tooltip("Analytics event to track when tutorial starts")]
		public UID StartEvent;
		
		[Tooltip("Analytics event to track when tutorial completes")]
		public UID CompleteEvent;
		
		[Tooltip("Analytics event to track when tutorial is skipped")]
		public UID SkipEvent;
		
		public virtual UID UniqueID => this;
		
		/// <summary>
		/// Gets all rewards from this tutorial definition.
		/// </summary>
		public List<RewardDefinition> GetAllRewards()
		{
			List<RewardDefinition> allRewards = new List<RewardDefinition>();
			
			foreach (var reward in CompletionRewards)
			{
				if (reward != null)
				{
					allRewards.Add(reward);
				}
			}
			
			return allRewards;
		}
		
		/// <summary>
		/// Checks if this tutorial should be shown based on level.
		/// </summary>
		public bool ShouldShowForLevel(int level)
		{
			return level >= MinLevel && level <= MaxLevel;
		}
		
		/// <summary>
		/// Checks if this tutorial should be shown based on player level.
		/// </summary>
		public bool ShouldShowForPlayerLevel(int playerLevel)
		{
			return playerLevel >= MinPlayerLevel;
		}
	}
	
	/// <summary>
	/// Represents a single step in a tutorial sequence.
	/// </summary>
	[Serializable]
	public class TutorialStep
	{
		[Tooltip("Step number in sequence")]
		[Min(1)]
		public int StepNumber = 1;
		
		[Tooltip("Title for this step")]
		[TextArea(1, 2)]
		public string Title;
		
		[Tooltip("Description for this step")]
		[TextArea(2, 4)]
		public string Description;
		
		[Tooltip("Type of interaction required")]
		public TutorialInteractionType InteractionType = TutorialInteractionType.None;
		
		[Tooltip("Target element to interact with (if applicable)")]
		public UID TargetElement;
		
		[Tooltip("Highlight this element")]
		public bool HighlightTarget = true;
		
		[Tooltip("Show hand pointer animation")]
		public bool ShowPointer = true;
		
		[Tooltip("Auto-advance after this time (0 = wait for interaction)")]
		[Min(0)]
		public float AutoAdvanceTime = 0f;
		
		[Tooltip("Show continue button")]
		public bool ShowContinueButton = true;
		
		[Tooltip("Continue button text")]
		public string ContinueButtonText = "Continue";
		
		[Tooltip("Skip button text")]
		public string SkipButtonText = "Skip";
	}
	
	/// <summary>
	/// Defines the type of interaction required for a tutorial step.
	/// </summary>
	public enum TutorialInteractionType
	{
		/// <summary>
		/// No interaction required, just display information.
		/// </summary>
		None,
		
		/// <summary>
		/// Player must tap/click a specific element.
		/// </summary>
		Tap,
		
		/// <summary>
		/// Player must drag an element.
		/// </summary>
		Drag,
		
		/// <summary>
		/// Player must swipe in a direction.
		/// </summary>
		Swipe,
		
		/// <summary>
		/// Player must press and hold.
		/// </summary>
		Hold,
		
		/// <summary>
		/// Player must complete a specific action (e.g., shoot a bubble).
		/// </summary>
		Action,
		
		/// <summary>
		/// Player must navigate to a specific screen.
		/// </summary>
		Navigate,
		
		/// <summary>
		/// Player must purchase something.
		/// </summary>
		Purchase,
		
		/// <summary>
		/// Player must watch an ad.
		/// </summary>
		WatchAd,
		
		/// <summary>
		/// Custom interaction type.
		/// </summary>
		Custom
	}
	
	/// <summary>
	/// Defines the position of the tutorial panel on screen.
	/// </summary>
	public enum TutorialPosition
	{
		/// <summary>
		/// Center of the screen.
		/// </summary>
		Center,
		
		/// <summary>
		/// Top of the screen.
		/// </summary>
		Top,
		
		/// <summary>
		/// Bottom of the screen.
		/// </summary>
		Bottom,
		
		/// <summary>
		/// Left side of the screen.
		/// </summary>
		Left,
		
		/// <summary>
		/// Right side of the screen.
		/// </summary>
		Right,
		
		/// <summary>
		/// Top-left corner.
		/// </summary>
		TopLeft,
		
		/// <summary>
		/// Top-right corner.
		/// </summary>
		TopRight,
		
		/// <summary>
		/// Bottom-left corner.
		/// </summary>
		BottomLeft,
		
		/// <summary>
		/// Bottom-right corner.
		/// </summary>
		BottomRight
	}
}