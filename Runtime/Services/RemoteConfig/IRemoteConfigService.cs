using Cysharp.Threading.Tasks;
using AK.CoreDomain.RemoteConfig;

namespace AK.Services
{
	/// <summary>
	/// Interface for Remote Config services.
	/// Implementations can wrap different providers (Firebase, custom backend, etc.)
	/// </summary>
	public interface IRemoteConfigService
	{
		/// <summary>
		/// Whether the service has been initialized.
		/// </summary>
		bool IsInitialized { get; }

		/// <summary>
		/// Initializes the remote config service.
		/// - Sets default values from RemoteConfigMeta
		/// - Fetches values from the remote server
		/// - Applies fetched values to RemoteVariables
		/// </summary>
		UniTask InitializeAsync();

		/// <summary>
		/// Fetches the latest values from the remote server.
		/// Call this to refresh values without full re-initialization.
		/// </summary>
		UniTask FetchAsync();

		/// <summary>
		/// Activates the most recently fetched values.
		/// Called automatically during InitializeAsync, but can be called
		/// separately if you want to control when values are applied.
		/// </summary>
		UniTask ActivateAsync();

		/// <summary>
		/// Fetches and activates values in one call.
		/// </summary>
		UniTask FetchAndActivateAsync();
	}
}