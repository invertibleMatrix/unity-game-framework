using System;

namespace AK.CoreDomain.Facts
{
	// Class with plain fields (not a readonly struct) so Unity can serialize
	// condition lists inside definition assets.
	// Direct reference is deliberate: FactType is a pure-identity (stateless) UID
	// asset, so bundle duplication of it is behaviorally transparent — no GUID
	// link ceremony needed here.
	[Serializable]
	public class FactCondition
	{
		public FactType Type;
		public int MinCount = 1;

		public FactCondition() { }

		public FactCondition(FactType type, int minCount = 1)
		{
			Type = type;
			MinCount = minCount;
		}
	}
}
