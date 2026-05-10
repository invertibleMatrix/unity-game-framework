using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AK.Core;
using GameplayCore.MetaData;
using GameplayCore.MetaData.Currency;
using GameplayCore.MetaData.Rewards;
using UnityEngine;

namespace GameplayCore.Models
{
	[Serializable]
	public class GameModel : EntityModel, ISerializationCallbackReceiver
	{
		public const int CURRENT_SAVE_VERSION = 1;

		public int  SaveVersion     = 1;
		public int  PlayerLevel     = 1;
		public int  CurrentSession  = 1;
		public int  CurrentDay      = 1;
		public int  LastPlayedLevel = -1;
		public int  TotalStars      = 0;
		public bool AudioEnabled    = true;
		public bool VibrationEnabled = true;

		public string SessionStartTime = GetFormattedTime(DateTime.UtcNow);
		public string SessionEndTime   = GetFormattedTime(DateTime.UtcNow);

		public GameSettingsModel GameSettingsModel = new();
		public GameStateModel    GameStateModel    = new();

		public List<Transaction> PendingLevelCompleteRewards     = new();
		public List<Transaction> PendingPurchasableTransactions = new();

		public DateTime SessionStartTimeDT => GetDateTimeFromString(SessionStartTime);
		public DateTime SessionEndTimeDT   => GetDateTimeFromString(SessionEndTime);

		[NonSerialized] private List<CurrencyModel> _currencies = new();

		[SerializeField] private List<SerializableCurrency> _serializedCurrencies = new();

		[SerializeField] private string _version;

		[NonSerialized] private IMetaDataRepository _metaDataRepository;

		[NonSerialized] private bool _isDirty;

		[NonSerialized]
		private Dictionary<TransactionType, List<Transaction>> _transactionCollection;

		public IMetaDataRepository                            MetaDataRepository => _metaDataRepository;
		public Dictionary<TransactionType, List<Transaction>> Transactions       => _transactionCollection;
		public bool                                           IsDirty            => _isDirty;

		/// <summary>
		/// Initializes the GameModel. Loads from save if available, resolves UIDs, and credits pending transactions.
		/// </summary>
		/// <param name="metaDataRepository">The metadata repository for UID resolution.</param>
		/// <param name="isFirstLaunch">True if this is the first time the game has been launched (no save data).</param>
		public void Initialize(IMetaDataRepository metaDataRepository, out bool isFirstLaunch)
		{
			_metaDataRepository = metaDataRepository;
			DateTime now = DateTime.UtcNow;
			SessionStartTime = now.ToString("O");
			CurrentSession++;

			isFirstLaunch = string.IsNullOrEmpty(_version);
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
				{ TransactionType.PurchasableItem, PendingPurchasableTransactions },
			};

			ResolveTransactionUIDs(PendingLevelCompleteRewards);
			ResolveTransactionUIDs(PendingPurchasableTransactions);

			foreach (var currency in _currencies)
			{
				currency.ResolveUID(_metaDataRepository);
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
			return _currencies.FirstOrDefault(x => x.CurrencyDefinition?.Type == currencyType);
		}

		public CurrencyModel GetCurrencyModel(Func<CurrencyModel, bool> predicate)
		{
			return _currencies.FirstOrDefault(predicate);
		}

		public IReadOnlyList<CurrencyModel> GetAllCurrencies() => _currencies;

		public void AddCurrency(CurrencyModel currency)
		{
			if (currency == null || _currencies.Contains(currency)) return;
			_currencies.Add(currency);
			MarkDirty();
		}

		public bool RemoveCurrency(CurrencyModel currency)
		{
			if (_currencies.Remove(currency))
			{
				MarkDirty();
				return true;
			}
			return false;
		}

		private void Migrate()
		{
			if (SaveVersion < CURRENT_SAVE_VERSION)
			{
				Debug.Log($"Migrating GameModel from version {SaveVersion} to {CURRENT_SAVE_VERSION}");
				SaveVersion = CURRENT_SAVE_VERSION;
				MarkDirty();
			}
		}

		private void ResolveTransactionUIDs(List<Transaction> transactions)
		{
			foreach (var transaction in transactions)
			{
				transaction.ResolveUID(_metaDataRepository);
			}
		}

		/// <summary>
		/// Marks the model as modified. Call Commit() to persist.
		/// </summary>
		public void MarkDirty()
		{
			_isDirty = true;
		}

