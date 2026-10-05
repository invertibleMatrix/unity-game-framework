using System;
using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// A value the game takes from remote config, with a default for when no remote value is
	/// known. The identity (inherited) is internal; the provider's key is <see cref="VariableKey"/>.
	///
	/// <para><b>Text.</b> Remote values travel as text: a provider delivers text, and the cache
	/// keeps it. Each type reads text one way (<see cref="AK.Kernel.RemoteConfig.RemoteValueText"/>),
	/// whichever source it came from. Text that isn't a value of the type, and empty text, are no
	/// value.</para>
	///
	/// <para><b>Who sets it.</b> <see cref="RemoteConfigMeta"/> applies cached and fetched values
	/// and keeps the cache; a variable only holds its value. A disabled variable is left at its
	/// default. Main thread only.</para>
	///
	/// <para><b>Which instance.</b> Values reach the instances in the meta's registry. A copy of
	/// the asset loaded from another bundle never gets one: hold the registry's instance, or
	/// resolve it by identity (<see cref="RemoteConfigMeta.GetVariable(AK.Core.Uid)"/>).</para>
	///
	/// <para>The remote value is held in memory only. In the editor it is cleared on entering Play
	/// mode, so with domain reload off a session doesn't start with the last one's values.</para>
	/// </summary>
	public abstract class RemoteVariableBase : MetaDataAsset
	{
		[Tooltip("The parameter's key in the remote config provider, such as Firebase.")]
		[SerializeField] protected string _variableKey;

		[Tooltip("Whether this variable takes values from remote config. A disabled variable always reads as its default.")]
		[SerializeField] protected bool _isEnabled = true;

		[Tooltip("Whether a fetched value is kept for later sessions, so it holds when the provider can't be reached.")]
		[SerializeField] protected bool _cacheValue = true;

		[NonSerialized] private string            _remoteText;
		[NonSerialized] private RemoteValueOrigin _origin;

		/// <summary>The parameter's key in the remote config provider.</summary>
		public string VariableKey => _variableKey;

		/// <summary>Whether the variable takes values from remote config.</summary>
		public bool IsEnabled => _isEnabled;

		/// <summary>Whether a fetched value is kept for later sessions.</summary>
		public bool CacheValue => _cacheValue;

		/// <summary>Where the value comes from.</summary>
		public RemoteValueOrigin Origin => _origin;

		/// <summary>Whether a remote value is known, fetched or cached. Without one, the variable reads as its default.</summary>
		public bool HasRemoteValue => _origin != RemoteValueOrigin.Default;

		/// <summary>The text the remote value was read from. Null without a remote value.</summary>
		public string RemoteText => _remoteText;

		/// <summary>The type of the variable's value.</summary>
		public abstract Type ValueType { get; }

		/// <summary>The default value as text, as a provider takes in-app defaults.</summary>
		public abstract string GetDefaultValueText();

		/// <summary>The value as text: the remote text, or the default's.</summary>
		public string GetValueText() => HasRemoteValue ? _remoteText : GetDefaultValueText();

		/// <summary>
		/// Takes <paramref name="text"/> as the remote value, from <paramref name="origin"/>.
		/// False, changing nothing, when the text is empty or isn't a value of the variable's type.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="origin"/> is <see cref="RemoteValueOrigin.Default"/>.</exception>
		public bool TrySetRemoteText(string text, RemoteValueOrigin origin)
		{
			if (origin == RemoteValueOrigin.Default)
			{
				throw new ArgumentOutOfRangeException(nameof(origin), origin, "A remote value is fetched or cached.");
			}

			if (string.IsNullOrEmpty(text) || !TryAcceptText(text))
			{
				return false;
			}

			_remoteText = text;
			_origin     = origin;
			return true;
		}

		/// <summary>Forgets the remote value. The variable reads as its default again.</summary>
		public void ClearRemoteValue()
		{
			_remoteText = null;
			_origin     = RemoteValueOrigin.Default;
			OnRemoteValueCleared();
		}

		/// <summary>Reads <paramref name="text"/> and holds the value. False, holding nothing new, when it isn't a value of the type.</summary>
		protected abstract bool TryAcceptText(string text);

		/// <summary>The remote value is gone: let go of it.</summary>
		protected abstract void OnRemoteValueCleared();

		/// <summary>Records a remote value the subclass already holds, with the text that reads back as it.</summary>
		protected void MarkRemote(string text, RemoteValueOrigin origin)
		{
			_remoteText = text;
			_origin     = origin;
		}

		protected virtual void OnValidate()
		{
			if (string.IsNullOrEmpty(_variableKey))
			{
				Debug.LogWarning($"Remote variable '{name}' has no VariableKey, so it never takes a remote value.", this);
			}
		}

#if UNITY_EDITOR
		[UnityEditor.InitializeOnEnterPlayMode]
		private static void ClearOnEnterPlayMode()
		{
			foreach (RemoteVariableBase variable in Resources.FindObjectsOfTypeAll<RemoteVariableBase>())
			{
				variable.ClearRemoteValue();
			}
		}
#endif
	}
}
