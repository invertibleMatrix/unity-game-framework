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
		protected override int CurrentSaveVersion => 3;

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
		[NonSerialized] private readonly List<CurrencyModel> _orphanedCurrencies = new();

		public IReadOnlyList<CurrencyModel> GetAllCurrencies() => _currencies;

		/// <summary>
		/// Balances whose currency identity no longer resolves. Kept out of the live list so
		/// gameplay never sees a definition-less model, but still persisted for support/diagnostics.
		/// </summary>
		public IReadOnlyList<CurrencyModel> GetOrphanedCurrencies() => _orphanedCurrencies;

		public CurrencyModel GetCurrencyModel(CurrencyDefinition definition)
		{
			if (definition == null) return null;
			return GetCurrencyModel(definition.IdAs<CurrencyDefinition>());
		}

		public CurrencyModel GetCurrencyModel(Uid<CurrencyDefinition> currencyId)
		{
			if (currencyId.IsNone) return null;

			for (int i = 0; i < _currencies.Count; i++)
			{
				if (_currencies[i].CurrencyId == currencyId) return _currencies[i];
			}

			return null;
		}

		public CurrencyModel GetCurrencyModel(CurrencyType currencyType)
		{
			if (currencyType == null) return null;
			return _currencies.FirstOrDefault(x => x.CurrencyDefinition != null && x.CurrencyDefinition.Type == currencyType);
		}

		/// <summary>
		/// Returns the balance for a currency, creating it with the definition's starting amount on first access.
		/// </summary>
		public CurrencyModel GetOrCreateCurrencyModel(CurrencyDefinition definition)
		{
			if (definition == null) return null;

			var existing = GetCurrencyModel(definition);
			if (existing != null) return existing;

			var created = new CurrencyModel(definition, definition.StartingAmount);
			_currencies.Add(created);
			Commit();
			return created;
		}

		public void AddCurrency(CurrencyModel currency)
		{
			if (currency == null || currency.CurrencyId.IsNone || _currencies.Contains(currency)) return;
			if (GetCurrencyModel(currency.CurrencyId) != null) return;

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
			_orphanedCurrencies.Clear();

			for (int i = _currencies.Count - 1; i >= 0; i--)
			{
				var currency = _currencies[i];
				if (currency.TryResolve(_metaDataRepository)) continue;

				Debug.LogWarning($"[ExampleGameModel] Currency balance for {currency.CurrencyId} (amount {currency.Amount}) no longer resolves to a CurrencyDefinition. Quarantined as orphan.");
				_orphanedCurrencies.Add(currency);
				_currencies.RemoveAt(i);
			}
		}

		protected override void OnMigrate()
		{
			// Version 3 changed the currency identity encoding; earlier saves are pre-launch and dropped.
			if (SaveVersion < 3)
			{
				_currencies.Clear();
				_serializedCurrencies.Clear();
			}
		}

		public override void OnBeforeSerialize()
		{
			_serializedCurrencies.Clear();
			AppendSerialized(_currencies);
			AppendSerialized(_orphanedCurrencies);
		}

		private void AppendSerialized(List<CurrencyModel> models)
		{
			foreach (var model in models)
			{
				_serializedCurrencies.Add(new SerializableCurrency
				{
					TypeName = model.GetType().AssemblyQualifiedName,
					Data = JsonUtility.ToJson(model)
				});
			}
		}

		public override void OnAfterDeserialize()
		{
			_currencies = new List<CurrencyModel>();
			foreach (var serializableCurrency in _serializedCurrencies)
			{
				var type = Type.GetType(serializableCurrency.TypeName);
				if (type != null && typeof(CurrencyModel).IsAssignableFrom(type))
				{
					_currencies.Add((CurrencyModel)JsonUtility.FromJson(serializableCurrency.Data, type));
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
