using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Facts
{
	/// <summary>
	/// Identity of a recordable fact ("GoPressed", "BoardEventInteracted"). Facts are
	/// monotonic truths — they only accrete a count. For exchanges with a lifecycle
	/// (pending/credited/reversed, rewards), use TransactionType instead.
	/// </summary>
	[CreateAssetMenu(fileName = "FactType", menuName = "AK/Facts/Fact Type")]
	public class FactType : MetaDataAsset
	{
	}
}
