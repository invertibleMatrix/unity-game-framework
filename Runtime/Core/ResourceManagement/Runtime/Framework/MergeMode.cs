namespace AK.Core.ResourceManagement
{
	/// <summary>
	/// Options for merging the results of requests.
	/// If keys (A, B) mapped to results ([1,2,4],[3,4,5])...
	///  - UseFirst (or None) takes the results from the first key
	///  -- [1,2,4]
	///  - Union takes results of each key and collects items that matched any key.
	///  -- [1,2,3,4,5]
	///  - Intersection takes results of each key, and collects items that matched every key.
	///  -- [4]
	/// </summary>
	public enum MergeMode
	{
		/// <summary>
		/// Use to indicate that no merge should occur. The first set of results will be used.
		/// </summary>
		None = 0,

		/// <summary>
		/// Use to indicate that the merge should take the first set of results.
		/// </summary>
		UseFirst = 0,

		/// <summary>
		/// Use to indicate that the merge should take the union of the results.
		/// </summary>
		Union,

		/// <summary>
		/// Use to indicate that the merge should take the intersection of the results.
		/// </summary>
		Intersection
	}
}