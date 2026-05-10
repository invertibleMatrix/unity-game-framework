using System;
using AK.Core;
using GameplayCore.MetaData;
using UnityEngine;

namespace GameplayCore.Models
{
	[Serializable]
	public class Transaction : ISerializationCallbackReceiver
	{
		public UID    UID;
		public string Time = GameModel.GetFormattedTime(DateTime.UtcNow);

		[SerializeField] private string _uidID;
		[SerializeField] private string _uidName; // Fallback: asset name for recovery if GUID changed

		public DateTime TimeDT => GameModel.GetDateTimeFromString(Time);

		public void OnBeforeSerialize()
		{
			if (UID != null)
			{
				_uidID = UID.Id;
				_uidName = UID.name; // Store asset name as fallback
			}
		}

		public void OnAfterDeserialize()
		{
		}

		public void ResolveUID(IMetaDataRepository repository)
		{
			// Try GUID lookup first
			if (!string.IsNullOrEmpty(_uidID))
			{
				UID = repository.UIDRegistry.GetUID(_uidID);
				if (UID != null) return;
			}

			// Fallback to asset name lookup
			if (!string.IsNullOrEmpty(_uidName))
			{
				UID = repository.UIDRegistry.GetUIDByName(_uidName);
				if (UID != null)
				{
					Debug.LogWarning($"Transaction UID resolved via name fallback: {_uidName}. " +
					                 $"Consider updating the saved GUID from '{_uidID}' to '{UID.Id}'.");
					return;
				}
			}

			// Both lookups failed - log warning but don't crash
			Debug.LogWarning($"Transaction UID could not be resolved. " +
			                 $"GUID: '{_uidID}', Name: '{_uidName}'. The asset may have been deleted.");
		}
	}

	public enum TransactionType
	{
		None,
		LevelCompleteTransaction,
		GachaBoxTransaction,
		PurchasableItem
	}
}