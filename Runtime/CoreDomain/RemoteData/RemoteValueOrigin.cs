namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>Where a remote variable's value comes from.</summary>
	public enum RemoteValueOrigin : byte
	{
		/// <summary>No remote value is known: the variable reads as its default.</summary>
		Default = 0,

		/// <summary>Kept from an earlier session's fetch, until this session's fetch answers.</summary>
		Cached = 1,

		/// <summary>Fetched this session, or set by code.</summary>
		Fetched = 2,
	}
}
