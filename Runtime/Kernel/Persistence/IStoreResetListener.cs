namespace AK.Kernel.Persistence
{
	/// <summary>
	/// Holds state read from a <see cref="RecordStore"/> and drops it when that store deletes
	/// everything it owns (<see cref="RecordStore.DeleteAll"/>). Without that, the next save
	/// would write the deleted data back.
	/// </summary>
	public interface IStoreResetListener
	{
		/// <summary>The store's data is gone: go back to a fresh state. Called after the deletion is flushed.</summary>
		void OnStoreReset();
	}
}
