using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Rewards;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AK.CoreDomain
{
	public interface IMetaDataRepository
	{
		public UIDRegistry  UIDRegistry  { get; }

		// Typed convenience properties for framework-core domains
		public RewardsMeta  RewardsMeta  { get; }
		public CurrencyMeta CurrencyMeta { get; }

		/// <summary>
		/// Register a Meta container for type-keyed lookup via GetMeta<T>().
		/// Call during bootstrap (GameBindings) for game-specific domains.
		/// </summary>
		void RegisterMeta<T>(T meta) where T : class, IMeta;

		/// <summary>
		/// Get a Meta container by type. Returns null if not registered.
		/// </summary>
		T GetMeta<T>() where T : class, IMeta;

		/// <summary>
		/// Try to get a Meta container by type. Returns false if not registered.
		/// </summary>
		bool TryGetMeta<T>(out T meta) where T : class, IMeta;

		/// <summary>
		/// Initialize all registered Meta containers and the UID registry.
		/// </summary>
		void InitializeRegistries();

		public T GetObjectByUID<T>(UID uid) where T : ScriptableObject;
	}
}
