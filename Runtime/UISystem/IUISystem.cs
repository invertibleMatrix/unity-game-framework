using System;
using System.Threading;
using AK.Kernel.Timing;
using Cysharp.Threading.Tasks;

namespace AK.Systems
{
	/// <summary>
	/// The UI system's public surface.
	///
	/// - <c>Show</c> instantiates (or reuses) synchronously and returns the view; the
	///   animation runs in the background. <c>ShowAsync</c> completes when it finishes.
	/// - A view with a <see cref="UIViewChannel"/> is a screen and lands on a channel stack;
	///   any other view is a fragment and lives in a parent's history stack.
	/// - Everything a show can be told travels in one <see cref="ShowOptions"/> value.
	/// </summary>
	public interface IUISystem
	{
		// =====================================================================
		// SHOW
		// =====================================================================

		/// <summary>
		/// Shows a view of <typeparamref name="TView"/>. Fire-and-forget: the view is returned
		/// immediately, its show animation runs in the background.
		/// </summary>
		/// <param name="onInit">Runs after instantiation and before any lifecycle hook.</param>
		TView Show<TView>(in ShowOptions options = default, Action<TView> onInit = null) where TView : UIView;

		/// <summary>Type-parameter variant for callers that only have a <see cref="Type"/>.</summary>
		TView Show<TView>(Type type, in ShowOptions options = default, Action<TView> onInit = null) where TView : UIView;

		/// <summary>Shows a view and completes when its show animation has finished.</summary>
		UniTask<TView> ShowAsync<TView>(in ShowOptions options = default, Action<TView> onInit = null, CancellationToken ct = default)
			where TView : UIView;

		/// <summary>
		/// Shows an already-registered instance — typically a pre-placed static fragment listed
		/// in its parent's Static Fragments. <see cref="ShowOptions.Parent"/> is ignored; the
		/// view's registered parent is used.
		/// </summary>
		void Show(UIView existing, in ShowOptions options = default);

		/// <summary>Awaitable form of <see cref="Show(UIView, in ShowOptions)"/>.</summary>
		UniTask ShowAsync(UIView existing, in ShowOptions options = default, CancellationToken ct = default);

		// =====================================================================
		// CLOSE
		// =====================================================================

		/// <summary>
		/// Closes a view. Fire-and-forget. Static children hide and survive; dynamic instances
		/// are destroyed or pooled. <paramref name="onClosed"/> fires only if the view was
		/// actually open.
		/// </summary>
		void Close(UIView view, in CloseOptions options = default, Action onClosed = null);

		/// <summary>Closes a view and completes when its hide animation has finished.</summary>
		UniTask CloseAsync(UIView view, in CloseOptions options = default, CancellationToken ct = default);

		// =====================================================================
		// QUERY
		// =====================================================================

		/// <summary>An open, non-closing view of the kind, or null. Screens on a channel stack win.</summary>
		TView GetView<TView>(string viewId = "") where TView : UIView;

		bool TryGetView<TView>(out TView view, string viewId = "") where TView : UIView;

		// =====================================================================
		// INPUT
		// =====================================================================

		/// <summary>
		/// Blocks pointer input while anyone holds it: the UI's, and the world's wherever game
		/// code checks for UI under the pointer. Shared by everything on this system
		/// (tutorials, spotlights, game code); each holder releases only its own hold.
		/// </summary>
		UIInputGate InputGate { get; }

		// =====================================================================
		// MEMORY
		// =====================================================================

		/// <summary>
		/// Destroys the closed views kept for reuse beyond <paramref name="keepPerKind"/> of each
		/// kind; zero empties the pool. The system trims to zero by itself when the OS reports low
		/// memory.
		/// </summary>
		void TrimPool(int keepPerKind = 0);

		// =====================================================================
		// TIME
		// =====================================================================

		/// <summary>
		/// The time the UI runs on: every view's entrance and exit, its background dim, and the
		/// delays before its static children show. Unscaled by default, so a game paused at
		/// timeScale 0 can still open and close its menus.
		/// </summary>
		TimeDomain TimeDomain { get; }

		// =====================================================================
		// EVENTS
		// =====================================================================

		/// <summary>After a view completes its show lifecycle (immediate and animated alike). Not raised on resume.</summary>
		event Action<UIView> ViewShown;
	}
}
