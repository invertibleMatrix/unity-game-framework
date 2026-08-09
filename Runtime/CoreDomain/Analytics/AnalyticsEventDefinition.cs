using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
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

		[Header("Sampling")] [Tooltip("Sampling rate (0.0 to 1.0). 1.0 = track all events, 0.5 = track 50% of events.")] [Range(0f, 1f)]
		public float SamplingRate = 1f;

		[Tooltip("Is this event only for development builds?")]
		public bool DevOnly = false;

		[Header("Integration")] [Tooltip("Custom analytics provider event name (e.g., Firebase, GameAnalytics).")]
		public string ProviderEventName;

		[Tooltip("Additional provider-specific configuration.")] [TextArea(2, 4)]
		public string ProviderConfig;

		public UID UniqueID => this;

		/// <summary>
		/// Checks if this event should be tracked based on sampling rate.
		/// </summary>
		public bool ShouldTrack()
		{
			if (!IsActive) return false;
			if (SamplingRate >= 1f) return true;
			if (SamplingRate <= 0f) return false;
			return UnityEngine.Random.value < SamplingRate;
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