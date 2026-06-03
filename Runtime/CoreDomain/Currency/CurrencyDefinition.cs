using System;
using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Currency
{
	/// <summary>
	/// Defines a currency type in the game.
	/// </summary>
	[CreateAssetMenu(fileName = "CurrencyDefinition", menuName = "Gameplay/MetaData/Currency/CurrencyDefinition")]
	public class CurrencyDefinition : MetaDataAsset
	{
		[Header("Basic Information")] [Tooltip("Unique identifier for this currency.")]
		public string CurrencyID;

		[Tooltip("Short code for display (e.g., 'Coins', 'Gems').")]
		public string ShortCode;

		[Header("Currency Type")] [Tooltip("The type of currency.")]
		public CurrencyType Type;

		[Header("Limits")] [Tooltip("Maximum amount of this currency a player can have (0 = unlimited).")]
		public long MaxAmount = 0;

		[Tooltip("Starting amount for new players.")]
		public int StartingAmount = 0;

		[Tooltip("Daily bonus amount (0 = no daily bonus).")]
		public long DailyBonusAmount = 0;

		[Header("Conversion")] [Tooltip("Can this currency be converted to other currencies?")]
		public bool CanConvert = false;

		[Tooltip("Can this currency be purchased with real money?")]
		public bool CanPurchase = false;

		[Tooltip("Can this currency be earned through gameplay?")]
		public bool CanEarn = true;

		[Header("Display")] [Tooltip("Display format (e.g., '{0}', '{0} Coins', '{0:N0}').")]
		public string DisplayFormat = "{0}";

		[Tooltip("Show decimal places?")]
		public bool ShowDecimals = false;

		[Tooltip("Decimal places to show (if ShowDecimals is true).")] [Range(0, 4)]
		public int DecimalPlaces = 0;

		[Header("Analytics")] [Tooltip("Custom analytics event name for tracking currency changes.")]
		public string AnalyticsEventName;

		/// <summary>
		/// Formats the amount for display.
		/// </summary>
		public string FormatAmount(long amount)
		{
			if (ShowDecimals)
			{
				return string.Format($"{{0:N{DecimalPlaces}}}", amount);
			}

			return string.Format(DisplayFormat, amount);
		}

		/// <summary>
		/// Checks if the amount is within the maximum limit.
		/// </summary>
		public bool IsWithinLimit(long amount)
		{
			if (MaxAmount <= 0) return true;
			return amount <= MaxAmount;
		}

		/// <summary>
		/// Clamps the amount to the maximum limit.
		/// </summary>
		public long ClampAmount(long amount)
		{
			if (MaxAmount <= 0) return amount;
			return Math.Min(amount, MaxAmount);
		}

		public UID UniqueID => this;
	}
}