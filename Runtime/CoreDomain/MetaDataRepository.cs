using System.Collections.Generic;
using System.Linq;
using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AK.CoreDomain
{
	[CreateAssetMenu(fileName = "MetaDataRepository", menuName = "Gameplay/MetaDataRepository")]
	public class MetaDataRepository : ScriptableObject, IMetaDataRepository
	{
		[SerializeField] private UIDRegistry _uidRegistry;

		[InlineEditor(), SerializeField]
		private RewardsMeta _rewardsMeta;
		public CurrencyMeta _currencyMeta;

		// Type-keyed registry for extensible Meta lookup
		private readonly Dictionary<System.Type, IMeta> _metaRegistry = new();

		public UIDRegistry  UIDRegistry  => _uidRegistry;
		public RewardsMeta  RewardsMeta  => _rewardsMeta;
		public CurrencyMeta CurrencyMeta => _currencyMeta;

		private void OnEnable()
		{
			AutoRegisterCoreMetas();
		}

		/// <summary>
		/// Auto-registers the serialized core Meta assets into the type-keyed registry.
		/// </summary>
		private void AutoRegisterCoreMetas()
		{
			if (_rewardsMeta != null)  RegisterMeta(_rewardsMeta);
			if (_currencyMeta != null) RegisterMeta(_currencyMeta);
		}

		public void RegisterMeta<T>(T meta) where T : class, IMeta
		{
			if (meta == null) return;
			_metaRegistry[typeof(T)] = meta;
		}

		public T GetMeta<T>() where T : class, IMeta
		{
			return _metaRegistry.TryGetValue(typeof(T), out var meta) ? meta as T : null;
		}

		public bool TryGetMeta<T>(out T meta) where T : class, IMeta
		{
			if (_metaRegistry.TryGetValue(typeof(T), out var m))
			{
				meta = m as T;
				return meta != null;
			}

			meta = null;
			return false;
		}

		public void InitializeRegistries()
		{
			_uidRegistry.Initialize();

			foreach (var kvp in _metaRegistry)
			{
				kvp.Value.InitializeMeta();
			}
		}

		public T GetObjectByUID<T>(UID uid) where T : ScriptableObject
		{
			if (uid == null || uid.IsEmpty()) return null;

			return null;
		}

#if UNITY_EDITOR
		[Button]
		public void PerformDataRegistration()
		{
			_uidRegistry.RefreshAllUIDs();

			if (_rewardsMeta != null && _rewardsMeta.Registry != null)
				_rewardsMeta.Registry.RefreshAllObjects();
		}
#endif
	}
}
