using System;
using System.Collections.Generic;

namespace AK.Core.Extensions
{
    public static class EnumExtensions
    {
        public static bool TryGetEnum<T>(this string? enumString, out T result) where T : struct, Enum
        {
            if (enumString == null)
            {
                result = default;
                return false;
            }
            
            //ReverseValues dictionary is not case sensitive, so this will work for any case string input!
            if (LowerEnumNameCache<T>.ReverseValues.TryGetValue(enumString, out result))
            {
                return true;
            }

            return Enum.TryParse(enumString, true, out result);
        }

        public static T GetEnum<T>(this string? enumString) where T : struct, Enum
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

        public static ICollection<string> GetNames<T>() where T : Enum
        {
            return EnumNameCache<T>.Values.Values;
        }

        public static string GetName<T>(this T @enum) where T : Enum
        {
            if (EnumNameCache<T>.Values.TryGetValue(@enum, out string? result))
            {
                return result;
            }

            return Enum.GetName(typeof(T), @enum) ?? @enum.ToString();
        }

        public static string GetLowerName<T>(this T @enum) where T : struct, Enum
        {
            if (LowerEnumNameCache<T>.Values.TryGetValue(@enum, out string? result))
            {
                return result;
            }

            return (Enum.GetName(typeof(T), @enum) ?? @enum.ToString()).ToLowerInvariant();
        }
        
        private static class EnumNameCache<T> where T : Enum
        {
            public static readonly Dictionary<T, string> Values;
            
            static EnumNameCache()
            {
                //This is safe against multiple enum names having the same value by only mapping the first name per value
                var enumValues = (T[])Enum.GetValues(typeof(T));
                Values = new Dictionary<T, string>(enumValues.Length);
                for (int i = 0; i < enumValues.Length; i++)
                {
                    T enumVal = enumValues[i];
                    string enumName = Enum.GetName(typeof(T), enumVal)!;
                    Values.TryAdd(enumVal, enumName);
                }
            }
        }

        private static class LowerEnumNameCache<T> where T : struct, Enum
        {
            public static readonly Dictionary<T, string> Values;
            public static readonly Dictionary<string, T> ReverseValues;

            static LowerEnumNameCache()
            {
                //This is safe against multiple enum names having the same value by only mapping the first name per value
                string[] enumNames = Enum.GetNames(typeof(T));
                Values = new Dictionary<T, string>(enumNames.Length);
                ReverseValues = new Dictionary<string, T>(enumNames.Length, StringComparer.InvariantCultureIgnoreCase);
                for (int i = 0; i < enumNames.Length; i++)
                {
                    string enumName = enumNames[i];
                    T enumVal = Enum.Parse<T>(enumName);
                    enumName = enumName.ToLowerInvariant();
                    
                    Values.TryAdd(enumVal, enumName);
                    ReverseValues.Add(enumName, enumVal);
                }
            }
        }
    }
}
