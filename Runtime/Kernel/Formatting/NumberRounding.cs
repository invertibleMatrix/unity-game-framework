namespace AK.Kernel.Formatting
{
	/// <summary>How a value is rounded to the digits that are shown.</summary>
	public enum NumberRounding : byte
	{
		/// <summary>To the nearest; halves go away from zero. 1.25 → 1.3, -1.25 → -1.3.</summary>
		Nearest = 0,

		/// <summary>Never above the value: toward negative infinity. 1.29 → 1.2, -1.21 → -1.3.</summary>
		Down = 1,

		/// <summary>Never below the value: toward positive infinity. 1.21 → 1.3, -1.29 → -1.2.</summary>
		Up = 2,
	}
}
