using System;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// A remote config value of type <typeparamref name="T"/>: the remote value when one is
	/// known, otherwise the default set on the asset.
	///
	/// A subclass says how its type reads from text and is written as text; the two must
	/// round-trip, since a value set by code is cached as text too.
	/// </summary>
	/// <typeparam name="T">The value's type.</typeparam>
	public abstract class RemoteVariable<T> : RemoteVariableBase
	{
		[Tooltip("The value when no remote value is known.")]
		[SerializeField] protected T _defaultValue;

		[NonSerialized] private T _remoteValue;

		/// <summary>The value when no remote value is known.</summary>
		public T DefaultValue => GetDefault();

		/// <summary>The remote value when one is known, otherwise <see cref="DefaultValue"/>.</summary>
		public T Value => HasRemoteValue ? _remoteValue : GetDefault();

		public override Type ValueType => typeof(T);

		public override string GetDefaultValueText() => FormatValue(_defaultValue);

		/// <summary>
		/// Sets the remote value as if it had been fetched: for tests, debug tools and providers
		/// that deliver typed values. It stays until the next fetch, and isn't cached until then.
		/// A value written as empty text, such as an empty string, is no value, as when fetched:
		/// the variable then reads as its default.
		/// </summary>
		public void SetRemoteValue(T value)
		{
			string text = FormatValue(value);
			if (string.IsNullOrEmpty(text))
			{
				ClearRemoteValue();
				return;
			}

			_remoteValue = value;
			MarkRemote(text, RemoteValueOrigin.Fetched);
		}

		/// <summary>Reads <paramref name="text"/> as a <typeparamref name="T"/>. False when it isn't one.</summary>
		protected abstract bool TryParseValue(string text, out T value);

		/// <summary>Writes <paramref name="value"/> as text that <see cref="TryParseValue"/> reads back as the same value.</summary>
		protected abstract string FormatValue(T value);

		/// <summary>The value when no remote value is known: the asset's default, unless a subclass protects it.</summary>
		protected virtual T GetDefault() => _defaultValue;

		protected sealed override bool TryAcceptText(string text)
		{
			if (!TryParseValue(text, out T value))
			{
				return false;
			}

			_remoteValue = value;
			return true;
		}

		protected sealed override void OnRemoteValueCleared() => _remoteValue = default;

		/// <summary>The variable's value; a missing variable reads as <c>default</c>.</summary>
		public static implicit operator T(RemoteVariable<T> variable) => variable != null ? variable.Value : default;
	}
}
