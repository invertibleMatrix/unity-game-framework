using UnityEngine;

namespace AK.Core
{
	public abstract class MetaDataAsset : UID
	{
		public string Name;

		[Tooltip("Display name shown in UI.")]
		public string DisplayName;

		[Tooltip("Description of this currency.")] [TextArea(2, 4)]
		public string Description;

		[Tooltip("Icon displayed in UI.")]
		public Sprite Icon;

		public virtual void InitializeMeta() { }
	}
}