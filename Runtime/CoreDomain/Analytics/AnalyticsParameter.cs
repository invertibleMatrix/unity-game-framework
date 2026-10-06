using System;
using System.Globalization;
using UnityEngine;

namespace AK.CoreDomain.Analytics
{
	/// <summary>
	/// Defines a parameter for an analytics event.
	/// </summary>
	[Serializable]
	public class AnalyticsParameter
	{
		[Tooltip("Parameter name (e.g., 'level_number', 'score', 'currency_amount').")]
		public ParameterName Name;

		[Tooltip("Optional string key. When set, this is used instead of ParameterName so games are not locked to the UGFW enum.")]
		public string Key;

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
		/// The default value as <typeparamref name="T"/>, which may be string, int, float or bool.
		/// Numbers parse with the invariant culture, so a definition reads the same on every device.
		/// Returns default(T) when there is no default, it does not parse, or T is another type.
		/// </summary>
		public T GetDefaultValue<T>()
		{
			if (string.IsNullOrEmpty(DefaultValue))
			{
				return default;
			}

			if (typeof(T) == typeof(string))
			{
				return (T)(object)DefaultValue;
			}
			if (typeof(T) == typeof(int))
			{
				return TryParseInt(DefaultValue, out int i) ? (T)(object)i : default;
			}
			if (typeof(T) == typeof(float))
			{
				return TryParseFloat(DefaultValue, out float f) ? (T)(object)f : default;
			}
			if (typeof(T) == typeof(bool))
			{
				return bool.TryParse(DefaultValue, out bool b) ? (T)(object)b : default;
			}

			return default;
		}

		/// <summary>
		/// The default value as <see cref="Type"/> says, or null when there is none. An Integer or
		/// Float default that does not parse gives 0 and a Boolean one false; every other type gives
		/// the text as written.
		/// </summary>
		public object GetDefaultValue()
		{
			if (string.IsNullOrEmpty(DefaultValue))
			{
				return null;
			}

			switch (Type)
			{
				case AnalyticsParameterType.Integer:
					return TryParseInt(DefaultValue, out int i) ? i : 0;
				case AnalyticsParameterType.Float:
					return TryParseFloat(DefaultValue, out float f) ? f : 0f;
				case AnalyticsParameterType.Boolean:
					return bool.TryParse(DefaultValue, out bool b) && b;
				default:
					return DefaultValue;
			}
		}

		// The styles int.Parse and float.Parse use by default, with the culture fixed.
		private static bool TryParseInt(string text, out int value) =>
			int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

		private static bool TryParseFloat(string text, out float value) =>
			float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
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