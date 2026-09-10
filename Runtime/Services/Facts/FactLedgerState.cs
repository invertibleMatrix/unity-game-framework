using System;
using System.Collections.Generic;
using AK.Core;

namespace AK.Services.Facts
{
	/// <summary>
	/// One count row per fact. Rows are keyed by identity value, never by asset reference
	/// or name, so the file is meaningful without the asset database and survives renames.
	/// Rows whose identity no longer resolves are kept (a later patch may restore the
	/// content) but reported as orphans; nothing is ever remapped silently.
	/// </summary>
	[Serializable]
	public class FactLedgerState : PersistableState<FactLedgerState>
	{
		protected override string SaveKey => "UGFW_FACT_LEDGER";

		public List<FactCountEntry> Counts = new();
	}

	[Serializable]
	public class FactCountEntry
	{
		public Uid FactId;
		public int Count;
	}
}
