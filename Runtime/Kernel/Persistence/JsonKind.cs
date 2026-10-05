namespace AK.Kernel.Persistence
{
	/// <summary>The kind of a JSON value.</summary>
	public enum JsonKind : byte
	{
		/// <summary>No value, such as a member that isn't there.</summary>
		None = 0,

		Object,
		Array,
		String,

		/// <summary>A number, including the NaN, Infinity and -Infinity tokens Unity's JsonUtility writes for non-finite floats.</summary>
		Number,

		/// <summary><c>true</c> or <c>false</c>.</summary>
		Boolean,

		Null,
	}
}
