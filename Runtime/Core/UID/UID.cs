using System;
using System.Collections.Generic;
using UnityEngine;

namespace AK.Core
{
	[CreateAssetMenu(fileName = "UID_", menuName = "AK/UID_")]
	public class UID : ScriptableObject, IEquatable<UID>
	{
		[SerializeField]
		private string _id;

		[SerializeField, TextArea(1, 2)]
		private string _description;

		public string Id          => _id;

		public bool IsEmpty() => string.IsNullOrEmpty(_id) || _id == Guid.Empty.ToString();

		public UID UniqueID => this;
		
		private void OnValidate()
		{
			if (string.IsNullOrEmpty(_id))
			{
				GenerateNewGuid();
#if UNITY_EDITOR
				// OnValidate changes are in-memory only — without SetDirty the file keeps
				// an empty _id and every editor session mints a new ephemeral GUID,
				// silently breaking every stored link.
				UnityEditor.EditorUtility.SetDirty(this);
#endif
			}

			// Auto-generate description if empty (optional)
			if (string.IsNullOrEmpty(_description))
			{
				_description = $"UID_{name}";
			}
		}

		public void GenerateNewGuid()
		{
			_id = Guid.NewGuid().ToString();
		}

		// Enhanced ToString for better debugging
		public override string ToString()
		{
			return string.IsNullOrEmpty(_description) ? _id : $"{_description} ({_id})";
		}

		private static UID _empty;

		public static UID EmptyUID()
		{
			if (_empty == null)
			{
				_empty = CreateInstance<UID>();
				_empty._id = Guid.Empty.ToString();
				_empty.hideFlags = HideFlags.HideAndDontSave;
			}

			return _empty;
		}

		// This is the key - always compares by GUID string, never reference
		public bool Equals(UID other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;

			// Handle empty/null cases consistently
			if (IsEmpty() && other.IsEmpty()) return true;
			if (IsEmpty() || other.IsEmpty()) return false;

			return string.Equals(_id, other._id, StringComparison.OrdinalIgnoreCase);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;

			if (obj is UID uid) return Equals(uid);
			if (obj is string s) return string.Equals(_id, s, StringComparison.OrdinalIgnoreCase);
			if (obj is Guid g) return string.Equals(_id, g.ToString(), StringComparison.OrdinalIgnoreCase);

			return false;
		}

		// Critical for Dictionary keys - always hash the GUID string
		public override int GetHashCode()
		{
			// StringComparer.OrdinalIgnoreCase handles case-insensitive hashing
			return IsEmpty() ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(_id);
		}
		
		public static bool operator ==(UID left, UID right)
		{
			// Unity fake-null aware: a destroyed UID compares equal to null.
			bool leftNull  = ReferenceEquals(left, null)  || (UnityEngine.Object)left  == null;
			bool rightNull = ReferenceEquals(right, null) || (UnityEngine.Object)right == null;

			if (leftNull && rightNull) return true;
			if (leftNull || rightNull) return false;
			return left.Equals(right);
		}

		public static bool operator !=(UID left, UID right)
		{
			return !(left == right);
		}

		public static bool operator ==(UID left, string right)
		{
			if (ReferenceEquals(left, null)) return string.IsNullOrEmpty(right);
			return string.Equals(left._id, right, StringComparison.OrdinalIgnoreCase);
		}

		public static bool operator !=(UID left, string right)
		{
			return !(left == right);
		}

		public static bool operator ==(string left, UID right)
		{
			return right == left;
		}

		public static bool operator !=(string left, UID right)
		{
			return !(left == right);
		}

		// Implicit conversions maintain the GUID-based approach
		public static implicit operator string(UID uid)
		{
			return uid?.Id;
		}

		// Asset → link projection, for framework internals (persistence writes strings).
		public static implicit operator UIDRef(UID uid)
		{
			return uid != null ? new UIDRef(uid) : null;
		}

		public static implicit operator Guid(UID uid)
		{
			if (uid?.IsEmpty() != false) return Guid.Empty;
			return Guid.TryParse(uid.Id, out var result) ? result : Guid.Empty;
		}
	}
}