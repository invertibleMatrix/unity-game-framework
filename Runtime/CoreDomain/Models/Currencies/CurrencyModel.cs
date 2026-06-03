using System;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Currency;
using UnityEngine;

namespace GameplayCore.Models
{
	[Serializable]
	public class CurrencyModel : EntityModel, ISerializationCallbackReceiver
	{
		[SerializeField] private string _uidID;
		[SerializeField] private string _uidName;

		public virtual CurrencyDefinition CurrencyDefinition { get; private set; }
		public virtual event Action<int>  OnChanged;
		public virtual event Action<int>  OnAmountAdded;
		public virtual event Action<int>  OnAmountDeducted;

		public UID UniqueID;
		public int Amount;
		public int RefillCycles;

		/// <summary>
		/// Whether this model needs UID resolution after deserialization.
		/// </summary>
		public bool NeedsResolution { get; private set; }

		/// <summary>
		/// Adds the specified amount. Respects MaxAmount from CurrencyDefinition.
		/// </summary>
		/// <returns>The actual amount added (may be less if capped).</returns>
		public int Add(int amount)
		{
			if (amount <= 0) return 0;

			int actualAmount = amount;

			if (CurrencyDefinition != null && CurrencyDefinition.MaxAmount > 0)
			{
				long remaining = CurrencyDefinition.MaxAmount - Amount;
				if (remaining <= 0) return 0;

				actualAmount = (int)Mathf.Min(amount, remaining);
			}

			Amount += actualAmount;
			OnChanged?.Invoke(actualAmount);
			OnAmountAdded?.Invoke(actualAmount);
			return actualAmount;
		}

		/// <summary>
		/// Deducts the specified amount. Cannot go below zero.
		/// </summary>
		/// <param name="amount">The amount to deduct.</param>
		/// <returns>True if the full amount was deducted. False if insufficient balance (partial or no deduction).</returns>
		public bool Deduct(int amount)
		{
			if (amount <= 0) return false;

			if (amount > Amount) return false;

			Amount -= amount;
			OnChanged?.Invoke(-amount);
			OnAmountDeducted?.Invoke(amount);
			return true;
		}

		/// <summary>
		/// Deducts whatever is possible, even if insufficient.
		/// </summary>
		/// <returns>The actual amount deducted.</returns>
		public int DeductPartial(int amount)
		{
			if (amount <= 0) return 0;

			int actual = Mathf.Min(amount, Amount);
			Amount -= actual;
			if (actual > 0)
			{
				OnChanged?.Invoke(-actual);
				OnAmountDeducted?.Invoke(actual);
			}
			return actual;
		}

		public void SetDefinition(CurrencyDefinition definition)
		{
			CurrencyDefinition = definition;
		}

		public float GetFillProgress()
		{
			if (CurrencyDefinition == null || CurrencyDefinition.MaxAmount <= 0) return 0f;
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
				_uidName = UniqueID.name;
			}
		}

		public void OnAfterDeserialize()
		{
			NeedsResolution = !string.IsNullOrEmpty(_uidID) || !string.IsNullOrEmpty(_uidName);
		}

		public void ResolveUID(IMetaDataRepository repository)
		{
			// Try GUID lookup first
			if (!string.IsNullOrEmpty(_uidID))
			{
				UniqueID = repository.UIDRegistry.GetUID(_uidID);
				if (UniqueID != null)
				{
					ResolveDefinition(repository);
					NeedsResolution = false;
					return;
				}
			}

			// Fallback to asset name lookup
			if (!string.IsNullOrEmpty(_uidName))
			{
				UniqueID = repository.UIDRegistry.GetUIDByName(_uidName);
				if (UniqueID != null)
				{
					Debug.LogWarning($"CurrencyModel UID resolved via name fallback: {_uidName}");
					ResolveDefinition(repository);
					NeedsResolution = false;
					return;
				}
			}

			if (NeedsResolution)
			{
				Debug.LogWarning($"CurrencyModel UID could not be resolved. GUID: '{_uidID}', Name: '{_uidName}'");
				NeedsResolution = false;
			}
		}

		private void ResolveDefinition(IMetaDataRepository repository)
		{
			if (UniqueID != null && repository.CurrencyMeta != null)
			{
				var definition = repository.CurrencyMeta.Registry.GetObjectByUID(UniqueID) as CurrencyDefinition;
				if (definition != null)
				{
					SetDefinition(definition);
				}
			}
		}
	}
}
