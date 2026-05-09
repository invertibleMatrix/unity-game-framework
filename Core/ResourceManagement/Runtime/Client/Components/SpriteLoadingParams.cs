using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Core.ResourceManagement
{
	/// <summary>
	/// Represents parameters for loading sprites, including format string and primary key.
	/// </summary>
	[System.Serializable]
	public struct SpriteLoadingParams
	{
		/// <summary>
		/// The format string containing placeholders for prefix, primary key, and postfix.
		/// </summary>
		[Title(":- Set Up Format String To Process PrimaryKey, i.e: prefix_{0}_postfix, atlas_name[{0}], etc...")]
		[SerializeField] private string _formatString;

		/// <summary>
		/// The primary key used for generating the sprite key.
		/// </summary>
		[SerializeField, BoxGroup] private string _primaryKey;

		/// <summary>
		/// Gets the generated sprite key using the format string and primary key.
		/// </summary>
		[ShowInInspector] public string Key => GenerateKey(_primaryKey);

		/// <summary>
		/// <see cref="HasPrimaryKey"/> Returns Whether The Primary Key Is Authored Or Not...
		/// </summary>
		public bool HasPrimaryKey() => string.IsNullOrEmpty(_primaryKey) == false;

		/// <summary>
		/// Sets the format string used to generate the sprite key.
		/// </summary>
		/// <param name="formatString">The format string with placeholders.</param>
		public void SetFormatString(string formatString) => _formatString = formatString;

		/// <summary>
		/// Sets the primary key used for generating the sprite key.
		/// </summary>
		/// <param name="primaryKey">The primary key value.</param>
		public void SetPrimaryKey(string primaryKey)
		{
			if (string.IsNullOrEmpty(primaryKey) == false) _primaryKey = primaryKey;
		}

		/// <summary>
		/// Generates the sprite key using the format string and primary key.
		/// -: If The Key Sent Is Empty Or Null, Params Key Is Going To Be Used Instead...
		/// </summary>
		/// <returns>The generated sprite key.</returns>
		public string GenerateKey(string primaryKey)
		{
			if (string.IsNullOrEmpty(primaryKey)) primaryKey = _primaryKey;
			if (string.IsNullOrEmpty(_formatString)) _formatString = "{0}";

			return string.Format(_formatString, primaryKey);
		}

		/// <summary>
		/// Creates a new instance of <see cref="SpriteLoadingParams"/> from a sprite key.
		/// </summary>
		/// <param name="key">The sprite key.</param>
		/// <param name="format">string format to generate the key</param>
		/// <returns>A new instance of <see cref="SpriteLoadingParams"/>.</returns>
		public static SpriteLoadingParams FromKey(string key, string format = "{0}")
		{
			return new SpriteLoadingParams()
			{
				_formatString = format,
				_primaryKey = key
			};
		}

		/// <summary>
		/// Default <see cref="SpriteLoadingParams"/> instance with a placeholder key ("N/A").
		/// </summary>
		public static SpriteLoadingParams Default = FromKey("N/A");

		private Color GetTint() => Color.yellow;
	}
}