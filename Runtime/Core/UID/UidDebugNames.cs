using System.Collections.Generic;
using System.Diagnostics;

namespace AK.Core
{
	/// <summary>
	/// A Uid in a log line is 32 hex characters and means nothing to a human. This map
	/// gives every identity the registry has seen a readable name in the editor and in
	/// development builds. Compiled to nothing in release: every call site is
	/// <see cref="ConditionalAttribute"/>-gated and the storage is never allocated.
	/// </summary>
	public static class UidDebugNames
	{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
		private static readonly Dictionary<Uid, string> _names = new();
#endif

		[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
		public static void Register(Uid id, string name)
		{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			if (id.IsSet && !string.IsNullOrEmpty(name)) _names[id] = name;
#endif
		}

		[Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
		public static void Register(UID asset)
		{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			if (asset != null) Register(asset.Id, asset.name);
#endif
		}

		/// <summary>"Name (7f3a1b2c)" when known, else the short hex. Safe to call in release — returns the hex.</summary>
		public static string Describe(Uid id)
		{
			if (id.IsNone) return "none";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			if (_names.TryGetValue(id, out string name)) return $"{name} ({id.ToShortString()})";
#endif
			return id.ToShortString();
		}

		public static string Describe<T>(Uid<T> id) where T : UID => Describe(id.Value);
	}
}
