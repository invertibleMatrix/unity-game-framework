using System;
using System.Collections.Generic;
using AK.Kernel.Collections;
using AK.Kernel.Threading;

namespace AK.Core
{
	/// <summary>
	/// An event bus whose events can be aimed at a target object and come from a source object.
	/// Besides listeners that hear every event of a type, a listener can hear only the events aimed
	/// at one target (<see cref="SubscribeToTarget{TEvent}"/>) or only those from one source
	/// (<see cref="SubscribeToSource{TEvent}"/>). An event reaches all three kinds of listener in
	/// one order: higher priority first, and in the order they subscribed within a priority.
	/// </summary>
	/// <inheritdoc/>
	/// <typeparam name="TObject">The type of targets and sources, such as <see cref="UnityEngine.GameObject"/>.</typeparam>
	public class GenericEventBus<TBaseEvent, TObject> : GenericEventBus<TBaseEvent>
	{
		public delegate void TargetedEventHandler<TEvent>(ref TEvent eventData, TObject target, TObject source);

		private static readonly EqualityComparer<TObject> ObjectComparer = EqualityComparer<TObject>.Default;

		public GenericEventBus() : this(default) { }

		/// <param name="defaultObject">The object that stands for no object. See <see cref="DefaultObject"/>.</param>
		public GenericEventBus(TObject defaultObject)
		{
			DefaultObject = defaultObject;
		}

		/// <summary>
		/// <para>The object that stands for no object: the target and source of an event raised
		/// without them.</para>
		/// Subscribing to it, or to null, as a target or a source subscribes to every event.
		/// </summary>
		public TObject DefaultObject { get; }

		public override bool Raise<TEvent>(in TEvent @event)
		{
			return Raise(@event, DefaultObject, DefaultObject);
		}

		/// <summary>
		/// Delivers <paramref name="event"/>, aimed at <paramref name="target"/> and from
		/// <paramref name="source"/>, after the event being delivered and those waiting before
		/// it, if any.
		/// </summary>
		/// <returns>Whether a listener consumed it, when it was delivered at once. False when it waits.</returns>
		public bool Raise<TEvent>(TEvent @event, TObject target, TObject source) where TEvent : TBaseEvent
		{
			if (!IsEventBeingRaised)
			{
				return RaiseImmediately(ref @event, target, source);
			}

			ThrowIfUnusable("GenericEventBus.Raise");

			TargetedChannel<TEvent> channel = GetChannel<TEvent, TargetedChannel<TEvent>>();
			channel.Waiting.Enqueue((@event, target, source));
			Wait(channel);
			return false;
		}

		public override bool RaiseImmediately<TEvent>(ref TEvent @event)
		{
			return RaiseImmediately(ref @event, DefaultObject, DefaultObject);
		}

		/// <summary>Delivers <paramref name="event"/> now, aimed at <paramref name="target"/> and from <paramref name="source"/>.</summary>
		/// <returns>Whether a listener consumed it.</returns>
		public bool RaiseImmediately<TEvent>(TEvent @event, TObject target, TObject source) where TEvent : TBaseEvent
		{
			return RaiseImmediately(ref @event, target, source);
		}

		/// <summary>Delivers <paramref name="event"/> now, aimed at <paramref name="target"/> and from <paramref name="source"/>.</summary>
		/// <returns>Whether a listener consumed it.</returns>
		public bool RaiseImmediately<TEvent>(ref TEvent @event, TObject target, TObject source)
			where TEvent : TBaseEvent
		{
			ThrowIfUnusable("GenericEventBus.RaiseImmediately");

			TargetedChannel<TEvent> channel = FindChannel<TEvent, TargetedChannel<TEvent>>();

			BeginRaise();
			try
			{
				return channel != null && channel.Deliver(this, ref @event, target, source);
			}
			finally
			{
				EndRaise();
			}
		}

