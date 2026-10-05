using System.Threading;
using Cysharp.Threading.Tasks;

namespace AK.Services
{
	/// <summary>
	/// Fetches remote config from a provider, such as Firebase or a game server, and applies
	/// it to the variables of a <see cref="AK.CoreDomain.RemoteConfig.RemoteConfigMeta"/>.
	///
	/// A provider that can't be reached never fails a call: the variables keep their cached or
	/// default values, and the failure is logged. Cancelling a call stops the caller waiting;
	/// the values already applied stay.
	/// </summary>
	public interface IRemoteConfigService
	{
		/// <summary>
		/// Whether <see cref="InitializeAsync"/> has finished: the variables hold fetched values,
		/// or cached and default ones when the provider couldn't be reached. False while it
		/// runs, and after it was cancelled.
		/// </summary>
		bool IsInitialized { get; }

		/// <summary>
		/// Loads the cached values, then fetches, activates and applies the provider's values.
		/// Once it has finished, later calls do nothing; a cancelled run can be started again.
		/// </summary>
		UniTask InitializeAsync(CancellationToken cancellationToken = default);

		/// <summary>Fetches the provider's latest values, without applying them.</summary>
		UniTask FetchAsync(CancellationToken cancellationToken = default);

		/// <summary>Activates the values fetched last and applies them to the variables.</summary>
		UniTask ActivateAsync(CancellationToken cancellationToken = default);

		/// <summary>Fetches, then activates and applies, in one call.</summary>
		UniTask FetchAndActivateAsync(CancellationToken cancellationToken = default);
	}
}
