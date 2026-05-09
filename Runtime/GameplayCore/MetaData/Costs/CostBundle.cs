using System.Collections.Generic;
using UnityEngine;

namespace GameplayCore.MetaData.Costs
{
    [System.Serializable]
    public class CostBundle
    {
        [Tooltip("A list of costs that make up this bundle.")]
        public List<CostDefinition> Costs;
    }
}