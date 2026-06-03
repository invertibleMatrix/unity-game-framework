using System;
using AK.Core;
using AK.CoreDomain;
using UnityEngine;

namespace AK.Examples.Models
{
    /// <summary>
    /// A pending reward or purchase record with UID reference and timestamp.
    /// Resolves UIDs after deserialization via IMetaDataRepository.
    /// </summary>
    [Serializable]
    public class Transaction : ISerializationCallbackReceiver
    {
        public UID    UID;
        public string Time = PersistableState.GetFormattedTime(DateTime.UtcNow);

        [SerializeField] private string _uidID;
        [SerializeField] private string _uidName;

        public DateTime TimeDT => PersistableState.GetDateTimeFromString(Time);

        public void OnBeforeSerialize()
        {
            if (UID != null)
            {
                _uidID = UID.Id;
                _uidName = UID.name;
            }
        }

        public void OnAfterDeserialize()
        {
        }

        /// <summary>
        /// Resolves the UID reference after deserialization.
        /// Tries GUID lookup first, falls back to asset name.
        /// </summary>
        public void ResolveUID(IMetaDataRepository repository)
        {
            if (!string.IsNullOrEmpty(_uidID))
            {
                UID = repository.UIDRegistry.GetUID(_uidID);
                if (UID != null) return;
            }

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

            Debug.LogWarning($"Transaction UID could not be resolved. " +
                             $"GUID: '{_uidID}', Name: '{_uidName}'. The asset may have been deleted.");
        }
    }
}
