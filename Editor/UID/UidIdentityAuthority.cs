using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AK.Core.Editor
{
	/// <summary>
	/// The only code that writes identity onto a UID asset. Two phases cover the two ways an
	/// asset can appear:
	///
	/// Phase 1 — creation inside the editor. <see cref="AssetModificationProcessor.OnWillCreateAsset"/>
	/// fires before the file is written, so a freshly created asset gets its Uid before the
	/// first save and its YAML is never on disk without one. A Ctrl+D duplicate arrives here
	/// too, carrying the source's Uid: it is re-minted immediately, because a duplicate made
	/// in the editor is unambiguously "a new thing that started as a copy".
	///
	/// Phase 2 — everything else. Files copied in from another branch, merged, or restored
	/// arrive through <see cref="AssetPostprocessor.OnPostprocessAllAssets"/> with whatever Uid
	/// they carry. Two assets sharing a Uid here is a genuine ambiguity: which one do the
	/// saves mean? It is never resolved silently. The collision is recorded and surfaced in
	/// the resolver window, the build validator refuses to ship it, and a human chooses the
	/// survivor (optionally recording a redirect from the loser).
	///
	/// Assets with no identity at all (hand-edited YAML, a struct that failed to parse) are
	/// minted on import with a log line — there is no ambiguity there, only a gap.
	/// </summary>
	public sealed class UidIdentityAuthority : AssetPostprocessor
	{
		private const string LogTag = "[UID]";

		private static readonly HashSet<string> _pendingCreatePaths = new();

		/// <summary>Uids that collided during the last import batch. Cleared when the resolver window handles them.</summary>
		public static readonly HashSet<Uid> UnresolvedCollisions = new();

		public static event Action CollisionsChanged;

		// ---------------------------------------------------------------- phase 1: creation

		private sealed class CreateHook : AssetModificationProcessor
		{
			private static void OnWillCreateAsset(string assetPath)
			{
				if (!assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return;
				_pendingCreatePaths.Add(assetPath);
			}
		}

		// ---------------------------------------------------------------- phase 2: import

		private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
		{
			bool touched = false;

			foreach (string path in imported)
			{
				if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;

				var asset = AssetDatabase.LoadAssetAtPath<UID>(path);
				if (asset == null) continue;

				touched = true;
				bool created = _pendingCreatePaths.Remove(path);
				ProcessImported(asset, path, created);
			}

			_pendingCreatePaths.Clear();

			if (touched || deleted.Length > 0 || moved.Length > 0)
			{
				UidEditorIndex.Invalidate();
			}

			if (touched)
			{
				DetectCollisions();
			}
		}

		private static void ProcessImported(UID asset, string path, bool created)
		{
			if (!asset.HasIdentity)
			{
				Mint(asset, path, created ? "created" : "imported without identity");
				return;
			}

			if (!created) return;

			if (IsOwnedElsewhere(asset))
			{
				Mint(asset, path, "duplicated in editor");
			}
		}

		private static bool IsOwnedElsewhere(UID asset)
		{
			string ownGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));

			if (UidEditorIndex.TryGetOwners(asset.Id, out var owners))
			{
				foreach (var owner in owners)
				{
					if (owner.AssetGuid != ownGuid) return true;
				}
			}

			return false;
		}

		// ---------------------------------------------------------------- minting

		/// <summary>
		/// Assigns identity to an asset that has none, or must have a fresh one. Derived
		/// identities come from an enclosing UidNamespace; everything else is random.
		/// </summary>
		public static Uid Mint(UID asset, string path, string reason)
		{
			UidNamespace ns = asset is UidNamespace ? null : UidEditorUtility.FindNamespaceFor(path);

			Uid           id;
			UidProvenance provenance;
			string        source;

			if (ns != null)
			{
				string canonical = UidEditorUtility.CanonicalName(path);
				id         = ns.Derive(canonical);
				provenance = UidProvenance.Derived;
				source     = $"{ns.Path}/{canonical}";
			}
			else
			{
				id         = Uid.NewRandom();
				provenance = UidProvenance.Minted;
				source     = string.Empty;
			}

			Undo.RecordObject(asset, "Assign identity");
			asset.Editor_AssignIdentity(id, provenance, source);
			UidEditorUtility.SaveAsset(asset);
			UidEditorIndex.Invalidate();

			Debug.Log($"{LogTag} Assigned {provenance.ToString().ToLowerInvariant()} identity {id.ToShortString()} to '{asset.name}' ({reason}).", asset);
			return id;
		}

		/// <summary>
		/// Explicit re-mint chosen by a human in the collision resolver. Imported identities
		/// are refused: they belong to an external system and must be fixed there.
		/// </summary>
		public static bool Remint(UID asset, string reason)
		{
			if (asset == null) return false;

			if (asset.Provenance == UidProvenance.Imported)
			{
				Debug.LogError($"{LogTag} Refusing to re-mint '{asset.name}': its identity is Imported and owned by an external system.", asset);
				return false;
			}

			Mint(asset, AssetDatabase.GetAssetPath(asset), reason);
			return true;
		}

		/// <summary>
		/// Adopts an identity from an external system. Used by import pipelines (server catalogs,
		/// spreadsheets) so those rows and this asset agree by construction.
		/// </summary>
		public static void AdoptImported(UID asset, Uid id, string externalSource)
		{
			if (asset == null || id.IsNone) throw new ArgumentException("Asset and identity are required.");

			Undo.RecordObject(asset, "Adopt imported identity");
			asset.Editor_AssignIdentity(id, UidProvenance.Imported, externalSource ?? string.Empty);
			UidEditorUtility.SaveAsset(asset);
			UidEditorIndex.Invalidate();
		}

		// ---------------------------------------------------------------- collisions

		public static void DetectCollisions()
		{
			int before = UnresolvedCollisions.Count;
			UnresolvedCollisions.Clear();

			foreach (var collision in UidEditorIndex.Collisions())
			{
				UnresolvedCollisions.Add(collision.Key);
			}

			if (UnresolvedCollisions.Count > 0)
			{
				foreach (Uid id in UnresolvedCollisions)
				{
					UidEditorIndex.TryGetOwners(id, out var owners);
					var names = new List<string>(owners.Count);
					foreach (var owner in owners) names.Add(owner.AssetPath);

					Debug.LogError($"{LogTag} Identity {id.ToShortString()} is owned by {owners.Count} assets:\n  {string.Join("\n  ", names)}\nOpen Tools → UGFW → UID → Resolve Collisions to choose the survivor. Builds are blocked until resolved.");
				}
			}

			if (before != UnresolvedCollisions.Count || UnresolvedCollisions.Count > 0)
			{
				CollisionsChanged?.Invoke();
			}
		}

		public static bool HasUnresolvedCollisions
		{
			get
			{
				DetectCollisions();
				return UnresolvedCollisions.Count > 0;
			}
		}

		/// <summary>
		/// Resolves one collision: <paramref name="survivor"/> keeps the identity, every other
		/// owner is re-minted. When a redirect table is given, each re-minted asset's NEW identity
		/// is not redirected (it is new content); the OLD shared identity continues to mean the
		/// survivor, which is exactly what existing saves expect.
		/// </summary>
		public static void ResolveCollision(Uid id, UID survivor)
		{
			if (survivor == null || survivor.Id != id)
			{
				Debug.LogError($"{LogTag} Survivor must currently own identity {id.ToShortString()}.");
				return;
			}

			if (!UidEditorIndex.TryGetOwners(id, out var owners)) return;

			string survivorGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(survivor));
			var    losers       = new List<UID>();

			foreach (var owner in owners)
			{
				if (owner.AssetGuid == survivorGuid) continue;

				var loser = AssetDatabase.LoadAssetAtPath<UID>(owner.AssetPath);
				if (loser != null) losers.Add(loser);
			}

			foreach (UID loser in losers)
			{
				Remint(loser, $"lost collision on {id.ToShortString()} to '{survivor.name}'");
			}

			UidEditorIndex.Invalidate();
			DetectCollisions();
		}

		/// <summary>
		/// Batch-mode entry for CI: mints missing identities, reports collisions, returns false
		/// when any collision remains. Never re-mints a collided asset on its own — that choice
		/// stays with a human.
		/// </summary>
		public static bool RepairMissingAndReport()
		{
			int minted = 0;
			foreach (string path in new List<string>(UidEditorIndex.AssetsWithoutIdentity))
			{
				var asset = AssetDatabase.LoadAssetAtPath<UID>(path);
				if (asset == null) continue;

				Mint(asset, path, "audit repair");
				minted++;
			}

			if (minted > 0)
			{
				AssetDatabase.SaveAssets();
				Debug.Log($"{LogTag} Minted {minted} missing identit{(minted == 1 ? "y" : "ies")}.");
			}

			DetectCollisions();
			return UnresolvedCollisions.Count == 0;
		}
	}
}
