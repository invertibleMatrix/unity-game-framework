using System;

namespace AK.Examples.Models
{
    /// <summary>
    /// Serialization helper for polymorphic CurrencyModel persistence.
    /// Stores the concrete type name and JSON data for deserialization.
    /// </summary>
    [Serializable]
    public class SerializableCurrency
    {
        public string TypeName;
        public string Data;
    }
}
