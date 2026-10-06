using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// A definition: an identity plus the display metadata every piece of game content
	/// shares. Domain definitions (currencies, rewards, ad placements...) extend this.
	/// A Meta that the repository initializes implements <see cref="IMeta"/>.
	/// </summary>
	public abstract class MetaDataAsset : UID
	{
		public string Name;

		[Tooltip("Display name shown in UI.")]
		public string DisplayName;

		[Tooltip("Description shown in UI.")] [TextArea(2, 4)]
		public string Description;

		[Tooltip("Icon displayed in UI.")]
		public Sprite Icon;
	}
}
