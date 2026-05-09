using System.Collections.Generic;
using System.Linq;
using AK.Core;
using GameplayCore.MetaData.Currency;
using UnityEngine;

namespace GameplayCore.MetaData
{
	/// <summary>
	/// Container for all currency definitions and exchange rates with powerful query methods.
	/// Similar to IAPMeta but for currencies.
	/// </summary>
	[CreateAssetMenu(fileName = "CurrencyMeta", menuName = "Gameplay/MetaData/Currency/CurrencyMeta")]
	public class CurrencyMeta : MetaDataAsset
	{
		public UID CoinID;
		public UID GemID;
		public UID GachaBoxCoinID;

		[Header("Currencies")] [SerializeField]
		private CurrencyRegistry _currencyRegistry;

		[Header("Exchange Rates")] [Tooltip("All currency exchange rates.")]
		public List<CurrencyExchangeRate> ExchangeRates;

		public CurrencyRegistry Registry => _currencyRegistry;

		public override void InitializeMeta()
		{
			_currencyRegistry.Initialize();
		}

		public IReadOnlyList<CurrencyDefinition> GetCurrencies()
		{
			return _currencyRegistry.GetAllObjects();
		}

		/// <summary>
		/// Gets a currency by its CurrencyID.
		/// </summary>
		public CurrencyDefinition GetCurrencyByID(UID currencyID)
		{
			return _currencyRegistry.GetObjectByUID(currencyID);
		}

		/// <summary>
		/// Gets all currencies of a specific type.
		/// </summary>
		public List<CurrencyDefinition> GetCurrenciesByType(CurrencyType type)
		{
			return _currencyRegistry.Registry.Objects.Where(c => c.Type == type).ToList();
		}

		/// <summary>
		/// Gets all soft currencies.
		/// </summary>
		public List<CurrencyDefinition> GetSoftCurrencies()
		{
			return _currencyRegistry.Registry.Objects.Where(c => c.Type == CurrencyType.Soft).ToList();
		}

		/// <summary>
		/// Gets all hard currencies.
		/// </summary>
		public List<CurrencyDefinition> GetHardCurrencies()
		{
			return _currencyRegistry.Registry.Objects.Where(c => c.Type == CurrencyType.Hard).ToList();
		}

		/// <summary>
		/// Gets all event currencies.
		/// </summary>
		public List<CurrencyDefinition> GetEventCurrencies()
		{
			return _currencyRegistry.Registry.Objects.Where(c => c.Type == CurrencyType.Event).ToList();
		}

		/// <summary>
		/// Gets all currencies that can be purchased.
		/// </summary>
		public List<CurrencyDefinition> GetPurchasableCurrencies()
		{
			return _currencyRegistry.Registry.Objects.Where(c => c.CanPurchase).ToList();
		}

		/// <summary>
		/// Gets all currencies that can be earned.
		/// </summary>
		public List<CurrencyDefinition> GetEarnableCurrencies()
		{
			return _currencyRegistry.Registry.Objects.Where(c => c.CanEarn).ToList();
		}

		/// <summary>
		/// Gets all currencies that can be converted.
		/// </summary>
		public List<CurrencyDefinition> GetConvertibleCurrencies()
		{
			return _currencyRegistry.Registry.Objects.Where(c => c.CanConvert).ToList();
		}

		/// <summary>
		/// Gets the exchange rate between two currencies.
		/// </summary>
		public CurrencyExchangeRate GetExchangeRate(CurrencyDefinition fromCurrency, CurrencyDefinition toCurrency)
		{
			if (fromCurrency == null || toCurrency == null)
			{
				return null;
			}

			return ExchangeRates.FirstOrDefault(e =>
				e.FromCurrency == fromCurrency &&
				e.ToCurrency == toCurrency &&
				e.IsAvailable());
		}

		/// <summary>
		/// Gets the exchange rate between two currencies by ID.
		/// </summary>
		public CurrencyExchangeRate GetExchangeRate(UID fromCurrencyID, UID toCurrencyID)
		{
			var fromCurrency = GetCurrencyByID(fromCurrencyID);
			var toCurrency = GetCurrencyByID(toCurrencyID);
			return GetExchangeRate(fromCurrency, toCurrency);
		}

		/// <summary>
		/// Gets all available exchange rates for a currency.
		/// </summary>
		public List<CurrencyExchangeRate> GetExchangeRatesForCurrency(CurrencyDefinition currency)
		{
			if (currency == null)
			{
				return new List<CurrencyExchangeRate>();
			}

			return ExchangeRates.Where(e =>
				e.FromCurrency == currency &&
				e.IsAvailable()).ToList();
		}

		/// <summary>
		/// Gets all available exchange rates for a currency by ID.
		/// </summary>
		public List<CurrencyExchangeRate> GetExchangeRatesForCurrency(UID currencyID)
		{
			var currency = GetCurrencyByID(currencyID);
			return GetExchangeRatesForCurrency(currency);
		}

		/// <summary>
		/// Gets all available exchange rates.
		/// </summary>
		public List<CurrencyExchangeRate> GetAvailableExchangeRates()
		{
			return ExchangeRates.Where(e => e.IsAvailable()).ToList();
		}

		/// <summary>
		/// Checks if a currency exists by CurrencyID.
		/// </summary>
		public bool HasCurrency(UID currencyID)
		{
			return _currencyRegistry.Registry.Objects.Any(c => c.CurrencyID == currencyID);
		}

		/// <summary>
		/// Checks if an exchange rate exists between two currencies.
		/// </summary>
		public bool HasExchangeRate(CurrencyDefinition fromCurrency, CurrencyDefinition toCurrency)
		{
			return GetExchangeRate(fromCurrency, toCurrency) != null;
		}

		/// <summary>
		/// Checks if an exchange rate exists between two currencies by ID.
		/// </summary>
		public bool HasExchangeRate(UID fromCurrencyID, UID toCurrencyID)
		{
			return GetExchangeRate(fromCurrencyID, toCurrencyID) != null;
		}

		/// <summary>
		/// Converts an amount from one currency to another.
		/// </summary>
		public long ConvertCurrency(long amount, CurrencyDefinition fromCurrency, CurrencyDefinition toCurrency)
		{
			var exchangeRate = GetExchangeRate(fromCurrency, toCurrency);
			if (exchangeRate == null)
			{
				return 0;
			}

			return exchangeRate.Convert(amount);
		}

		/// <summary>
		/// Converts an amount from one currency to another by ID.
		/// </summary>
		public long ConvertCurrency(long amount, UID fromCurrencyID, UID toCurrencyID)
		{
			var fromCurrency = GetCurrencyByID(fromCurrencyID);
			var toCurrency = GetCurrencyByID(toCurrencyID);
			return ConvertCurrency(amount, fromCurrency, toCurrency);
		}

		/// <summary>
		/// Gets all currencies that have daily bonuses.
		/// </summary>
		public List<CurrencyDefinition> GetCurrenciesWithDailyBonus()
		{
			return _currencyRegistry.Registry.Objects.Where(c => c.DailyBonusAmount > 0).ToList();
		}
	}
}