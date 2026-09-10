using System;

namespace AK.Systems
{
	/// <summary>
	/// Identity of a view kind: concrete view type plus variant id. Keys the prefab
	/// lookup, the pool, and the registry index so every "find a view of this kind"
	/// question is a hash lookup instead of a scan.
	/// </summary>
	public readonly struct ViewKey : IEquatable<ViewKey>
	{
		public readonly Type   Type;
		public readonly string ViewId;

		public ViewKey(Type type, string viewId)
		{
			Type = type;
			ViewId = viewId ?? string.Empty;
		}

		public static ViewKey Of(UIView view) => new(view.GetType(), view.ViewId);

		public bool Equals(ViewKey other) => Type == other.Type && string.Equals(ViewId, other.ViewId, StringComparison.Ordinal);

		public override bool Equals(object obj) => obj is ViewKey other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(Type, ViewId);

		public override string ToString() => ViewId.Length == 0 ? Type.Name : $"{Type.Name}#{ViewId}";
	}
}
