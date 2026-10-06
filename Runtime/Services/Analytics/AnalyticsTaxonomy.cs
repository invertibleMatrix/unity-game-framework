using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace AK.Services.Analytics
{
	/// <summary>
	/// A game's analytics vocabulary, which the providers read so the framework carries none of
	/// its own. <see cref="Dimensions"/> name the custom dimension slots and list their values,
	/// <see cref="FlatNamer"/> names design events for providers with flat event names, and
	/// <see cref="AppsFlyerSelector"/> picks the design events AppsFlyer gets. The
	/// <see cref="Empty"/> taxonomy names slots custom_01 and up, names design events by their
	/// parts, and sends AppsFlyer no design events. Immutable.
	/// </summary>
	public sealed class AnalyticsTaxonomy
	{
		private static readonly ReadOnlyCollection<string> NoValues = new(Array.Empty<string>());

		public static readonly AnalyticsTaxonomy Empty = new();

		private readonly AnalyticsDimension[] _dimensions;

		/// <param name="dimensions">The custom dimensions in slot order: the first is slot 1.</param>
		/// <param name="flatNamer">Names design events for flat-name providers; null for the default names.</param>
		/// <param name="appsFlyerSelector">Picks design events for AppsFlyer; null to send none.</param>
		/// <exception cref="ArgumentException">A dimension is null, or two share a name.</exception>
		public AnalyticsTaxonomy(
			IReadOnlyList<AnalyticsDimension> dimensions = null,
			IFlatEventNamer flatNamer = null,
			IAppsFlyerEventSelector appsFlyerSelector = null)
		{
			int count = dimensions?.Count ?? 0;
			_dimensions = new AnalyticsDimension[count];
			for (int i = 0; i < count; i++)
			{
				AnalyticsDimension dimension = dimensions[i]
					?? throw new ArgumentException($"Dimension {i + 1} is null.", nameof(dimensions));
				for (int j = 0; j < i; j++)
				{
					if (string.Equals(_dimensions[j].Name, dimension.Name, StringComparison.Ordinal))
					{
						throw new ArgumentException($"Dimensions {j + 1} and {i + 1} are both named '{dimension.Name}'.", nameof(dimensions));
					}
				}

				_dimensions[i] = dimension;
			}

			Dimensions = new ReadOnlyCollection<AnalyticsDimension>(_dimensions);
			FlatNamer = flatNamer;
			AppsFlyerSelector = appsFlyerSelector;
		}

		/// <summary>The custom dimensions in slot order: the first is slot 1.</summary>
		public IReadOnlyList<AnalyticsDimension> Dimensions { get; }

		/// <summary>Names design events for flat-name providers. Null means the default names.</summary>
		public IFlatEventNamer FlatNamer { get; }

		/// <summary>Picks the design events AppsFlyer gets. Null means none.</summary>
		public IAppsFlyerEventSelector AppsFlyerSelector { get; }

		/// <summary>
		/// The name of dimension <paramref name="slot"/> (1 is the first): its dimension's name, or
		/// custom_01 style for a slot with no dimension.
		/// </summary>
		public string DimensionName(int slot)
		{
			return slot >= 1 && slot <= _dimensions.Length
				? _dimensions[slot - 1].Name
				: "custom_" + slot.ToString("00", CultureInfo.InvariantCulture);
		}

		/// <summary>The slot of the dimension named <paramref name="name"/>, matched exactly.</summary>
		public bool TryGetDimensionSlot(string name, out int slot)
		{
			for (int i = 0; i < _dimensions.Length; i++)
			{
				if (string.Equals(_dimensions[i].Name, name, StringComparison.Ordinal))
				{
					slot = i + 1;
					return true;
				}
			}

			slot = 0;
			return false;
		}

		/// <summary>The values dimension <paramref name="slot"/> may take; empty for a slot with no dimension.</summary>
		public IReadOnlyList<string> AllowedValues(int slot)
		{
			return slot >= 1 && slot <= _dimensions.Length
				? _dimensions[slot - 1].AllowedValues
				: NoValues;
		}
	}

	/// <summary>
	/// A custom dimension: a property of the player that every event carries, such as an age
	/// band. GameAnalytics accepts only values listed before it starts, so list every value the
	/// game sets. Immutable.
	/// </summary>
	public sealed class AnalyticsDimension
	{
		/// <exception cref="ArgumentException">The name is empty, or a value is null or empty.</exception>
		public AnalyticsDimension(string name, params string[] allowedValues)
		{
			if (string.IsNullOrEmpty(name))
			{
				throw new ArgumentException("The dimension name is empty.", nameof(name));
			}

			var values = allowedValues == null ? Array.Empty<string>() : new string[allowedValues.Length];
			for (int i = 0; i < values.Length; i++)
			{
				if (string.IsNullOrEmpty(allowedValues[i]))
				{
					throw new ArgumentException($"Value {i} of dimension '{name}' is empty.", nameof(allowedValues));
				}

				values[i] = allowedValues[i];
			}

			Name = name;
			AllowedValues = new ReadOnlyCollection<string>(values);
		}

		public string Name { get; }

		public IReadOnlyList<string> AllowedValues { get; }
	}
}
