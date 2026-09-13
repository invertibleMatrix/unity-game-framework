using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	[System.Serializable]
	internal sealed class RemoteJsonQuotedString
	{
		public string value;
	}

	/// <summary>
	/// Base class for complex/serializable type remote variables.
	/// Firebase returns these as JSON strings which are deserialized via JsonUtility.
	///
	/// Usage:
	/// 1. Create a [Serializable] class:
	///    [Serializable] public class GameConfig { public int MaxLives; public float CoinMultiplier; }
	///
	/// 2. Create a concrete wrapper:
	///    [CreateAssetMenu(fileName = "RemoteGameConfig_", menuName = "Gameplay/MetaData/RemoteConfig/Remote GameConfig")]
	///    public class RemoteGameConfig : RemoteJson<GameConfig> { }
	///
	/// 3. Create the asset in Unity Editor and set the default value
	///
	/// 4. In Firebase Console, set the value as JSON:
	///    {"MaxLives":10,"CoinMultiplier":1.5}
	/// </summary>
	public abstract class RemoteJson<T> : RemoteVariable<T> where T : class, new()
	{
		public override void SetRemoteValueFromString(string value)
		{
			SetRemoteValueFromJson(value);
		}

		public void SetRemoteValueFromJson(string json)
		{
			json = UnwrapFirebaseJson(json);
			if (string.IsNullOrEmpty(json))
			{
				Debug.LogWarning($"RemoteJson '{name}': Attempted to set null or empty JSON value.");
				return;
			}

			try
			{
				// FromJson<T>() with T as a generic parameter drops arrays/lists
				// (ads_config is nested objects so it still worked). Overwrite uses
				// the concrete runtime type, matching AgeUp / district catalogs.
				T parsed = new T();
				JsonUtility.FromJsonOverwrite(json, parsed);
				_remoteValue = parsed;
				_hasRemoteValue = true;

				if (_cacheValue)
				{
					SaveCachedValue();
				}
			}
			catch (System.Exception e)
			{
				Debug.LogError($"RemoteJson '{name}': Failed to parse JSON '{json}'. Error: {e.Message}");
			}
		}

		/// <summary>
		/// Firebase JSON-typed params are sometimes a quoted JSON string.
		/// The game server already double-parses this; Unity must too.
		/// </summary>
		internal static string UnwrapFirebaseJson(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return value;
			}

			string trimmed = value.Trim();
			if (trimmed.Length > 0 && trimmed[0] == '\uFEFF')
			{
				trimmed = trimmed.Substring(1).TrimStart();
			}

			// Not a quoted JSON string — already a bare object/array/primitive.
			if (trimmed.Length < 2 || trimmed[0] != '"' || trimmed[trimmed.Length - 1] != '"')
			{
				return trimmed;
			}

			// Strict path: re-wrap and let JsonUtility decode escapes for us.
			RemoteJsonQuotedString box = null;
			try
			{
				box = JsonUtility.FromJson<RemoteJsonQuotedString>("{\"value\":" + trimmed + "}");
			}
			catch (System.Exception)
			{
				// fall through to the tolerant path below
			}

			if (box != null && !string.IsNullOrEmpty(box.value))
			{
				return box.value;
			}

			// Tolerant fallback: the strict box parse failed (a stray unescaped
			// quote in the payload breaks the re-wrapped object). Strip the outer
			// quotes and unescape the common sequences by hand so we still recover,
			// and log a warning that names the unwrap step — otherwise the caller's
			// FromJsonOverwrite fails against a double-wrapped string with no clue.
			string inner = trimmed.Substring(1, trimmed.Length - 2)
				.Replace("\\\"", "\"")
				.Replace("\\\\", "\\")
				.Replace("\\/", "/")
				.Replace("\\n", "\n")
				.Replace("\\r", "\r")
				.Replace("\\t", "\t");

			Debug.LogWarning($"RemoteJson: strict quoted-JSON parse failed; used tolerant unwrap. Value begins '{trimmed.Substring(0, System.Math.Min(48, trimmed.Length))}…'");
			return inner;
		}

		/// <summary>
		/// Gets the current value as a JSON string.
		/// Useful for debugging or logging.
		/// </summary>
		public string ToJson()
		{
			return JsonUtility.ToJson(Value);
		}
	}
}
