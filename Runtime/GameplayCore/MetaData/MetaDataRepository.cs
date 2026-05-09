using System.Collections.Generic;
using System.Linq;
using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameplayCore.MetaData
{
	[CreateAssetMenu(fileName = "MetaDataRepository", menuName = "Gameplay/MetaDataRepository")]
	public class MetaDataRepository : ScriptableObject, IMetaDataRepository
	{
		[SerializeField] private UIDRegistry                    _uidRegistry;

		[InlineEditor(), SerializeField]
		private RewardsMeta _rewardsMeta;

		public CurrencyMeta _currencyMeta;
		
			                    [InlineEditor(), SerializeField]
		private AudioIds _audioIds;

		[InlineEditor(), SerializeField]
		private ParticleIds _particleIds;


		public UIDRegistry  UIDRegistry  => _uidRegistry;
		public RewardsMeta  RewardsMeta  => _rewardsMeta;
		public CurrencyMeta CurrencyMeta => _currencyMeta;
		public AudioIds     AudioIds     => _audioIds;
		public ParticleIds  ParticleIds  => _particleIds;

		
		

		public void InitializeRegistries()
		{
			_uidRegistry.Initialize();
			_rewardsMeta.InitializeMeta();
		}

		public T GetObjectByUID<T>(UID uid) where T : ScriptableObject
		{
			if (uid == null || uid.IsEmpty()) return null;

			// Add other types as needed

			return null;
		}

#if UNITY_EDITOR
		[Button]
		public void PerformDataRegistration()
		{
			_uidRegistry.RefreshAllUIDs();
			_rewardsMeta.Registry.RefreshAllObjects();
			// EditorUtility.SetDirty(_levelDefinitions);
		}
#endif
	}
}