		public override void SubscribeTo<TEvent>(EventHandler<TEvent> handler, float priority = 0)
		{
			AddListener<TEvent>(DefaultObject, handler, priority, bySource: false);
		}

		/// <summary>Subscribes <paramref name="handler"/> to every event of <typeparamref name="TEvent"/>.</summary>
		/// <param name="priority">Higher hears the event earlier. Equal priorities hear it in the order they subscribed.</param>
		public void SubscribeTo<TEvent>(TargetedEventHandler<TEvent> handler, float priority = 0)
			where TEvent : TBaseEvent
		{
			AddListener<TEvent>(DefaultObject, handler, priority, bySource: false);
		}

		public override void UnsubscribeFrom<TEvent>(EventHandler<TEvent> handler)
		{
			RemoveListener<TEvent>(DefaultObject, handler, bySource: false);
		}

		/// <summary>Removes one subscription of <paramref name="handler"/> to every event, the latest. Nothing happens when it has none.</summary>
		public void UnsubscribeFrom<TEvent>(TargetedEventHandler<TEvent> handler) where TEvent : TBaseEvent
		{
			RemoveListener<TEvent>(DefaultObject, handler, bySource: false);
		}

		/// <summary>Subscribes <paramref name="handler"/> to the events of <typeparamref name="TEvent"/> aimed at <paramref name="target"/>.</summary>
		/// <param name="priority">Higher hears the event earlier. Equal priorities hear it in the order they subscribed.</param>
		public void SubscribeToTarget<TEvent>(TObject target, TargetedEventHandler<TEvent> handler, float priority = 0)
			where TEvent : TBaseEvent
		{
			AddListener<TEvent>(target, handler, priority, bySource: false);
		}

		/// <summary>Removes one subscription of <paramref name="handler"/> to <paramref name="target"/>, the latest.</summary>
		public void UnsubscribeFromTarget<TEvent>(TObject target, TargetedEventHandler<TEvent> handler)
			where TEvent : TBaseEvent
		{
			RemoveListener<TEvent>(target, handler, bySource: false);
		}

		/// <summary>Subscribes <paramref name="handler"/> to the events of <typeparamref name="TEvent"/> from <paramref name="source"/>.</summary>
		/// <param name="priority">Higher hears the event earlier. Equal priorities hear it in the order they subscribed.</param>
		public void SubscribeToSource<TEvent>(TObject source, TargetedEventHandler<TEvent> handler, float priority = 0)
			where TEvent : TBaseEvent
		{
			AddListener<TEvent>(source, handler, priority, bySource: true);
		}

		/// <summary>Removes one subscription of <paramref name="handler"/> to <paramref name="source"/>, the latest.</summary>
		public void UnsubscribeFromSource<TEvent>(TObject source, TargetedEventHandler<TEvent> handler)
			where TEvent : TBaseEvent
		{
			RemoveListener<TEvent>(source, handler, bySource: true);
		}

		private bool IsNoObject(TObject obj) => obj == null || ObjectComparer.Equals(obj, DefaultObject);

		private void AddListener<TEvent>(TObject obj, Delegate handler, float priority, bool bySource) where TEvent : TBaseEvent
		{
			if (handler == null) throw new ArgumentNullException(nameof(handler));
			ThrowIfUnusable("GenericEventBus.SubscribeTo");

			TargetedChannel<TEvent> channel = GetChannel<TEvent, TargetedChannel<TEvent>>();
			Rank rank = NextRank(priority);

			if (IsNoObject(obj))
			{
				channel.All.Add(handler, rank);
				return;
			}

			Dictionary<TObject, RankedList<Delegate>> byObject = bySource ? channel.BySource : channel.ByTarget;
			if (!byObject.TryGetValue(obj, out RankedList<Delegate> listeners))
			{
				listeners = new RankedList<Delegate>();
				byObject.Add(obj, listeners);
			}

			listeners.Add(handler, rank);
		}

