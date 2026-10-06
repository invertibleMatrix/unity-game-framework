namespace AK.Core
{
	/// <summary>
	/// A service whose saved data belongs to one account at a time.
	///
	/// Until an account is bound, the service uses the device's data, saved under its bare key.
	/// Binding an account switches to that account's data, under <c>{key}@{account}</c>, so
	/// people sharing a device keep separate progress. When the account bound has no data of its
	/// own and the device has some, the device's data moves to the account. That carries
	/// progress saved before data was per account over to the first account to sign in.
	/// </summary>
	public interface IAccountScoped
	{
		/// <summary>The bound account; null while the service uses the device's data.</summary>
		string AccountId { get; }

		/// <summary>
		/// Switches to <paramref name="accountId"/>'s data, or back to the device's for null or
		/// empty. Binding the account already bound does nothing. Main thread only.
		/// </summary>
		/// <exception cref="System.ArgumentException"><paramref name="accountId"/> has a control character.</exception>
		/// <exception cref="System.InvalidOperationException">A write batch is open.</exception>
		void BindAccount(string accountId);
	}
}
