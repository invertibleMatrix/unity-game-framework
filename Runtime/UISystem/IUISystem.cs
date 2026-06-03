using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace AK.Systems
{
	/// <summary>
	/// Unified interface for the V2 UI system.
	/// Replaces both IUISystem and IFragmentSystem from V1.
	/// 
	/// Key design:
	/// - Show() returns the view immediately (synchronous instantiation, fire-and-forget animation).
	/// - ShowAsync() returns UniTask that completes when the animation finishes.
	/// - Views with UIChannel component act as "screens" (own Canvas, channel stacking).
	/// - Views without UIChannel act as "fragments" (live inside a parent view).
	/// </summary>
	public interface IUISystem
	{
		// =====================================================================
		// SHOW — Fire-and-forget (animation runs in background, no compiler warnings)
		// =====================================================================

		/// <summary>
		/// Shows a view of the specified type. Instantiates immediately and returns the reference.
		/// Animation runs in the background — no need to await or call Forget().
		///
		/// If the view has a UIChannel component, it acts as a screen (pushed onto channel stack).
		/// If not, it acts as a fragment (shown inside the given parent or the best available screen).
		/// If a static registration already exists for this type+parent, it reuses that instance.
		/// </summary>
		/// <param name="context">Optional context data.</param>
		/// <param name="parent">Optional explicit parent view. If null, auto-finds the best host for fragments.</param>
		/// <param name="viewId">Optional variant ID for multi-variant prefabs.</param>
		/// <param name="stackBehaviour">Optional override for stack behaviour.</param>
		/// <param name="onInit">Called after instantiation but before animation — set up the view here (replaces V1's onPrepare).</param>
		TView Show<TView>(UIContext context = null,
		                  UIView parent = null,
		                  string viewId = "",
		                  UIChannel? channelOverride = null,
		                  ViewStackBehaviour? stackBehaviour = null,
		                  Action<TView> onInit = null)
			where TView : UIView;

		/// <summary>
		/// Shows a view using a Type parameter. Fire-and-forget.
		/// </summary>
		TView Show<TView>(Type type,
		                  UIContext context = null,
		                  UIView parent = null,
		                  string viewId = "",
		                  UIChannel? channelOverride = null,
		                  ViewStackBehaviour? stackBehaviour = null,
		                  Action<TView> onInit = null)
			where TView : UIView;

		// =====================================================================
		// SHOW ASYNC — Awaitable (completes when animation finishes)
		// =====================================================================

		/// <summary>
		/// Shows a view and awaits until the show animation completes.
		/// The view is instantiated immediately — the task represents the animation lifecycle.
		/// </summary>
		UniTask<TView> ShowAsync<TView>(UIContext context = null,
		                                UIView parent = null,
		                                string viewId = "",
		                                UIChannel? channelOverride = null,
		                                ViewStackBehaviour? stackBehaviour = null,
		                                Action<TView> onInit = null,
		                                CancellationToken ct = default)
			where TView : UIView;

		/// <summary>
		/// Shows a view using a Type parameter and awaits the animation.
		/// </summary>
		UniTask<TView> ShowAsync<TView>(Type type,
		                                UIContext context = null,
		                                UIView parent = null,
		                                string viewId = "",
		                                UIChannel? channelOverride = null,
		                                ViewStackBehaviour? stackBehaviour = null,
		                                Action<TView> onInit = null,
		                                CancellationToken ct = default)
			where TView : UIView;

		// =====================================================================
		// CLOSE
		// =====================================================================

		/// <summary>
		/// Closes a view. Fire-and-forget — animation runs in background.
		/// </summary>
		void Close(UIView view, CloseContext context = CloseContext.Normal, Action onClose = null);

		/// <summary>
		/// Closes a view and awaits until the close animation completes.
		/// </summary>
		UniTask CloseAsync(UIView view, CloseContext context = CloseContext.Normal, CancellationToken ct = default);

		// =====================================================================
		// NAVIGATION
		// =====================================================================

		/// <summary>
		/// Navigates back to the previous fragment in the specified parent's history stack.
		/// </summary>
		void GoBack(UIView parentView);

		/// <summary>
		/// Navigates back and awaits the transition animation.
		/// </summary>
		UniTask GoBackAsync(UIView parentView, CancellationToken ct = default);

		// =====================================================================
		// QUERY
		// =====================================================================

		/// <summary>
		/// Gets an existing active view of the specified type.
		/// </summary>
		TView GetView<TView>(string viewId = "") where TView : UIView;

		// =====================================================================
		// RAPID SHOW/CLOSE — For tooltips and frequently-reused views
		// =====================================================================

		/// <summary>
		/// Shows a view instantly without animation. Useful for tooltips that need to jump
		/// between positions rapidly. Skips animation but still runs lifecycle hooks.
		/// </summary>
		TView ShowImmediate<TView>(UIContext context = null,
		                           UIView parent = null,
		                           string viewId = "",
		                           UIChannel? channelOverride = null,
		                           ViewStackBehaviour? stackBehaviour = null,
		                           Action<TView> onInit = null)
			where TView : UIView;

		/// <summary>
		/// Closes a view instantly without animation. Useful for tooltips that need to
		/// relocate quickly. Skips animation but still runs lifecycle hooks.
		/// </summary>
		void CloseImmediate(UIView view, CloseContext context = CloseContext.Normal);

		void DisplayToast(string text);

		void DisplayBanner(string text, string variantId = "");
	}
}