		/// <summary>
		/// Persists the model to disk if it has been modified since the last save.
		/// </summary>
		public void Commit()
		{
			if (!_isDirty) return;

			DateTime now = DateTime.UtcNow;
			SessionEndTime = now.ToString("O");
			GameSaveService.Save(this);
			_isDirty = false;
		}

		/// <summary>
		/// Forces a save to disk regardless of dirty state.
		/// </summary>
		public void ForceCommit()
		{
			DateTime now = DateTime.UtcNow;
			SessionEndTime = now.ToString("O");
			GameSaveService.Save(this);
			_isDirty = false;
		}

		public void CommitSessionEndTime()
		{
			DateTime now = DateTime.UtcNow;
			SessionEndTime = now.ToString("O");
			MarkDirty();
			Commit();
		}

		public void AppendLevelCompleteRewards(List<UID> ids)
		{
			foreach (UID uid in ids)
			{
				if (uid == null || uid.IsEmpty())
				{
					Debug.LogWarning("Cannot append reward with empty UID");
					continue;
				}

				PendingLevelCompleteRewards.Add(new Transaction
				{
					UID = uid,
					Time = GetFormattedTime(DateTime.UtcNow)
				});
			}

			if (ids.Count > 0) MarkDirty();
		}

		public void AppendPurchasedItemTransaction(List<UID> ids)
		{
			foreach (UID uid in ids)
			{
				if (uid == null || uid.IsEmpty())
				{
					Debug.LogWarning("Cannot append purchased item with empty UID");
					continue;
				}

				PendingPurchasableTransactions.Add(new Transaction
				{
					UID = uid,
					Time = GetFormattedTime(DateTime.UtcNow)
				});
			}

			if (ids.Count > 0) MarkDirty();
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
					if (PendingPurchasableTransactions.Count > 0)
					{
						foreach (Transaction transaction in PendingPurchasableTransactions.ToList())
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
				MarkDirty();
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

		public static DateTime GetDateTimeFromString(string dt)
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
				Debug.LogWarning($"Orphaned transaction removed from {transactionType} — UID was null or empty");
				MarkDirty();
				return;
			}

			var rewardDefinition = _metaDataRepository.RewardsMeta.Registry.GetObjectByUID(rewardUID);
			if (rewardDefinition == null)
			{
				transactions.RemoveAt(idx);
				Debug.LogWarning($"Orphaned transaction removed from {transactionType}. " +
				                 $"UID '{rewardUID.Id}' not found in Reward Registry.");
				MarkDirty();
				return;
			}

			ApplyReward(rewardDefinition);

			transactions.RemoveAt(idx);
			MarkDirty();
		}

		private void ApplyReward(RewardDefinition reward)
		{
			switch (reward.Type)
			{
				case RewardType.Star:
					TotalStars += reward.Amount;
					break;

				case RewardType.Currency:
					if (reward.CurrencyDefinition != null)
					{
						var currencyModel = GetCurrencyModel(reward.CurrencyDefinition);
						if (currencyModel != null)
						{
							currencyModel.Add(reward.Amount);
						}
						else
						{
							Debug.LogWarning($"No CurrencyModel found for {reward.CurrencyDefinition.DisplayName}");
						}
					}
					break;

				case RewardType.Bundle:
					if (reward.Bundle != null)
					{
						foreach (var subReward in reward.Bundle.GetAllRewardsRecursive())
						{
							ApplyReward(subReward);
						}
					}
					break;

				case RewardType.Gacha:
					if (reward.GachaBundle != null)
					{
						var gachaResults = reward.GachaBundle.EvaluateRewards();
						foreach (var gachaReward in gachaResults)
						{
							ApplyReward(gachaReward);
						}
					}
					break;

				case RewardType.Subscription:
				case RewardType.Unlockable:
				case RewardType.Powerup:
				case RewardType.Booster:
				case RewardType.Live:
				case RewardType.NoAds:
					// These reward types require game-specific handling.
					// Consumers should listen to OnRewardApplied or override in a subclass.
					break;
			}

			Debug.Log($"Reward applied: {reward.DisplayName} ({reward.Type}) x{reward.Amount}");
		}

		public void OnBeforeSerialize()
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

		public void OnAfterDeserialize()
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
					Debug.LogWarning($"Could not resolve CurrencyModel type: {serializableCurrency.TypeName}. " +
					                 "Using base CurrencyModel.");
					var currency = new CurrencyModel();
					JsonUtility.FromJsonOverwrite(serializableCurrency.Data, currency);
					_currencies.Add(currency);
				}
			}
		}
	}
}