		private void RemoveListener<TEvent>(TObject obj, Delegate handler, bool bySource) where TEvent : TBaseEvent
		{
			MainThreadGuard.Assert("GenericEventBus.UnsubscribeFrom");

			TargetedChannel<TEvent> channel = FindChannel<TEvent, TargetedChannel<TEvent>>();
			if (channel == null || handler == null) return;

			if (IsNoObject(obj))
			{
				channel.All.RemoveLatest(handler);
				return;
			}

			Dictionary<TObject, RankedList<Delegate>> byObject = bySource ? channel.BySource : channel.ByTarget;
			if (byObject.TryGetValue(obj, out RankedList<Delegate> listeners) && listeners.RemoveLatest(handler) && listeners.Count == 0)
			{
				// A delivery in progress looks the list up at every step, so dropping it is safe.
				byObject.Remove(obj);
			}
		}

		private sealed class TargetedChannel<TEvent> : Channel where TEvent : TBaseEvent
		{
			// Each holds EventHandler<TEvent> and TargetedEventHandler<TEvent> listeners. Their ranks
			// come from one counter, so the three lists merge in subscription order.
			public readonly RankedList<Delegate>                       All      = new();
			public readonly Dictionary<TObject, RankedList<Delegate>> ByTarget = new(ObjectComparer);
			public readonly Dictionary<TObject, RankedList<Delegate>> BySource = new(ObjectComparer);

			public readonly Queue<(TEvent Event, TObject Target, TObject Source)> Waiting = new();

			/// <returns>Whether a listener consumed the event.</returns>
			public bool Deliver(GenericEventBus<TBaseEvent, TObject> bus, ref TEvent @event, TObject target, TObject source)
			{
				bool aimed = !bus.IsNoObject(target);
				bool sourced = !bus.IsNoObject(source);

				for (Rank last = Rank.BeforeAll; ;)
				{
					// The next listener after the last one called, from whichever list ranks it first.
					RankedList<Delegate> list = All;
					int next = All.IndexAfter(last);

					if (aimed && ByTarget.TryGetValue(target, out RankedList<Delegate> forTarget))
					{
						PickEarlier(forTarget, last, ref list, ref next);
					}

					if (sourced && BySource.TryGetValue(source, out RankedList<Delegate> fromSource))
					{
						PickEarlier(fromSource, last, ref list, ref next);
					}

					if (next == list.Count) return false;

					last = list.RankAt(next);
					Delegate handler = list.ItemAt(next);

					if (handler is TargetedEventHandler<TEvent> targeted)
					{
						InvokeTargeted(targeted, ref @event, target, source);
					}
					else
					{
						Invoke((EventHandler<TEvent>)handler, ref @event);
					}

					if (bus.CurrentEventIsConsumed) return true;
				}
			}

			public override void DeliverOldestWaiting(GenericEventBus<TBaseEvent> bus)
			{
				(TEvent @event, TObject target, TObject source) = Waiting.Dequeue();
				((GenericEventBus<TBaseEvent, TObject>)bus).RaiseImmediately(ref @event, target, source);
			}

			public override void ClearListeners()
			{
				All.Clear();
				ByTarget.Clear();
				BySource.Clear();
			}

			public override void Clear()
			{
				ClearListeners();
				Waiting.Clear();
			}

			private static void PickEarlier(RankedList<Delegate> candidates, Rank last, ref RankedList<Delegate> list, ref int next)
			{
				int index = candidates.IndexAfter(last);
				if (index == candidates.Count) return;

				if (next == list.Count || candidates.RankAt(index).CompareTo(list.RankAt(next)) < 0)
				{
					list = candidates;
					next = index;
				}
			}

			private static void InvokeTargeted(TargetedEventHandler<TEvent> handler, ref TEvent @event, TObject target, TObject source)
			{
				try
				{
					handler(ref @event, target, source);
				}
				catch (Exception e)
				{
					UnityEngine.Debug.LogException(e);
				}
			}
		}
	}
}
