namespace AK.Kernel.Persistence
{
	/// <summary>What <see cref="JsonEnvelope.Inspect"/> found a text to be.</summary>
	public enum JsonShape : byte
	{
		/// <summary>One well-formed JSON object.</summary>
		Object = 0,

		/// <summary>One well-formed JSON value that isn't an object.</summary>
		NotAnObject,

		/// <summary>
		/// Not one well-formed JSON value: bad syntax, cut short, followed by more text, or
		/// nested deeper than <see cref="JsonEnvelope.MaxDepth"/>.
		/// </summary>
		Malformed,
	}
}
