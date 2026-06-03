using AK.Core;
using UnityEngine;

namespace AK.Examples.Models
{
    /// <summary>
    /// ScriptableObject asset representing a transaction category.
    /// Games create their own instances (e.g., LevelComplete, GachaBox, PurchasableItem).
    /// </summary>
    [CreateAssetMenu(fileName = "TransactionType", menuName = "AK/Examples/MetaData/Models/TransactionType")]
    public class TransactionType : MetaDataAsset { }
}
