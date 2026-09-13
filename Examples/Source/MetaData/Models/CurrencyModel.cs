using System;
using AK.Core;
using AK.Examples.Currency;
using UnityEngine;

namespace AK.Examples.Models
{
	/// <summary>
	/// Persisted balance of one currency. The save holds only the currency's identity;
	/// the definition is re-resolved at load through an <see cref="IUidResolver"/>.
	/// An unresolvable identity is reported as an orphan, never healed by name.
	/// </summary>
	[Serializable]
	public class CurrencyModel : EntityModel
	{
		[SerializeField] private Uid<CurrencyDefinition> _currencyId;

		public int Amount;
		public int RefillCycles;

		public virtual CurrencyDefinition CurrencyDefinition { get; private set; }
		public virtual event Action<int>  OnChanged;
		public virtual event Action<int>  OnAmountAdded;
		public virtual event Action<int>  OnAmountDeducted;

		public Uid<CurrencyDefinition> CurrencyId => _currencyId;
		public bool IsResolved => CurrencyDefinition != null;

		public CurrencyModel() { }

		public CurrencyModel(CurrencyDefinition definition, int amount = 0)
		{
			if (definition == null) throw new ArgumentNullException(nameof(definition));

			_currencyId = definition.IdAs<CurrencyDefinition>();
			CurrencyDefinition = definition;
			Amount = amount;
		}

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
		/// <returns>True if the full amount was deducted. False if insufficient balance (no deduction).</returns>
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

		/// <summary>
		/// Re-binds the definition after load. Returns false when the persisted identity
		/// is None or no longer resolves; the caller decides whether to drop or quarantine.
		/// </summary>
		public bool TryResolve(IUidResolver resolver)
		{
			CurrencyDefinition = null;

			if (resolver == null || _currencyId.IsNone) return false;
			if (!resolver.TryResolve(_currencyId, out CurrencyDefinition definition)) return false;

			CurrencyDefinition = definition;
			return true;
		}
	}
}
