using System.Collections.Generic;
using AK.Core;
using GameplayCore.MetaData.Rewards;
using GameplayCore.MetaData.DailyRewards;
using GameplayCore.MetaData.Notifications;
using GameplayCore.MetaData.RemoteConfig;
using GameplayCore.MetaData.SpinWheel;
using GameplayCore.MetaData.Store;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace GameplayCore.MetaData
{
	public interface IMetaDataRepository
	{
		public UIDRegistry  UIDRegistry  { get; }
		public RewardsMeta  RewardsMeta  { get; }
		public CurrencyMeta CurrencyMeta { get; }
		public AudioIds     AudioIds     { get; }
		public ParticleIds  ParticleIds  { get; }

		public void InitializeRegistries();
		public T    GetObjectByUID<T>(UID uid) where T : ScriptableObject;
	}
}