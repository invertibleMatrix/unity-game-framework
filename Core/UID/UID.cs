using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Core
{
	[CreateAssetMenu(fileName = "UID_", menuName = "AK/UID_")]
	public class UID : ScriptableObject, IEquatable<UID>
	{
		[SerializeField, ReadOnly]
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
			}

			// Auto-generate description if empty (optional)
			if (string.IsNullOrEmpty(_description))
			{
				_description = $"UID_{name}";
			}
		}

		[Button("Generate New ID")]
		public void GenerateNewGuid()
		{
			_id = Guid.NewGuid().ToString();
		}

		// Enhanced ToString for better debugging
		public override string ToString()
		{
			return string.IsNullOrEmpty(_description) ? _id : $"{_description} ({_id})";
		}

		public static UID EmptyUID()
		{
			var instance = CreateInstance<UID>();
			instance._id = Guid.Empty.ToString();
			return instance;
		}

		// This is the key - always compares by GUID string, never reference
		public bool Equals(UID other)
		{
			if (ReferenceEquals(null, other)) return IsEmpty();
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
			if (ReferenceEquals(left, null) && ReferenceEquals(right, null)) return true;
			if (ReferenceEquals(left, null) || ReferenceEquals(right, null)) return false;
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

		public static implicit operator Guid(UID uid)
		{
			if (uid?.IsEmpty() != false) return Guid.Empty;
			return Guid.TryParse(uid.Id, out var result) ? result : Guid.Empty;
		}
	}
}