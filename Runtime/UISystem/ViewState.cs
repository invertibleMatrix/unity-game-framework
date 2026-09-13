namespace AK.Systems
{
	/// <summary>
	/// Where a view is in its show/hide lifecycle. The show and close hooks fire only on the
	/// transitions between these states, which is what makes teardown idempotent and keeps
	/// <c>RegisterResources</c>/<c>UnRegisterResources</c> paired: resources are registered in
	/// every state except <see cref="Hidden"/>. Whether the view is currently drawn is a
	/// separate fact (<see cref="UIView.IsVisible"/>) — a paused view may or may not be.
	/// </summary>
	public enum ViewState : byte
	{
		/// <summary>Not showing and holding no resources: fresh, pooled, closed, or its entrance was cancelled.</summary>
		Hidden,

		/// <summary>OnPrepareShow and RegisterResources have run; the entrance is playing.</summary>
		Showing,

		/// <summary>OnShow has run.</summary>
		Shown,

		/// <summary>Covered by a view above: OnPause has run and input is blocked.</summary>
		Paused,

		/// <summary>OnPrepareHide has run; the exit is playing. OnHide and UnRegisterResources follow when it settles.</summary>
		Hiding,
	}
}
