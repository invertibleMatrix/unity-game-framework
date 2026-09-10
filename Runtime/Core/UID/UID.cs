using System;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// The authoring handle for an identity: a ScriptableObject a designer can drag into a
	/// field instead of typing a string. It carries exactly one <see cref="Uid"/>, assigned
	/// once when the asset is created and never changed afterwards.
	///
	/// Equality is Unity's reference equality. Two live instances with the same Uid are a
	/// bug (asset-bundle duplication) that the build validator refuses to ship; masking it
	/// with value-equality would hide the bug, not fix it. Compare <c>.Id</c> when you mean
	/// "same identity".
	///
	/// The asset has no runtime role beyond holding its Uid and whatever a subclass adds.
	/// Everything that needs to store, compare, hash, or transmit an identity uses the Uid.
	/// </summary>
	public class UID : ScriptableObject
	{
		[SerializeField, HideInInspector] private Uid            _id;
		[SerializeField, HideInInspector] private UidProvenance  _provenance;
		[SerializeField, HideInInspector] private string         _provenanceSource;

		[SerializeField, TextArea(1, 3)] private string _notes;

		public Uid           Id               => _id;
		public UidProvenance Provenance       => _provenance;
		public string        ProvenanceSource => _provenanceSource;
		public string        Notes            => _notes;

		public bool HasIdentity => _id.IsSet;

		/// <summary>Typed view of the identity. Zero cost; exists so call sites read naturally.</summary>
		public Uid<T> IdAs<T>() where T : UID => new(_id);

		public override string ToString()
		{
			return _id.IsSet ? $"{name} ({_id.ToShortString()})" : $"{name} (no identity)";
		}

#if UNITY_EDITOR
		/// <summary>
		/// The only write path to identity, and it is editor-only. Callers are the identity
		/// authority (asset creation, duplicate resolution, import) — never gameplay code.
		/// </summary>
		internal void Editor_AssignIdentity(Uid id, UidProvenance provenance, string source)
		{
			_id               = id;
			_provenance       = provenance;
			_provenanceSource = source ?? string.Empty;
		}
#endif
	}
}
