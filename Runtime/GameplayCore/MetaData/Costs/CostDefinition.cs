using System.Collections.Generic;
using AK.Core;
using UnityEngine;

namespace GameplayCore.MetaData.Costs
{
    [CreateAssetMenu(fileName = "CostDefinition", menuName = "Gameplay/MetaData/CostDefinition")]
    public class CostDefinition : MetaDataAsset
    {
        [Tooltip("The name to display for this cost.")]
        public string Name;
        
        [Tooltip("A description of what this cost is for.")]
        [TextArea]
        public string Description;

        [Tooltip("The icon to display for this cost in the UI.")]
        public Sprite Icon;

        [Tooltip("A list of ways this item can be paid for.")]
        public List<CostOption> CostOptions;
    }
}