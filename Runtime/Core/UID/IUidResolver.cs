namespace AK.Core
{
	/// <summary>
	/// Untyped identity resolution across every registered domain. Implemented by the
	/// metadata repository, which aggregates all registry assets it knows about. For typed
	/// lookups prefer the domain registry directly — this exists for the places that
	/// genuinely hold a polymorphic Uid: ledgers, reward references, redirect tooling.
	/// </summary>
	public interface IUidResolver
	{
		bool TryResolve(Uid id, out UID asset);
		bool TryResolve<T>(Uid id, out T asset) where T : UID;
		bool TryResolve<T>(Uid<T> id, out T asset) where T : UID;
	}
}
