namespace AK.Systems
{
	/// <summary>
	/// Framework-internal cascade vocabulary: how fatal a close is, so the teardown
	/// pipeline knows whether static children may hide-and-survive or must die.
	/// Deliberately absent from the public Close API — callers always mean Normal;
	/// the cascade supplies ParentDestroyed itself when a host is torn down.
	/// </summary>
	internal enum CloseContext
	{
		/// <summary>
		/// Normal close. Static fragments are hidden, dynamic fragments are destroyed.
		/// The only context the public close API uses.
		/// </summary>
		Normal,

		/// <summary>
		/// Parent screen or fragment is being destroyed. All child fragments are destroyed regardless of static/dynamic.
		/// Supplied by the teardown cascade — never by callers.
		/// </summary>
		ParentDestroyed,

		/// <summary>
		/// Explicit destroy request (e.g., cleanup, pooling). Fragment is destroyed.
		/// Latent capability for framework teardown tools — no public entry point passes it today.
		/// </summary>
		ForceDestroy
	}
}
