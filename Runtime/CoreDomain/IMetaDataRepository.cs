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
		public RewardsMeta  RewardsMeta  { get; }
		public CurrencyMeta CurrencyMeta { get; }
		public AudioIds     AudioIds     { get; }
		public ParticleIds  ParticleIds  { get; }

		public void InitializeRegistries();
		public T    GetObjectByUID<T>(UID uid) where T : ScriptableObject;
	}
}