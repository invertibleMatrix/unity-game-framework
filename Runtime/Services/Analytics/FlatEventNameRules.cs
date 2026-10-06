using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AK.Services.Analytics
{
	/// <summary>
	/// An <see cref="IFlatEventNamer"/> from a list of rules. The first rule whose prefix starts
	/// the id names the event and copies its listed parts into properties; an id no rule matches
	/// gets the default name. Immutable, so one instance can serve every provider.
	/// </summary>
	public sealed class FlatEventNameRules : IFlatEventNamer
	{
		private readonly FlatEventNameRule[] _rules;

		/// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
		/// <exception cref="ArgumentException">A rule is null.</exception>
		public FlatEventNameRules(params FlatEventNameRule[] rules)
		{
			if (rules == null)
			{
				throw new ArgumentNullException(nameof(rules));
			}

			_rules = new FlatEventNameRule[rules.Length];
			for (int i = 0; i < rules.Length; i++)
			{
				_rules[i] = rules[i] ?? throw new ArgumentException($"Rule {i} is null.", nameof(rules));
			}

			Rules = new ReadOnlyCollection<FlatEventNameRule>(_rules);
		}

		/// <summary>The rules, in the order they are tried.</summary>
		public IReadOnlyList<FlatEventNameRule> Rules { get; }

		public bool TryName(DesignEventParts parts, Dictionary<string, object> properties, out string eventName)
		{
			if (parts == null)
			{
				throw new ArgumentNullException(nameof(parts));
			}

			if (properties == null)
			{
				throw new ArgumentNullException(nameof(properties));
			}

			for (int i = 0; i < _rules.Length; i++)
			{
				FlatEventNameRule rule = _rules[i];
				if (parts.StartsWith(rule.Prefix))
				{
					rule.CopyParts(parts, properties);
					eventName = rule.EventName;
					return true;
				}
			}

			eventName = null;
			return false;
		}
	}

	/// <summary>
	/// One rule of <see cref="FlatEventNameRules"/>: ids starting with <see cref="Prefix"/> are
	/// named <see cref="EventName"/>, and each of <see cref="Properties"/> copies a part of the id
	/// into a property. Immutable.
	/// </summary>
	public sealed class FlatEventNameRule
	{
		private readonly PartProperty[] _properties;

		/// <param name="idPrefix">The leading parts to match, such as "level:start". Case is ignored.</param>
		/// <param name="eventName">The flat event name.</param>
		/// <param name="properties">The parts of the id to copy into properties.</param>
		/// <exception cref="ArgumentException">
		/// The prefix has no parts, the name is empty, or a property has a negative part or no name.
		/// </exception>
		public FlatEventNameRule(string idPrefix, string eventName, params PartProperty[] properties)
		{
			Prefix = DesignEventParts.Parse(idPrefix);
			if (Prefix.Count == 0)
			{
				throw new ArgumentException($"The prefix '{idPrefix}' has no parts.", nameof(idPrefix));
			}

			if (string.IsNullOrEmpty(eventName))
			{
				throw new ArgumentException("The event name is empty.", nameof(eventName));
			}

			EventName = eventName;
			_properties = properties == null ? Array.Empty<PartProperty>() : new PartProperty[properties.Length];
			for (int i = 0; i < _properties.Length; i++)
			{
				PartProperty property = properties[i];
				if (property.Part < 0 || string.IsNullOrEmpty(property.Name))
				{
					throw new ArgumentException($"Property {i} of '{eventName}' needs a part of 0 or more and a name.", nameof(properties));
				}

				_properties[i] = property;
			}

			Properties = new ReadOnlyCollection<PartProperty>(_properties);
		}

		public DesignEventParts Prefix { get; }

		public string EventName { get; }

		public IReadOnlyList<PartProperty> Properties { get; }

		// An id too short for a part leaves its property alone.
		internal void CopyParts(DesignEventParts parts, Dictionary<string, object> properties)
		{
			for (int i = 0; i < _properties.Length; i++)
			{
				PartProperty property = _properties[i];
				string value = parts.At(property.Part);
				if (value == null || (property.OnlyIfMissing && properties.ContainsKey(property.Name)))
				{
					continue;
				}

				properties[property.Name] = value;
			}
		}
	}

	/// <summary>
	/// Copies part <see cref="Part"/> of a design id (0 is the first) into the property
	/// <see cref="Name"/>. With <see cref="OnlyIfMissing"/>, an event parameter of that name wins.
	/// </summary>
	public readonly struct PartProperty
	{
		private PartProperty(int part, string name, bool onlyIfMissing)
		{
			Part = part;
			Name = name;
			OnlyIfMissing = onlyIfMissing;
		}

		/// <summary>Sets the property, replacing a parameter of the same name.</summary>
		public static PartProperty Set(int part, string name) => new(part, name, false);

		/// <summary>Sets the property unless the event has a parameter of that name.</summary>
		public static PartProperty SetIfMissing(int part, string name) => new(part, name, true);

		public int Part { get; }

		public string Name { get; }

		public bool OnlyIfMissing { get; }
	}
}
