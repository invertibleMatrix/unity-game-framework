using System;
using System.Collections.Generic;
using UnityEditor;

namespace AK.Core.Editor
{
	/// <summary>
	/// Project-wide identity index for the editor: Uid → asset GUIDs. Built lazily from one
	/// AssetDatabase query and invalidated by the identity authority whenever assets are
	/// created, imported, moved, or deleted. Every editor tool that must answer "who owns
	/// this Uid?" — collision detection, the drawer's picker, the inspector window, the
	/// build validator — reads from here rather than re-scanning the project.
	///
	/// Values are asset GUIDs, not object references, so the index never pins assets in memory.
	/// </summary>
	public static class UidEditorIndex
	{
		public readonly struct Entry
		{
			public readonly string AssetGuid;
			public readonly string AssetPath;
			public readonly string TypeName;

			public Entry(string assetGuid, string assetPath, string typeName)
			{
				AssetGuid = assetGuid;
				AssetPath = assetPath;
				TypeName  = typeName;
			}
		}

		private static Dictionary<Uid, List<Entry>> _byId;
		private static Dictionary<string, Uid>      _byGuid;
		private static List<string>                 _withoutIdentity;
		private static int                          _version;

		public static event Action Rebuilt;

		/// <summary>Increments on every rebuild; cache consumers compare against it.</summary>
		public static int Version
		{
			get
			{
				EnsureBuilt();
				return _version;
			}
		}

		public static void Invalidate()
		{
			_byId            = null;
			_byGuid          = null;
			_withoutIdentity = null;
		}

		public static bool TryGetOwners(Uid id, out IReadOnlyList<Entry> owners)
		{
			EnsureBuilt();
			if (_byId.TryGetValue(id, out var list))
			{
				owners = list;
				return true;
			}

			owners = Array.Empty<Entry>();
			return false;
		}

		public static bool IsOwned(Uid id)
		{
			EnsureBuilt();
			return _byId.ContainsKey(id);
		}

		public static bool TryGetIdentity(string assetGuid, out Uid id)
		{
			EnsureBuilt();
			return _byGuid.TryGetValue(assetGuid, out id);
		}

		/// <summary>Uids owned by more than one asset. The set the collision resolver works from.</summary>
		public static IEnumerable<KeyValuePair<Uid, IReadOnlyList<Entry>>> Collisions()
		{
			EnsureBuilt();
			foreach (var kv in _byId)
			{
				if (kv.Value.Count > 1) yield return new KeyValuePair<Uid, IReadOnlyList<Entry>>(kv.Key, kv.Value);
			}
		}

		public static IReadOnlyList<string> AssetsWithoutIdentity
		{
			get
			{
				EnsureBuilt();
				return _withoutIdentity;
			}
		}

		public static IEnumerable<KeyValuePair<Uid, IReadOnlyList<Entry>>> All()
		{
			EnsureBuilt();
			foreach (var kv in _byId)
			{
				yield return new KeyValuePair<Uid, IReadOnlyList<Entry>>(kv.Key, kv.Value);
			}
		}

		public static int Count
		{
			get
			{
				EnsureBuilt();
				return _byId.Count;
			}
		}

		private static void EnsureBuilt()
		{
			if (_byId != null) return;

			_byId            = new Dictionary<Uid, List<Entry>>();
			_byGuid          = new Dictionary<string, Uid>();
			_withoutIdentity = new List<string>();

			foreach (string guid in AssetDatabase.FindAssets("t:UID"))
			{
				string path  = AssetDatabase.GUIDToAssetPath(guid);
				var    asset = AssetDatabase.LoadAssetAtPath<UID>(path);
				if (asset == null) continue;

				if (!asset.HasIdentity)
				{
					_withoutIdentity.Add(path);
					continue;
				}

				var entry = new Entry(guid, path, asset.GetType().Name);
				if (!_byId.TryGetValue(asset.Id, out var owners))
				{
					owners = new List<Entry>(1);
					_byId[asset.Id] = owners;
				}

				owners.Add(entry);
				_byGuid[guid] = asset.Id;
			}

			_version++;
			Rebuilt?.Invoke();
		}
	}
}
