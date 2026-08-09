using System;
using System.Collections.Generic;
using AK.Core;

namespace AK.Services.Facts
{
	[Serializable]
	public class FactLedgerState : PersistableState<FactLedgerState>
	{
		protected override string SaveKey => "UGFW_FACT_LEDGER";

		public List<FactCountEntry> Counts = new();
	}

	[Serializable]
	public class FactCountEntry
	{
		public string FactId;
		public int    Count;
	}
}
