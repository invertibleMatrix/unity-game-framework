using System.Text;

namespace AK.Services.Analytics
{
	/// <summary>
	/// Shared name-conversion helpers for analytics providers.
	/// Single implementation so Firebase/GameAnalytics/etc. never diverge.
	/// </summary>
	public static class AnalyticsNameUtility
	{
		/// <summary>
		/// Converts PascalCase/camelCase to snake_case.
		/// "ItemId" -> "item_id", "LevelNumber" -> "level_number", "URLValue" -> "url_value".
		/// </summary>
		public static string ToSnakeCase(string input)
		{
			if (string.IsNullOrEmpty(input))
			{
				return input;
			}

			var sb = new StringBuilder(input.Length + 8);

			for (var i = 0; i < input.Length; i++)
			{
				var c = input[i];

				if (char.IsUpper(c))
				{
					// Insert '_' on lower->upper boundaries and at the end of an acronym run
					// ("URLValue" -> "URL_Value").
					var prevIsLowerOrDigit = i > 0 && (char.IsLower(input[i - 1]) || char.IsDigit(input[i - 1]));
					var nextIsLower = i + 1 < input.Length && char.IsLower(input[i + 1]);

					if (i > 0 && (prevIsLowerOrDigit || nextIsLower))
					{
						sb.Append('_');
					}

					sb.Append(char.ToLowerInvariant(c));
				}
				else
				{
					sb.Append(char.ToLowerInvariant(c));
				}
			}

			return sb.ToString();
		}
	}
}
