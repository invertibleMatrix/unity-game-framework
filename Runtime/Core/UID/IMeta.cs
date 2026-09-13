namespace AK.Core
{
	/// <summary>
	/// A Meta container registered in the metadata repository and looked up by type.
	/// </summary>
	public interface IMeta
	{
		/// <summary>Build internal caches. Called once by MetaDataRepository.InitializeRegistries().</summary>
		void InitializeMeta();
	}

	/// <summary>
	/// A Meta that owns a domain registry. The repository registers it for aggregate
	/// identity resolution automatically — no separate list to maintain.
	/// </summary>
	public interface IMetaWithRegistry : IMeta
	{
		UidRegistryAssetBase RegistryAsset { get; }
	}
}
