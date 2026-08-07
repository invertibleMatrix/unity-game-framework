using System;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// Bundle-safe UID link: serializes only the target's logical GUID and asset name
	/// as plain strings — never a hard object reference — so serialized data creates
	/// no asset-bundle dependencies. Resolution happens at runtime through the owning
	/// TypedUIDRegistryAsset (or UIDRegistry), which rebuilds lookups from live assets.
	/// Pair with UIDOfTypeAttribute to constrain the editor picker.
	/// </summary>
	[Serializable]
	public class UIDRef
	{
		[SerializeField, HideInInspector] private string _guid;
		[SerializeField, HideInInspector] private string _assetName;

		public string Guid      => _guid;
		public string AssetName => _assetName;

		public bool IsSet => !string.IsNullOrEmpty(_guid);

		public UIDRef() { }

		public UIDRef(UID asset)
		{
			Set(asset);
		}

		public void Set(UID asset)
		{
			_guid = asset != null ? asset.Id : string.Empty;
			_assetName = asset != null ? asset.name : string.Empty;
		}

		public static implicit operator string(UIDRef reference) => reference?._guid;
	}
}
