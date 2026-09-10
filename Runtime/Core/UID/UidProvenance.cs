namespace AK.Core
{
	/// <summary>
	/// Where an asset's identity came from. Tooling uses this to decide what it is allowed
	/// to do: a Minted identity may be re-minted when the asset is a fresh duplicate; an
	/// Imported identity belongs to an external system and is never regenerated; a
	/// Derived identity is a function of its namespace and name and is recomputed, not minted.
	/// </summary>
	public enum UidProvenance : byte
	{
		Minted   = 0,
		Imported = 1,
		Derived  = 2,
	}
}
