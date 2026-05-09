using System;
using System.Collections.Generic;
using GameplayCore.MetaData.Rewards;
using UnityEngine;

namespace GameplayCore.MetaData
{
	[Serializable]
	public class GachaBoxConfig
	{
		public Sprite            Icon;
		public List<GachaBundle> Bundles;
	}
}