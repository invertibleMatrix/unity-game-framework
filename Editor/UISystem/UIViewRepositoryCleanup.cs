using System;
using System.Collections.Generic;
using UnityEditor;

namespace AK.Systems.Editor
{
	/// <summary>
	/// Finds and removes the entries of a <see cref="UIViewRepository"/> that the UI system
	/// can't use: references to a view that no longer exists (its prefab was deleted, or the
	/// component removed), empty entries, and duplicates. A duplicate lists the very same view
	/// as an earlier entry, the same prefab and component, so the two entries are byte for byte
	/// the same. Prefab variants and copies are other prefabs and always stay, even when they
	/// look alike: a copy can carry its own view id.
	/// </summary>
	public static class UIViewRepositoryCleanup
	{
		/// <summary>The serialized name of the repository's view list.</summary>
		public const string ViewsProperty = "_views";

		public enum Problem : byte
		{
			/// <summary>Points at a view that no longer exists.</summary>
			Missing = 0,

			/// <summary>Points at nothing.</summary>
			Empty = 1,

			/// <summary>Lists the same view as an earlier entry.</summary>
			Duplicate = 2,
		}

		/// <summary>An entry a clean-up removes.</summary>
		public readonly struct Finding : IEquatable<Finding>
		{
			/// <summary>The entry's index in the list as it was found.</summary>
			public readonly int Index;

			public readonly Problem Problem;

			/// <summary>For a duplicate, the index of the earlier entry it repeats; otherwise -1.</summary>
			public readonly int DuplicateOf;

			public Finding(int index, Problem problem, int duplicateOf = -1)
			{
				Index       = index;
				Problem     = problem;
				DuplicateOf = duplicateOf;
			}

			public bool Equals(Finding other) =>
				Index == other.Index && Problem == other.Problem && DuplicateOf == other.DuplicateOf;

			public override bool Equals(object obj) => obj is Finding other && Equals(other);

			public override int GetHashCode()
			{
				unchecked
				{
					int hash = Index;
					hash = hash * 397 ^ (int)Problem;
					return hash * 397 ^ DuplicateOf;
				}
			}

			public override string ToString() =>
				DuplicateOf >= 0 ? $"{Index}: {Problem} of {DuplicateOf}" : $"{Index}: {Problem}";
		}

		/// <summary>
		/// Fills <paramref name="findings"/> with the entries a clean-up removes, in list order.
		/// Reads the serialized state: apply pending changes to <paramref name="repository"/> first.
		/// </summary>
		public static void Find(SerializedObject repository, List<Finding> findings)
		{
			if (findings == null)
			{
				throw new ArgumentNullException(nameof(findings));
			}

			SerializedProperty views = ViewsOf(repository);
			int count = views.arraySize;
			findings.Clear();

			// Entries for the same object share its instance id; the first of them stays.
			var firstById = new Dictionary<int, int>(count);
			for (int i = 0; i < count; i++)
			{
				SerializedProperty entry = views.GetArrayElementAtIndex(i);
				int id = entry.objectReferenceInstanceIDValue;

				if (id == 0)
				{
					findings.Add(new Finding(i, Problem.Empty));
				}
				else if (entry.objectReferenceValue == null)
				{
					findings.Add(new Finding(i, Problem.Missing));
				}
				else if (firstById.TryGetValue(id, out int first))
				{
					findings.Add(new Finding(i, Problem.Duplicate, first));
				}
				else
				{
					firstById.Add(id, i);
				}
			}
		}

		/// <summary>
		/// Removes the entries <see cref="Find"/> reports, as one undoable change. The rest keep
		/// their order and their exact references. <paramref name="removed"/> receives what went.
		/// </summary>
		public static void Remove(SerializedObject repository, List<Finding> removed)
		{
			Find(repository, removed);
			if (removed.Count == 0)
			{
				return;
			}

			SerializedProperty views = ViewsOf(repository);
			int count = views.arraySize;
			int kept = 0;
			int next = 0;

			for (int i = 0; i < count; i++)
			{
				if (next < removed.Count && removed[next].Index == i)
				{
					next++;
					continue;
				}

				if (kept != i)
				{
					views.GetArrayElementAtIndex(kept).objectReferenceInstanceIDValue =
						views.GetArrayElementAtIndex(i).objectReferenceInstanceIDValue;
				}

				kept++;
			}

			views.arraySize = kept;
			repository.ApplyModifiedProperties();
		}

		private static SerializedProperty ViewsOf(SerializedObject repository)
		{
			if (repository == null)
			{
				throw new ArgumentNullException(nameof(repository));
			}

			SerializedProperty views = repository.FindProperty(ViewsProperty);
			if (views == null || !views.isArray)
			{
				throw new ArgumentException($"{repository.targetObject} has no '{ViewsProperty}' list.", nameof(repository));
			}

			return views;
		}
	}
}
