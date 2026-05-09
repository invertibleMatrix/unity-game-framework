using System;
using AK.Core;
using GameplayCore.MetaData;
using GameplayCore.MetaData.Currency;
using UnityEngine;

namespace GameplayCore.Models
{
	[Serializable]
	public class CurrencyModel : EntityModel, ISerializationCallbackReceiver
	{
		[SerializeField] private string _uidID;

		public virtual CurrencyDefinition CurrencyDefinition { get; private set; }
		public virtual event Action<int>  OnChanged;
		public virtual event Action<int>  OnAmountAdded;
		public virtual event Action<int>  OnAmountDeducted;

		public UID UniqueID;
		public int Amount;
		public int RefillCycles;

		public void Add(int amount)
		{
			if (CurrencyDefinition?.MaxAmount > 0 && Amount >= CurrencyDefinition.MaxAmount)
			{
				Debug.Log($"Max Amount reached for {CurrencyDefinition.DisplayName}");
				return;
			}

			Amount += amount;
			OnChanged?.Invoke(amount);
			OnAmountAdded?.Invoke(amount);
		}

		public void Deduct(int amount)
		{
			Amount = Mathf.Clamp(Amount, 0, Amount - amount);
			OnChanged?.Invoke(amount);
			OnAmountDeducted?.Invoke(amount);
		}

		public void SetDefinition(CurrencyDefinition definition)
		{
			CurrencyDefinition = definition;
		}

		public float GetFillProgress()
		{
			return (float)Amount / CurrencyDefinition.MaxAmount;
		}

		public void ResetFillCycle()
		{
			Amount = 0;
			RefillCycles++;
		}

		public void OnBeforeSerialize()
		{
			if (UniqueID != null)
			{
				_uidID = UniqueID.Id;
			}
		}

		public void OnAfterDeserialize() { }

		public void ResolveUID(IMetaDataRepository repository)
		{
			if (!string.IsNullOrEmpty(_uidID))
			{
				UniqueID = repository.UIDRegistry.GetUID(_uidID);
			}
		}
	}
}