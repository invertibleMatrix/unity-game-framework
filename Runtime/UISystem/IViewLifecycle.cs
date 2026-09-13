using System.Threading;
using Cysharp.Threading.Tasks;
using Reflex.Core;

namespace AK.Systems
{
	/// <summary>Which hide a view is asked to perform.</summary>
	internal enum HideMode : byte
	{
		/// <summary>Closing: OnPrepareHide → [animation] → OnHide → UnRegisterResources.</summary>
		Close,

		/// <summary>Covered by a view above: animation only. Resources stay registered and no hooks run.</summary>
		Pause,
	}

	/// <summary>
	/// Everything the system does to a view. <see cref="UIView"/> implements this explicitly, so
	/// none of it shows on the type a view author subclasses; the system reaches it through
	/// <see cref="UIViewLifecycleExtensions.Lifecycle"/>. This is the system→view coupling made
	/// visible and one-directional — a view only ever calls back through <see cref="IViewHost"/>.
	/// </summary>
	internal interface IViewLifecycle
	{
		/// <summary>Injects dependencies, binds the view to its host and parent, caches components. Once per (re)use.</summary>
		void Attach(IViewHost host, UIView parent, Container container);

		/// <summary>Attaches and registers this view's pre-placed static children with the host, recursively.</summary>
		void AttachStaticChildren(Container container);

		/// <summary>
		/// Forced settle before destroy or pool: cancels animation, runs the close hooks if they
		/// have not run yet, releases resources. Idempotent.
		/// </summary>
		void Teardown();

		/// <summary>SetActive → OnPrepareShow → RegisterResources (once) → [animation] → OnShow → host notified.</summary>
		UniTask ShowAsync(bool immediate, CancellationToken ct);

		/// <summary>
		/// <see cref="HideMode.Close"/>: OnPrepareHide → [animation] → SetActive(false) → OnHide → UnRegisterResources.
		/// <see cref="HideMode.Pause"/>: [animation] → SetActive(false).
		/// </summary>
		UniTask HideAsync(HideMode mode, bool immediate, CancellationToken ct);

		/// <summary>Uncovered after a <see cref="HideMode.Pause"/> hide: SetActive → [animation]. No hooks.</summary>
		UniTask ResumeAsync(bool immediate, CancellationToken ct);

		/// <summary>Alpha to zero, so a view about to animate in — or one paused without animation — is not drawn.</summary>
		void Conceal();

		/// <summary>Covered by a view above: input off, then <see cref="UIView.OnPause"/>.</summary>
		void Pause();

		/// <summary>Uncovered: input on, then <see cref="UIView.OnResume"/>.</summary>
		void Resume();

		/// <summary>Per-show stack behaviour override; null restores the serialized one.</summary>
		void OverrideStackBehaviour(ViewStackBehaviour? behaviour);

		/// <summary>Arms a pending ShowOnStart delay. The token is cancelled by every close and teardown path.</summary>
		CancellationToken ArmDelayedStart();
	}

	internal static class UIViewLifecycleExtensions
	{
		/// <summary>The system-facing side of a view. The one place the explicit-interface cast lives.</summary>
		internal static IViewLifecycle Lifecycle(this UIView view) => view;
	}
}
