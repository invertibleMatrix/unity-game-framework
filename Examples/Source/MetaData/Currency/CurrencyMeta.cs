using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.Examples.Currency;
using UnityEngine;

namespace AK.CoreDomain
{
	/// <summary>
	/// Container for all currency definitions and exchange rates.
	/// </summary>
	[CreateAssetMenu(fileName = "CurrencyMeta", menuName = "AK/MetaData/Currency/CurrencyMeta")]
	public class CurrencyMeta : MetaDataAsset, IMetaWithRegistry
	{
		[Header("Currencies")] [SerializeField]
		private CurrencyRegistry _currencyRegistry;

		[Header("Exchange Rates")] [Tooltip("All currency exchange rates.")]
		public List<CurrencyExchangeRate> ExchangeRates = new();

		public CurrencyRegistry Registry => _currencyRegistry;

		public UidRegistryAssetBase RegistryAsset => _currencyRegistry;

		public override void InitializeMeta() { }

		public IReadOnlyList<CurrencyDefinition> GetCurrencies()
		{
			return _currencyRegistry != null ? _currencyRegistry.Objects : Array.Empty<CurrencyDefinition>();
		}

		public bool TryGetCurrency(Uid<CurrencyDefinition> id, out CurrencyDefinition currency)
		{
			currency = null;
			return _currencyRegistry != null && _currencyRegistry.TryResolve(id, out currency);
		}

		public CurrencyDefinition GetCurrency(Uid<CurrencyDefinition> id)
		{
			return TryGetCurrency(id, out var currency) ? currency : null;
		}

		public bool HasCurrency(Uid<CurrencyDefinition> id)
		{
			return _currencyRegistry != null && _currencyRegistry.Contains(id.Value);
		}

		public List<CurrencyDefinition> GetCurrenciesByType(CurrencyType type)
		{
			if (type == null) return new List<CurrencyDefinition>();
			return GetCurrencies().Where(c => c.Type == type).ToList();
		}

		public List<CurrencyDefinition> GetPurchasableCurrencies()
		{
			return GetCurrencies().Where(c => c.CanPurchase).ToList();
		}

		public List<CurrencyDefinition> GetEarnableCurrencies()
		{
			return GetCurrencies().Where(c => c.CanEarn).ToList();
		}

		public List<CurrencyDefinition> GetConvertibleCurrencies()
		{
			return GetCurrencies().Where(c => c.CanConvert).ToList();
		}

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

		public CurrencyExchangeRate GetExchangeRate(Uid<CurrencyDefinition> fromCurrencyId, Uid<CurrencyDefinition> toCurrencyId)
		{
			return GetExchangeRate(GetCurrency(fromCurrencyId), GetCurrency(toCurrencyId));
		}

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

		public List<CurrencyExchangeRate> GetExchangeRatesForCurrency(Uid<CurrencyDefinition> currencyId)
		{
			return GetExchangeRatesForCurrency(GetCurrency(currencyId));
		}

		public List<CurrencyExchangeRate> GetAvailableExchangeRates()
		{
			return ExchangeRates.Where(e => e.IsAvailable()).ToList();
		}

		public bool HasExchangeRate(CurrencyDefinition fromCurrency, CurrencyDefinition toCurrency)
		{
			return GetExchangeRate(fromCurrency, toCurrency) != null;
		}

		public bool HasExchangeRate(Uid<CurrencyDefinition> fromCurrencyId, Uid<CurrencyDefinition> toCurrencyId)
		{
			return GetExchangeRate(fromCurrencyId, toCurrencyId) != null;
		}

		public long ConvertCurrency(long amount, CurrencyDefinition fromCurrency, CurrencyDefinition toCurrency)
		{
			var exchangeRate = GetExchangeRate(fromCurrency, toCurrency);
			if (exchangeRate == null)
			{
				return 0;
			}

			return exchangeRate.Convert(amount);
		}

		public long ConvertCurrency(long amount, Uid<CurrencyDefinition> fromCurrencyId, Uid<CurrencyDefinition> toCurrencyId)
		{
			return ConvertCurrency(amount, GetCurrency(fromCurrencyId), GetCurrency(toCurrencyId));
		}

		public List<CurrencyDefinition> GetCurrenciesWithDailyBonus()
		{
			return GetCurrencies().Where(c => c.DailyBonusAmount > 0).ToList();
		}
	}
}
