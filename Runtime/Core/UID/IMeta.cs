namespace AK.Core
{
	/// <summary>
	/// Common interface for all Meta containers registered in MetaDataRepository.
	/// Enables type-keyed lookup via GetMeta<T>() without hardcoding
	/// every domain as a field on the repository.
	/// </summary>
	public interface IMeta
	{
		/// <summary>
		/// Initialize the meta's internal registries. Called by MetaDataRepository
		/// during InitializeRegistries().
		/// </summary>
		void InitializeMeta();
	}
}
