using System;
using AK.Kernel.Persistence;
using AK.Kernel.RemoteConfig;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// A remote JSON object, read with JsonUtility into a <typeparamref name="T"/>.
	///
	/// <para><b>Usage.</b>
	/// <list type="number">
	/// <item>A serializable class: <c>[Serializable] public class GameConfig { public int MaxLives; public float CoinMultiplier; }</c></item>
	/// <item>A concrete variable: <c>[CreateAssetMenu(...)] public class RemoteGameConfig : RemoteJson&lt;GameConfig&gt; { }</c></item>
	/// <item>The asset, with its key and default value set in the inspector.</item>
	/// <item>The provider's value, as a JSON object: <c>{"MaxLives":10,"CoinMultiplier":1.5}</c></item>
	/// </list></para>
	///
	/// <para><b>Reading.</b> The value must be one well-formed JSON object. Some providers
	/// deliver it inside a JSON string, <c>"{\"MaxLives\":10}"</c>; that string is decoded first
	/// (<see cref="RemoteValueText.UnwrapJson"/>). JsonUtility leaves members the JSON doesn't
	/// mention at their field initializers.</para>
	///
	/// <para><b>Sharing.</b> Without a remote value, <see cref="RemoteVariable{T}.Value"/> is a
	/// copy of the default, made once, so a caller can't change the asset. Every caller still
	/// gets the same object: treat it as read-only.</para>
	/// </summary>
	public abstract class RemoteJson<T> : RemoteVariable<T> where T : class, new()
	{
		private const int ExcerptLength = 64;

		[NonSerialized] private T _defaultCopy;

		/// <summary>The value as JSON.</summary>
		public string ToJson() => JsonUtility.ToJson(Value);

		protected override bool TryParseValue(string text, out T value)
		{
			value = null;

			string json = RemoteValueText.UnwrapJson(text, out bool repaired);
			if (repaired)
			{
				Debug.LogWarning($"Remote variable '{name}' ({VariableKey}) holds a JSON string that isn't valid JSON, most often for a quote " +
				                 $"that isn't escaped. It was decoded leniently; fix the value at the provider. It begins '{Excerpt(text)}'.", this);
			}

			// JsonUtility reads a malformed or non-object value as defaults without complaint.
			if (JsonEnvelope.Inspect(json, string.Empty, out _) != JsonShape.Object)
			{
				return false;
			}

			// Overwrite reads into the concrete runtime type, arrays and lists included.
			var parsed = new T();
			try
			{
				JsonUtility.FromJsonOverwrite(json, parsed);
			}
			catch (ArgumentException)
			{
				return false;
			}

			value = parsed;
			return true;
		}

		protected override string FormatValue(T value) => value != null ? JsonUtility.ToJson(value) : "{}";

		protected override T GetDefault() => _defaultCopy ??= Copy(_defaultValue);

		protected override void OnValidate()
		{
			base.OnValidate();
			_defaultCopy = null;
		}

		private static T Copy(T source)
		{
			var copy = new T();
			if (source != null)
			{
				JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);
			}

			return copy;
		}

		private static string Excerpt(string text) =>
			text.Length <= ExcerptLength ? text : text.Substring(0, ExcerptLength) + "…";
	}
}
