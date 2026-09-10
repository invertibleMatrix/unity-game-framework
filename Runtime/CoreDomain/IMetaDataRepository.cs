using AK.Core;

namespace AK.CoreDomain
{
	/// <summary>
	/// Type-keyed access to every Meta container plus untyped identity resolution across
	/// all of them. The resolver view is derived from the registries the metas own — there
	/// is no separate list to keep in sync.
	/// </summary>
	public interface IMetaDataRepository : IUidResolver
	{
		/// <summary>Redirects applied on every miss, in every registry this repository knows. May be null.</summary>
		UidRedirectTable Redirects { get; }

		/// <summary>
		/// Register a Meta container for type-keyed lookup via GetMeta<T>().
		/// Call during bootstrap (GameBindings) for all domains.
		/// </summary>
		void RegisterMeta<T>(T meta) where T : class, IMeta;

		/// <summary>Get a Meta container by type. Returns null if not registered.</summary>
		T GetMeta<T>() where T : class, IMeta;

		/// <summary>Try to get a Meta container by type. Returns false if not registered.</summary>
		bool TryGetMeta<T>(out T meta) where T : class, IMeta;

		/// <summary>
		/// Register a registry for aggregate resolution. Metas that own registries call this
		/// from InitializeMeta; standalone registries (audio, particles, cameras) can be
		/// registered directly from bindings.
		/// </summary>
		void RegisterRegistry(UidRegistryAssetBase registry);

		/// <summary>Initialize all registered Meta containers, then wire redirects into every registry.</summary>
		void InitializeRegistries();
	}
}
