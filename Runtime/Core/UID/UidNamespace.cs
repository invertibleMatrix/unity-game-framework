using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// A namespace for deterministic identities. A UID asset created under a namespace
	/// receives <c>Uid.Deterministic(namespace.Id, canonicalName)</c> instead of a random
	/// value, so "SoftCurrency" under the "UGFW.CurrencyTypes" namespace is the same
	/// identity in every project and on every server that knows the namespace.
	///
	/// The namespace's own identity is random and minted once; it is the root of the tree.
	/// </summary>
	[CreateAssetMenu(fileName = "UidNamespace", menuName = "AK/UID/Namespace")]
	public sealed class UidNamespace : UID
	{
		[SerializeField, Tooltip("Human-readable path, e.g. 'UGFW.CurrencyTypes'. Informational — the identity is what matters.")]
		private string _path;

		public string Path => _path;

		/// <summary>The identity an asset with this canonical name receives under this namespace.</summary>
		public Uid Derive(string canonicalName) => Uid.Deterministic(Id, canonicalName);
	}
}
