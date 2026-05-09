using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AK.Core;
using GameplayCore.MetaData;
using GameplayCore.MetaData.Currency;
using GameplayCore.MetaData.Rewards;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameplayCore.Models
{
	[Serializable]
	public class GameModel : EntityModel, ISerializationCallbackReceiver
	{
		
		private readonly PrefsProperty<GameModel> _model = new("SAVE_FILE", new GameModel());
		
		public const int CURRENT_SAVE_VERSION = 1;

		public int  SaveVersion     = 1;
		public int  PlayerLevel     = 1;
		public int  CurrentSession  = 1;
		public int  CurrentDay      = 1;
		public int  LastPlayedLevel = -1;
		public int  TotalStars      = 0;
		public bool AudioEnabled = true;
		public bool VibrationEnabled = true;

		public string SessionStartTime = GetFormattedTime(DateTime.UtcNow);
		public string SessionEndTime   = GetFormattedTime(DateTime.UtcNow);

		public GameSettingsModel GameSettingsModel = new();
		public GameStateModel    GameStateModel    = new();

		public List<Transaction> PendingLevelCompleteRewards = new();

		public DateTime SessionStartTimeDT => GetDataTimeFromString(SessionStartTime);
		public DateTime SessionEndTimeDT   => GetDataTimeFromString(SessionEndTime);

		[ShowInInspector, NonSerialized]
		private List<CurrencyModel> _currencies = new();

		[SerializeField] private List<SerializableCurrency> _serializedCurrencies = new();

		[SerializeField] private List<Transaction> _pendingPurchasableTransactions = new();

		[SerializeField] private string _version;

		private IMetaDataRepository _metaDataRepository;

		private Dictionary<TransactionType, List<Transaction>> _transactionCollection;

		public IMetaDataRepository                            MetaDataRepository => _metaDataRepository;
		public Dictionary<TransactionType, List<Transaction>> Transactions       => _transactionCollection;

		public void Initialize(IMetaDataRepository metaDataRepository, out bool isFirstLaunch)
		{
			_metaDataRepository = metaDataRepository;
			DateTime now = DateTime.UtcNow;
			SessionStartTime = now.ToString("O");
			CurrentSession++;

			if (string.IsNullOrEmpty(_version))
			{
				isFirstLaunch = true;
			}
			else
			{
				isFirstLaunch = false;
			}

			_version = Application.version;

			if (TryGetSessionEndTime(out DateTime lastSessionTime))
			{
				bool isNewDay = now.Date != lastSessionTime.Date;
				if (isNewDay)
				{
					CurrentDay++;
				}
			}

			_transactionCollection = new()
			{
				{ TransactionType.LevelCompleteTransaction, PendingLevelCompleteRewards },
				{ TransactionType.PurchasableItem, _pendingPurchasableTransactions },
			};

			ResolveTransactionUIDs(PendingLevelCompleteRewards);
			ResolveTransactionUIDs(_pendingPurchasableTransactions);

			foreach (var currency in _currencies)
			{
				currency.ResolveUID(_metaDataRepository);
				if (currency.UniqueID != null)
				{
					// var def = _metaDataRepository.CurrencyMeta.Registry.GetObjectByUID(currency.UniqueID);
					// if (def != null)
					// {
					// 	currency.SetDefinition(def);
					// }
				}
			}

			Migrate();

			CreditPendingTransactions(TransactionType.LevelCompleteTransaction);
			CreditPendingTransactions(TransactionType.GachaBoxTransaction);
			CreditPendingTransactions(TransactionType.PurchasableItem);

			Commit();
		}
		
		public CurrencyModel GetCurrencyModel(CurrencyDefinition definition)
		{
			if (definition == null) return null;
			return _currencies.FirstOrDefault(x => x.UniqueID == definition.UniqueID);
		}

		public CurrencyModel GetCurrencyModel(CurrencyType currencyType)
		{
			return _currencies.FirstOrDefault(x => x.CurrencyDefinition.Type == currencyType);
		}

		public CurrencyModel GetCurrencyModel(Func<CurrencyModel, bool> cPredicate)
		{
			return _currencies.FirstOrDefault(cPredicate);
		}

		private void Migrate()
		{
			if (SaveVersion < CURRENT_SAVE_VERSION)
			{
				Debug.Log($"Migrating GameModel from version {SaveVersion} to {CURRENT_SAVE_VERSION}");
				SaveVersion = CURRENT_SAVE_VERSION;
				Commit();
			}
		}

		private void ResolveTransactionUIDs(List<Transaction> transactions)
		{
			foreach (var transaction in transactions)
			{
				transaction.ResolveUID(_metaDataRepository);
			}
		}

		public void Commit()
		{
			DateTime now = DateTime.UtcNow;
			SessionEndTime = now.ToString("O");

			_model.Save(this);
		}

		public void CommitSessionEndTime()
		{
			DateTime now = DateTime.UtcNow;
			SessionEndTime = now.ToString("O");
			Commit();
		}

		public void AppendLevelCompleteRewards(List<UID> ids)
		{
			foreach (UID uid in ids)
			{
				if (uid.IsEmpty())
				{
					Debug.Log($"UID for {uid} is empty");
					continue;
				}

				PendingLevelCompleteRewards.Add(new Transaction()
				{
					UID = uid,
					Time = GetFormattedTime(DateTime.UtcNow)
				});
			}
		}

		public void AppendPurchasedItemTransaction(List<UID> ids)
		{
			foreach (UID uid in ids)
			{
				if (uid.IsEmpty())
				{
					Debug.Log($"UID for {uid} is empty");
					continue;
				}

				_pendingPurchasableTransactions.Add(new Transaction()
				{
					UID = uid,
					Time = GetFormattedTime(DateTime.UtcNow)
				});
			}
		}

		public void CreditPendingTransactions(TransactionType transactionType)
		{
			switch (transactionType)
			{
				case TransactionType.LevelCompleteTransaction:
					if (PendingLevelCompleteRewards.Count > 0)
					{
						foreach (Transaction transaction in PendingLevelCompleteRewards.ToList())
						{
							CreditPendingReward(TransactionType.LevelCompleteTransaction, transaction.UID);
						}
					}

					break;
				case TransactionType.PurchasableItem:
					if (_pendingPurchasableTransactions.Count > 0)
					{
						foreach (Transaction transaction in _pendingPurchasableTransactions.ToList())
						{
							CreditPendingReward(TransactionType.PurchasableItem, transaction.UID);
						}
					}

					break;
			}
		}

		public void RemoveLevelCompleteReward(UID uid)
		{
			int idx = PendingLevelCompleteRewards.FindIndex(x => x.UID == uid);
			if (idx != -1)
			{
				PendingLevelCompleteRewards.RemoveAt(idx);
			}
		}

		public static string GetFormattedTime(DateTime dateTime)
		{
			return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
		}

		public bool TryGetSessionEndTime(out DateTime time)
		{
			time = DateTime.Now;
			if (DateTime.TryParse(SessionEndTime, null, DateTimeStyles.RoundtripKind, out DateTime t))
			{
				time = t;
				return true;
			}

			return false;
		}

		public static DateTime GetDataTimeFromString(string dt)
		{
			if (DateTime.TryParse(dt, null, DateTimeStyles.RoundtripKind, out DateTime time))
			{
				return time;
			}

			return DateTime.Now;
		}

		public void CreditPendingReward(TransactionType transactionType, UID rewardUID)
		{
			if (!_transactionCollection.TryGetValue(transactionType, out List<Transaction> transactions))
			{
				return;
			}

			int idx = transactions.FindIndex(x => x.UID == rewardUID);
			if (idx == -1)
			{
				Debug.LogWarning($"Transaction not found in {transactionType}. UID: {rewardUID}");
				return;
			}

			if (rewardUID == null || rewardUID.IsEmpty())
			{
				transactions.RemoveAt(idx);
				Debug.LogWarning($"Orphaned transaction removed from {transactionType} - UID was null or empty");
				return;
			}

			var rewardDefinition = _metaDataRepository.RewardsMeta.Registry.GetObjectByUID(rewardUID);
			if (rewardDefinition == null)
			{
				// Gracefully handle orphaned transaction - remove it so player isn't stuck
				transactions.RemoveAt(idx);
				Debug.LogWarning($"Orphaned transaction removed from {transactionType}. " +
				                 $"UID '{rewardUID.Id}' (name: '{rewardUID.name}') not found in Reward Registry. " +
				                 $"The reward asset may have been deleted or its GUID changed.");
				return;
			}

			Debug.Log($"CreditPendingReward {rewardDefinition.DisplayName} {rewardDefinition.Amount}");
			switch (rewardDefinition.Type)
			{
				case RewardType.Subscription:
					// Should be handled by Subscription System internally
					break;
				case RewardType.Star:
					// Handled internally
					break;
				case RewardType.Gacha:
					// Handled by exploding the gacha and crediting those rewards as outcomes e.g Boosters, Powerups, Coins, Gems
					break;
				case RewardType.Unlockable:
					// No Use currently
					break;
				case RewardType.Bundle:
					Debug.LogError("Exploded bundle was not passed in as reward def!");
					break;
			}

			transactions.RemoveAt(idx);
			Commit();
		}

		public void OnBeforeSerialize()
		{
			_serializedCurrencies.Clear();
			foreach (var currency in _currencies)
			{
				var serializableCurrency = new SerializableCurrency
				{
					TypeName = currency.GetType().FullName,
					Data = JsonUtility.ToJson(currency)
				};
				_serializedCurrencies.Add(serializableCurrency);
			}
		}

		public void OnAfterDeserialize()
		{
			_currencies = new List<CurrencyModel>();
			foreach (var serializableCurrency in _serializedCurrencies)
			{
				var type = Type.GetType(serializableCurrency.TypeName);
				if (type != null)
				{
					CurrencyModel currency = (CurrencyModel)JsonUtility.FromJson(serializableCurrency.Data, type);
					_currencies.Add(currency);
				}
			}
		}
	}
}