using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.CoreDomain.Currency
{
	/// <summary>
	/// Defines an exchange rate between two currencies.
	/// </summary>
	[Serializable]
	public class CurrencyExchangeRate
	{
		[Tooltip("The source currency to convert from.")]
		public CurrencyDefinition FromCurrency;

		[Tooltip("The target currency to convert to.")]
		public CurrencyDefinition ToCurrency;

		[Tooltip("Exchange rate (amount of ToCurrency per 1 FromCurrency).")]
		public float ExchangeRate = 1f;

		[Tooltip("Minimum amount that can be converted.")]
		public long MinAmount = 1;

		[Tooltip("Maximum amount that can be converted (0 = unlimited).")]
		public long MaxAmount = 0;

		[Tooltip("Is this exchange rate currently active?")]
		public bool IsActive = true;

		[Header("Time Limits")]
		[Tooltip("Is this a limited-time exchange rate?")]
		public bool IsLimitedTime;

		[Tooltip("Start time of availability (UTC).")]
		[ShowIf("IsLimitedTime")]
		public DateTime StartTime;

		[Tooltip("End time of availability (UTC).")]
		[ShowIf("IsLimitedTime")]
		public DateTime EndTime;

		/// <summary>
		/// Converts an amount from source currency to target currency.
		/// </summary>
		public long Convert(long fromAmount)
		{
			return (long)(fromAmount * ExchangeRate);
		}

		/// <summary>
		/// Checks if this exchange rate is currently available.
		/// </summary>
		public bool IsAvailable()
		{
			if (!IsActive) return false;

			if (IsLimitedTime)
			{
				DateTime now = DateTime.UtcNow;
				if (now < StartTime || now > EndTime)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Checks if the amount is within the conversion limits.
		/// </summary>
		public bool IsWithinLimits(long amount)
		{
			if (amount < MinAmount) return false;
			if (MaxAmount > 0 && amount > MaxAmount) return false;
			return true;
		}
	}
}