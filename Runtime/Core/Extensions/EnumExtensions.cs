using System;
using System.Collections.Generic;

namespace AK.Core.Extensions
{
    /// <summary>
    /// Enum names, cached per enum type at its first use. Names are matched in any case,
    /// ordinally; of two names that differ only in case, the one <see cref="Enum.GetNames"/>
    /// lists first is matched. A value with several names goes by the first one listed.
    /// </summary>
    public static class EnumExtensions
    {
        public static bool TryGetEnum<T>(this string enumString, out T result) where T : struct, Enum
        {
            if (enumString == null)
            {
                result = default;
                return false;
            }

            if (NameCache<T>.Values.TryGetValue(enumString, out result))
            {
                return true;
            }

            // Numbers, and combinations of flags.
            return Enum.TryParse(enumString, true, out result);
        }

        public static T GetEnum<T>(this string enumString) where T : struct, Enum
        {
            bool success = TryGetEnum(enumString, out T result);
            if (!success)
            {
                throw enumString == null ?
                    new ArgumentNullException(nameof(enumString)) :
                    new ArgumentException($"EnumExtensions.GetEnum<T>(this string enumString) | '{enumString}' is not a valid name for enum '{typeof(T).Name}'!");
            }
            return result;
        }

        /// <summary>One name per value: the first one listed.</summary>
        public static ICollection<string> GetNames<T>() where T : struct, Enum
        {
            return NameCache<T>.Names.Values;
        }

        public static string GetName<T>(this T @enum) where T : struct, Enum
        {
            if (NameCache<T>.Names.TryGetValue(@enum, out string result))
            {
                return result;
            }

            return @enum.ToString();
        }

        public static string GetLowerName<T>(this T @enum) where T : struct, Enum
        {
            if (NameCache<T>.LowerNames.TryGetValue(@enum, out string result))
            {
                return result;
            }

            return @enum.ToString().ToLowerInvariant();
        }

        private static class NameCache<T> where T : struct, Enum
        {
            // By value: its first name listed, as declared and in lower case.
            public static readonly Dictionary<T, string> Names;
            public static readonly Dictionary<T, string> LowerNames;

            // By name, in any case: the value of the first name listed that matches.
            public static readonly Dictionary<string, T> Values;

            static NameCache()
            {
                string[] names = Enum.GetNames(typeof(T));
                Names      = new Dictionary<T, string>(names.Length);
                LowerNames = new Dictionary<T, string>(names.Length);
                Values     = new Dictionary<string, T>(names.Length, StringComparer.OrdinalIgnoreCase);

                foreach (string name in names)
                {
                    T value = Enum.Parse<T>(name);
                    if (Names.TryAdd(value, name))
                    {
                        LowerNames.Add(value, name.ToLowerInvariant());
                    }

                    Values.TryAdd(name, value);
                }
            }
        }
    }
}
