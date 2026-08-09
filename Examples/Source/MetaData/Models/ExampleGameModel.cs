using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using AK.CoreDomain;
using AK.Examples.Currency;
using AK.Examples.Models;
using UnityEngine;

namespace AK.Examples
{
	/// <summary>
	/// Example game model extending PersistableState. Games create their own
	/// model class with game-specific fields. The framework provides the
	/// persistence, session tracking, and migration via the base class.
	/// </summary>
	[Serializable]
	public class ExampleGameModel : PersistableState<ExampleGameModel>
	{
		protected override string SaveKey => "EXAMPLE_GAME_SAVE";
		protected override int CurrentSaveVersion => 2;

		// Game-specific fields — each game defines its own
		public int TotalStars;
		public int LastPlayedLevel = -1;
		public bool AudioEnabled = true;
		public bool VibrationEnabled = true;

		// Settings — universal framework building block
		public GameSettingsModel GameSettingsModel = new();

		// Currency management — common pattern most games need
		[NonSerialized] private List<CurrencyModel> _currencies = new();
		[SerializeField] private List<SerializableCurrency> _serializedCurrencies = new();

		[NonSerialized] private IMetaDataRepository _metaDataRepository;

		public IReadOnlyList<CurrencyModel> GetAllCurrencies() => _currencies;

		public CurrencyModel GetCurrencyModel(CurrencyDefinition definition)
		{
			if (definition == null) return null;
			return _currencies.FirstOrDefault(x => x.UniqueID == definition.UniqueID);
		}

		public CurrencyModel GetCurrencyModel(CurrencyType currencyType)
		{
			if (currencyType == null) return null;
			return _currencies.FirstOrDefault(x => x.CurrencyDefinition?.Type == currencyType);
		}

		public void AddCurrency(CurrencyModel currency)
		{
			if (currency == null || _currencies.Contains(currency)) return;
			_currencies.Add(currency);
			Commit();
		}

		public bool RemoveCurrency(CurrencyModel currency)
		{
			if (_currencies.Remove(currency))
			{
				Commit();
				return true;
			}
			return false;
		}

		public void SetMetaDataRepository(IMetaDataRepository metaDataRepository)
		{
			_metaDataRepository = metaDataRepository;
		}

		public override void OnInitialized(bool isFirstLaunch)
		{
			foreach (var currency in _currencies)
			{
				currency.ResolveUID(_metaDataRepository);
			}
		}

		protected override void OnMigrate()
		{
			// Example: migrate from version 1 to version 2
			// if (SaveVersion < 2) { ... }
		}

		public override void OnBeforeSerialize()
		{
			_serializedCurrencies.Clear();
			foreach (var currency in _currencies)
			{
				var serializableCurrency = new SerializableCurrency
				{
					TypeName = currency.GetType().AssemblyQualifiedName,
					Data = JsonUtility.ToJson(currency)
				};
				_serializedCurrencies.Add(serializableCurrency);
			}
		}

		public override void OnAfterDeserialize()
		{
			_currencies = new List<CurrencyModel>();
			foreach (var serializableCurrency in _serializedCurrencies)
			{
				var type = Type.GetType(serializableCurrency.TypeName);
				if (type != null)
				{
					var currency = (CurrencyModel)JsonUtility.FromJson(serializableCurrency.Data, type);
					_currencies.Add(currency);
				}
				else
				{
					var currency = new CurrencyModel();
					JsonUtility.FromJsonOverwrite(serializableCurrency.Data, currency);
					_currencies.Add(currency);
				}
			}
		}
	}
}
