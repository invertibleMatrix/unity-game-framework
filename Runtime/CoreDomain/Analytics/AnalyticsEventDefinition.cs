using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.Kernel.Analytics;
using UnityEngine;

namespace AK.CoreDomain.Analytics
{
	/// <summary>
	/// Defines an analytics event that can be tracked.
	/// </summary>
	[CreateAssetMenu(fileName = "AnalyticsEventDefinition", menuName = "AK/MetaData/Analytics/AnalyticsEventDefinition")]
	public class AnalyticsEventDefinition : MetaDataAsset
	{
		[Header("Basic Information")] [Tooltip("Unique identifier for this event.")]
		public string EventID;

		[Header("Event Configuration")] [Tooltip("The category of this event.")]
		public AnalyticsEventCategory Category;

		[Tooltip("Priority for event processing (higher = processed first).")] [Range(0, 100)]
		public int Priority = 50;

		[Header("Parameters")] [Tooltip("Parameters for this event.")]
		public List<AnalyticsParameter> Parameters;

		[Header("Batching")] [Tooltip("Should this event be batched with other events?")]
		public bool ShouldBatch = false;

		[Tooltip("Maximum batch size (0 = no limit).")]
		public int MaxBatchSize = 10;

		[Tooltip("Batch timeout in seconds (0 = no timeout).")]
		public float BatchTimeoutSeconds = 5f;

		[Header("Conditions")] [Tooltip("Is this event currently active?")]
		public bool IsActive = true;

		[Tooltip("Minimum level required to track this event.")]
		public int MinLevelRequired = 1;

		[Tooltip("Maximum level after which this event won't be tracked (0 = no max).")]
		public int MaxLevelRequired = 0;

		[Header("Sampling")] [Tooltip("Share of users who send this event (0 to 1). Sampling keeps or drops whole users: a user is always in or always out, and everyone in a smaller sample is in every larger one.")] [Range(0f, 1f)]
		public float SamplingRate = 1f;

		[Tooltip("Is this event only for development builds?")]
		public bool DevOnly = false;

		[Header("Integration")] [Tooltip("Custom analytics provider event name (e.g., Firebase, GameAnalytics).")]
		public string ProviderEventName;

		[Tooltip("Native event kind. Unspecified lets the runtime infer.")]
		public AnalyticsEventKind Kind = AnalyticsEventKind.Unspecified;

		[Tooltip("GameAnalytics design event id (colon hierarchy, max 5 parts). Empty = EventID / ProviderEventName.")]
		public string DesignEventId;

		[Tooltip("Progression part 1 when Kind is Progression.")]
		public string Progression01;

		[Tooltip("Progression part 2 when Kind is Progression.")]
		public string Progression02;

		[Tooltip("Progression part 3 when Kind is Progression.")]
		public string Progression03;

		[Tooltip("If true, missing required parameters drop the event. Default is fail-open: warn and still send.")]
		public bool FailClosed = false;

		[Tooltip("Additional provider-specific configuration.")] [TextArea(2, 4)]
		public string ProviderConfig;


		/// <summary>
		/// True when this event is active and a user in <paramref name="userBucket"/> is in its
		/// sample. The bucket comes from the user's id (<see cref="UserSampling.Bucket"/>), so the
		/// same user gets the same answer on every session and device.
		/// </summary>
		public bool ShouldTrack(double userBucket)
		{
			return IsActive && UserSampling.Includes(SamplingRate, userBucket);
		}

		/// <summary>
		/// Checks if this event is available for the current level.
		/// </summary>
		public bool IsAvailable(int currentLevel = 1)
		{
			if (currentLevel < MinLevelRequired) return false;
			if (MaxLevelRequired > 0 && currentLevel > MaxLevelRequired) return false;
			return true;
		}

		/// <summary>
		/// Validates that all required parameters are present.
		/// </summary>
		public bool ValidateParameters(Dictionary<ParameterName, object> parameters)
		{
			if (Parameters == null) return true;

			foreach (var param in Parameters)
			{
				if (param.IsRequired && !parameters.ContainsKey(param.Name))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Gets all required parameters.
		/// </summary>
		public List<AnalyticsParameter> GetRequiredParameters()
		{
			if (Parameters == null) return new List<AnalyticsParameter>();
			return Parameters.Where(p => p.IsRequired).ToList();
		}

		/// <summary>
		/// Gets all optional parameters.
		/// </summary>
		public List<AnalyticsParameter> GetOptionalParameters()
		{
			if (Parameters == null) return new List<AnalyticsParameter>();
			return Parameters.Where(p => !p.IsRequired).ToList();
		}
	}
}