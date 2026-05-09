using System;
using UnityEngine;

namespace GameplayCore.MetaData.Analytics
{
	/// <summary>
	/// Defines a parameter for an analytics event.
	/// </summary>
	[Serializable]
	public class AnalyticsParameter
	{
		[Tooltip("Parameter name (e.g., 'level_number', 'score', 'currency_amount').")]
		public ParameterName Name;

		[Tooltip("Parameter type.")]
		public AnalyticsParameterType Type;

		[Tooltip("Is this parameter required?")]
		public bool IsRequired = true;

		[Tooltip("Default value if not provided.")]
		public string DefaultValue;

		[Tooltip("Description of this parameter.")]
		[TextArea(2, 3)]
		public string Description;

		/// <summary>
		/// Gets the default value as the specified type.
		/// </summary>
		public T GetDefaultValue<T>()
		{
			if (string.IsNullOrEmpty(DefaultValue))
			{
				return default;
			}

			try
			{
				if (typeof(T) == typeof(string))
				{
					return (T)(object)DefaultValue;
				}
				if (typeof(T) == typeof(int))
				{
					return (T)(object)int.Parse(DefaultValue);
				}
				if (typeof(T) == typeof(float))
				{
					return (T)(object)float.Parse(DefaultValue);
				}
				if (typeof(T) == typeof(bool))
				{
					return (T)(object)bool.Parse(DefaultValue);
				}
			}
			catch
			{
				return default;
			}

			return default;
		}
	}

	/// <summary>
	/// Defines the type of an analytics parameter.
	/// </summary>
	public enum AnalyticsParameterType
	{
		/// <summary>
		/// String value.
		/// </summary>
		String = 0,
		
		/// <summary>
		/// Integer value.
		/// </summary>
		Integer = 1,
		
		/// <summary>
		/// Float value.
		/// </summary>
		Float = 2,
		
		/// <summary>
		/// Boolean value.
		/// </summary>
		Boolean = 3,
		
		/// <summary>
		/// JSON object.
		/// </summary>
		JsonObject = 4
	}
}