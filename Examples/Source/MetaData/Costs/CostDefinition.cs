using System.Collections.Generic;
using AK.Core;
using UnityEngine;

namespace AK.Examples.Costs
{
    [CreateAssetMenu(fileName = "CostDefinition", menuName = "AK/MetaData/CostDefinition")]
    public class CostDefinition : MetaDataAsset
    {
        [Tooltip("A list of ways this item can be paid for.")]
        public List<CostOption> CostOptions;
    }
}