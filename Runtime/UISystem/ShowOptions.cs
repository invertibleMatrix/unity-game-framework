using System;

namespace AK.Systems
{
	/// <summary>
	/// How a show negotiates with what is already on the stack.
	/// </summary>
	public enum ShowMode : byte
	{
		/// <summary>
		/// Default. Pushes onto the parent's history and animates at once. Sibling fragments do
		/// not negotiate: the new view's stack behaviour is not applied to whatever is below.
		/// </summary>
		Parallel = 0,

		/// <summary>
		/// Waits for any show already running on the same parent, then applies the new view's
		/// stack behaviour (pause / hide / close) to the view below before animating in.
		/// Screens always behave this way regardless of the mode requested.
		/// </summary>
		Serialized = 1,

		/// <summary>
		/// No animation: the view snaps visible and the view below is paused/hidden instantly.
		/// Lifecycle hooks still run. For tooltips and anything that relocates rapidly.
		/// </summary>
		Immediate = 2,
	}

	/// <summary>
	/// Everything a show can be told, in one value. Build with the object initializer or the
	/// fluent helpers; every field has a meaningful default so <c>default</c> is a valid,
	/// plain show.
	///
	/// <code>
	/// ui.Show&lt;UIViewShop&gt;();
	/// ui.Show&lt;UIViewShop&gt;(new ShowOptions { Context = ctx, Parent = this });
	/// ui.Show&lt;UIViewShop&gt;(ShowOptions.Under(this, ctx).Serialized(ViewStackBehaviour.HideBelow));
	/// </code>
	/// </summary>
	public readonly struct ShowOptions
	{
		/// <summary>Data handed to the view via <see cref="UIView.SetContext"/>. May be null.</summary>
		public readonly UIContext Context;

		/// <summary>Host for a fragment. Null lets the system pick the top-most screen.</summary>
		public readonly UIView Parent;

		/// <summary>Variant id for multi-variant prefabs. Empty selects the default variant.</summary>
		public readonly string ViewId;

		/// <summary>Channel a screen is pushed onto instead of the one on its <see cref="UIViewChannel"/>.</summary>
		public readonly UIChannel? Channel;

		/// <summary>
		/// Overrides the view's serialized stack behaviour for this show. Setting it implies
		/// <see cref="ShowMode.Serialized"/> unless <see cref="Mode"/> is <see cref="ShowMode.Immediate"/>.
		/// </summary>
		public readonly ViewStackBehaviour? StackBehaviour;

		public readonly ShowMode Mode;

		public ShowOptions(UIContext context = null, UIView parent = null, string viewId = null, UIChannel? channel = null,
		                   ViewStackBehaviour? stackBehaviour = null, ShowMode mode = ShowMode.Parallel)
		{
			Context = context;
			Parent = parent;
			ViewId = viewId ?? string.Empty;
			Channel = channel;
			StackBehaviour = stackBehaviour;
			Mode = stackBehaviour != null && mode == ShowMode.Parallel ? ShowMode.Serialized : mode;
		}

		public string EffectiveViewId => ViewId ?? string.Empty;

		public bool IsImmediate  => Mode == ShowMode.Immediate;
		public bool IsSerialized => Mode == ShowMode.Serialized;

		// ---- factories -------------------------------------------------------------------

		public static ShowOptions With(UIContext context) => new(context);

		public static ShowOptions Under(UIView parent, UIContext context = null) => new(context, parent);

		public static ShowOptions Variant(string viewId, UIContext context = null) => new(context, viewId: viewId);

		// ---- fluent copies ---------------------------------------------------------------

		public ShowOptions WithContext(UIContext context)   => new(context, Parent, ViewId, Channel, StackBehaviour, Mode);
		public ShowOptions WithParent(UIView parent)        => new(Context, parent, ViewId, Channel, StackBehaviour, Mode);
		public ShowOptions WithViewId(string viewId)        => new(Context, Parent, viewId, Channel, StackBehaviour, Mode);
		public ShowOptions OnChannel(UIChannel channel)     => new(Context, Parent, ViewId, channel, StackBehaviour, Mode);
		public ShowOptions Immediate()                      => new(Context, Parent, ViewId, Channel, StackBehaviour, ShowMode.Immediate);

		/// <summary>Serialize behind pending shows on the parent and apply <paramref name="behaviour"/> (or the view's own) to the view below.</summary>
		public ShowOptions Serialized(ViewStackBehaviour? behaviour = null)
			=> new(Context, Parent, ViewId, Channel, behaviour ?? StackBehaviour, ShowMode.Serialized);
	}

	/// <summary>
	/// How a close runs. <c>default</c> is an animated close.
	/// </summary>
	public readonly struct CloseOptions
	{
		/// <summary>Skip the hide animation; lifecycle hooks still run.</summary>
		public readonly bool Immediate;

		public CloseOptions(bool immediate)
		{
			Immediate = immediate;
		}

		public static readonly CloseOptions Animated = default;
		public static readonly CloseOptions Now      = new(immediate: true);
	}
}
