using UnityEngine;

namespace GameplayCore.MetaData.RemoteConfig
{
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
		/// <summary>
		/// Sets the remote value from a JSON string.
		/// Called by the remote config service after fetching from Firebase.
		/// </summary>
		public void SetRemoteValueFromJson(string json)
		{
			if (string.IsNullOrEmpty(json))
			{
				Debug.LogWarning($"RemoteJson '{name}': Attempted to set null or empty JSON value.");
				return;
			}

			try
			{
				_remoteValue = JsonUtility.FromJson<T>(json);
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
		/// Gets the current value as a JSON string.
		/// Useful for debugging or logging.
		/// </summary>
		public string ToJson()
		{
			return JsonUtility.ToJson(Value);
		}
	}
}