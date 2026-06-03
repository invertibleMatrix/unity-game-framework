namespace AK.Systems
{
	/// <summary>
	/// Defines the context in which a fragment is being closed.
	/// This determines whether static fragments should be destroyed or just hidden.
	/// </summary>
	public enum CloseContext
	{
		/// <summary>
		/// Normal user-initiated close. Static fragments are hidden, dynamic fragments are destroyed.
		/// </summary>
		Normal,
		
		/// <summary>
		/// Parent screen or fragment is being destroyed. All child fragments are destroyed regardless of static/dynamic.
		/// </summary>
		ParentDestroyed,
		
		/// <summary>
		/// Explicit destroy request (e.g., cleanup, pooling). Fragment is destroyed.
		/// </summary>
		ForceDestroy
	}
}