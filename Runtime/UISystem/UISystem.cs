using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Reflex.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AK.Systems
{
	/// <summary>
	/// The UI system's scene presence and public face. Views with a <see cref="UIViewChannel"/>
	/// are screens: each gets its own Canvas and lives on a per-channel stack. Views without
	/// one are fragments: they live inside a parent view and are tracked in that parent's
	/// history. The work is done by the show and close pipelines over a shared registry,
	/// screen stacks, fragment histories and view factory; this class wires them together,
	/// forwards <see cref="IUISystem"/>, and answers what a view may ask of its system
	/// through <see cref="IViewHost"/>.
	/// </summary>
	public sealed class UISystem : MonoBehaviour, IViewHost, IDisposable
	{
		[SerializeField] private UIViewRepository _repository;
		[SerializeField] private Transform        _viewsContainer;
		[SerializeField] private Camera           _uiCamera;
		[SerializeField] private bool             _spawnDefaultOverlayView = true;
		[SerializeField] private bool             _ensureEventSystem       = true;

		[Inject] private Container _diContainer;

		private ViewRegistry      _registry;
		private ScreenStacks      _screens;
		private FragmentHistories _histories;
		private ViewFactory       _factory;
		private ShowPipeline      _show;
		private ClosePipeline     _close;

		public event Action<UIView> ViewShown;

		// =================================================================
		// LIFECYCLE
		// =================================================================

		private void Awake()
		{
			EnsureInitialized();
		}

		/// <summary>Awake's body, callable from edit-mode tests where Unity does not run Awake. Idempotent.</summary>
		internal void EnsureInitialized()
		{
			if (_registry != null) return;

			_registry = new ViewRegistry();
			_screens = new ScreenStacks(_uiCamera);
			_histories = new FragmentHistories();
			_factory = new ViewFactory(_repository, _viewsContainer, _diContainer);
			_close = new ClosePipeline(_registry, _screens, _histories, _factory);
			_show = new ShowPipeline(this, _registry, _screens, _histories, _factory, _close);

			if (_ensureEventSystem && FindFirstObjectByType<EventSystem>() == null)
			{
				var go = new GameObject("EventSystem");
				go.transform.SetParent(_viewsContainer);
				go.AddComponent<EventSystem>();
				go.AddComponent<StandaloneInputModule>();
			}

			if (_spawnDefaultOverlayView)
			{
				Show<UIViewOverlay>();
			}
		}

		public void Dispose()
		{
			_factory?.Clear();
		}

		// =================================================================
		// IUISystem — SHOW
		// =================================================================

		public TView Show<TView>(in ShowOptions options = default, Action<TView> onInit = null) where TView : UIView
		{
			return Show(typeof(TView), options, onInit);
		}

		public TView Show<TView>(Type type, in ShowOptions options = default, Action<TView> onInit = null) where TView : UIView
		{
			var (view, presentation) = _show.Show(type, options, onInit);
			if (view != null) presentation.Forget();
			return view;
		}

		public async UniTask<TView> ShowAsync<TView>(ShowOptions options = default, Action<TView> onInit = null, CancellationToken ct = default)
			where TView : UIView
		{
			var (view, presentation) = _show.Show(typeof(TView), options, onInit);
			if (view == null) return null;

			await presentation.AttachExternalCancellation(ct);
			return view;
		}

		UniTask<TView> IUISystem.ShowAsync<TView>(in ShowOptions options, Action<TView> onInit, CancellationToken ct)
			=> ShowAsync(options, onInit, ct);

		public void Show(UIView existing, in ShowOptions options = default)
		{
			_show.ShowExistingAsync(existing, options).Forget();
		}

		public UniTask ShowAsync(UIView existing, in ShowOptions options = default, CancellationToken ct = default)
		{
			return _show.ShowExistingAsync(existing, options, ct);
		}

		// =================================================================
		// IUISystem — CLOSE
		// =================================================================

		public void Close(UIView view, in CloseOptions options = default, Action onClosed = null)
		{
			CloseThenNotifyAsync(view, options.Immediate, onClosed).Forget();
		}

		public UniTask CloseAsync(UIView view, in CloseOptions options = default, CancellationToken ct = default)
		{
			return _close.CloseAsync(view, CloseContext.Normal, options.Immediate, ct);
		}

		/// <summary>The callback fires only when the close does something — a double close or an unregistered view "closed" nothing.</summary>
		private async UniTask CloseThenNotifyAsync(UIView view, bool immediate, Action onClosed)
		{
			bool willClose = view != null && _registry.TryGet(view, out var record) && !record.IsClosing;
			await _close.CloseAsync(view, CloseContext.Normal, immediate);
			if (willClose) onClosed?.Invoke();
		}

		// =================================================================
		// IUISystem — QUERY
		// =================================================================

		/// <summary>Screens on a channel stack first (top-most within a channel), then any registered instance that is not tearing down.</summary>
		public TView GetView<TView>(string viewId = "") where TView : UIView
		{
			var key = new ViewKey(typeof(TView), viewId);

			foreach (var stack in _screens.All)
			{
				foreach (var view in stack)
				{
					if (view != null && view.GetType() == key.Type && view.ViewId == key.ViewId)
						return view as TView;
				}
			}

			for (var record = _registry.FirstOfKind(key); record != null; record = record.NextOfKind)
			{
				if (record.IsAlive && !record.IsClosing)
					return record.Instance as TView;
			}

			return null;
		}

		public bool TryGetView<TView>(out TView view, string viewId = "") where TView : UIView
		{
			view = GetView<TView>(viewId);
			return view != null;
		}

		// =================================================================
		// IViewHost — what a view may ask of its system
		// =================================================================

		bool IViewHost.IsRegistered(UIView view) => _registry.IsRegistered(view);

		void IViewHost.RegisterStatic(UIView view, UIView parent)
		{
			if (_registry.Contains(view))
			{
				Debug.LogWarning($"Static view '{view.name}' is already registered.", view);
				return;
			}

			_registry.Add(new ViewRecord(view, parent, isStatic: true));
		}

		void IViewHost.ShowStaticChildren(UIView parent) => _show.ShowStaticChildren(parent);

		void IViewHost.NotifyShown(UIView view) => ViewShown?.Invoke(view);

		void IViewHost.NotifyDestroyedExternally(UIView view) => _close.NotifyDestroyedExternally(view);

		// =================================================================
		// DIAGNOSTICS — tests and editor tooling
		// =================================================================

		internal ViewRegistry      Registry  => _registry;
		internal ScreenStacks      Screens   => _screens;
		internal FragmentHistories Histories => _histories;
		internal ViewPool          Pool      => _factory?.Pool;

		internal int RegisteredViewCount => _registry.Count;

		internal int CountOfKind(Type type, string viewId = "") => _registry.CountOfKind(new ViewKey(type, viewId));
	}
}
