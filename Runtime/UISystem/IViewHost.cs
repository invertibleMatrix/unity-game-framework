namespace AK.Systems
{
	/// <summary>
	/// What a view may ask of the system that owns it. Every <see cref="UIView"/> is attached to
	/// exactly one host and reaches the system only through this — never through the concrete
	/// <see cref="UISystem"/>. The public <see cref="IUISystem"/> surface is inherited, so
	/// <see cref="UIView.UISystem"/> hands a view author the same object they would inject.
	/// Everything the system does to a view goes the other way, through <see cref="IViewLifecycle"/>.
	/// </summary>
	public interface IViewHost : IUISystem
	{
		/// <summary>True while <paramref name="view"/> is registered and its GameObject is alive.</summary>
		bool IsRegistered(UIView view);

		/// <summary>Registers a pre-placed static child under <paramref name="parent"/>. Warns and ignores a view that is already registered.</summary>
		void RegisterStatic(UIView view, UIView parent);

		/// <summary>Shows the ShowOnStart static children of <paramref name="parent"/>. Called by the parent once its own show completes.</summary>
		void ShowStaticChildren(UIView parent);

		/// <summary>A view finished its show lifecycle, immediate and animated alike. Raises <see cref="IUISystem.ViewShown"/>.</summary>
		void NotifyShown(UIView view);

		/// <summary>A view was destroyed outside the close pipeline (scene unload, direct Destroy). Drops the system's bookkeeping for it.</summary>
		void NotifyDestroyedExternally(UIView view);
	}
}
