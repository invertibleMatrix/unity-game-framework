namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// The one rule for settings that remote config can override: the remote value wins when
	/// one is known, and the local setting holds otherwise. The variable's own default never
	/// overrides anything, so an unset <see cref="RemoteBool"/>, whose default is false, can't
	/// switch a feature off.
	/// </summary>
	public static class RemoteOverride
	{
		/// <summary>
		/// <paramref name="variable"/>'s remote value when it has one, otherwise
		/// <paramref name="local"/>. A missing variable leaves <paramref name="local"/>.
		/// </summary>
		public static T Resolve<T>(RemoteVariable<T> variable, T local) =>
			variable != null && variable.HasRemoteValue ? variable.Value : local;
	}
}
