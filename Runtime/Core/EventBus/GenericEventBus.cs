using System;
using System.Collections.Generic;
using System.Threading;
using AK.Kernel.Collections;
using AK.Kernel.Threading;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// <para>An event bus. Listeners subscribe to an event type, and each event raised reaches the
	/// listeners of its type: higher priority first, and in the order they subscribed within a
	/// priority. A listener can consume the event (<see cref="ConsumeCurrentEvent"/>), and the
	/// listeners after it don't hear it.</para>
	///
	/// <para><b>Changes during a raise.</b> A listener unsubscribed while an event is delivered
	/// doesn't hear it afterwards. One subscribed meanwhile hears it if it ranks after the
	/// listener being called, and the next event otherwise. An event raised with
	/// <see cref="Raise{TEvent}"/> while another is delivered waits until that one, and those
	/// waiting before it, have been delivered. <see cref="RaiseImmediately{TEvent}(ref TEvent)"/>
	/// delivers at once, inside the event being delivered.</para>
	///
	/// <para>A listener that throws is logged, and the listeners after it still hear the event.</para>
	///
	/// <para>The bus owns its listeners: nothing outside it holds them. Main thread only, which the
	/// editor and development builds assert. For events aimed at objects, with a source, use
	/// <see cref="GenericEventBus{TBaseEvent, TObject}"/>.</para>
	/// </summary>
	/// <typeparam name="TBaseEvent"><para>The type every event derives from or implements.</para> Use <see cref="object"/> for any type.</typeparam>
	public class GenericEventBus<TBaseEvent> : IDisposable
	{
		// Each event type has a slot, numbered once per base event type. A bus keeps its channels
		// in an array of its own, at those slots.
		private static int _eventTypeCount;

		private static class EventTypeSlot<TEvent>
		{
			public static readonly int Index = Interlocked.Increment(ref _eventTypeCount) - 1;
		}

		private Channel[] _channels = Array.Empty<Channel>();

		// One entry per waiting event: the channel that holds it, in the order raised.
		private readonly Queue<Channel> _waiting = new();

		// Whether each event being delivered was consumed, the innermost last.
		private bool[] _consumed = new bool[4];
		private int    _raiseDepth;
		private bool   _deliveringWaiting;
		private long   _nextOrder;
		private bool   _disposed;

		/// <summary>A listener. It may change the event, and the listeners after it see the change.</summary>
		/// <param name="@event">The event that was raised.</param>
		/// <typeparam name="TEvent">The type of event this callback handles.</typeparam>
		public delegate void EventHandler<TEvent>(ref TEvent @event) where TEvent : TBaseEvent;

		/// <summary>Whether the event being delivered has been consumed.</summary>
		public bool CurrentEventIsConsumed => _raiseDepth > 0 && _consumed[_raiseDepth - 1];

		/// <summary>Whether an event is being delivered.</summary>
		public bool IsEventBeingRaised => _raiseDepth > 0;

		/// <summary>Delivers <paramref name="event"/> now, even inside another event's delivery.</summary>
		/// <returns>Whether a listener consumed it.</returns>
		public bool RaiseImmediately<TEvent>(TEvent @event) where TEvent : TBaseEvent
		{
			return RaiseImmediately(ref @event);
		}

		/// <summary>Delivers <paramref name="event"/> now, even inside another event's delivery.</summary>
		/// <returns>Whether a listener consumed it.</returns>
		public virtual bool RaiseImmediately<TEvent>(ref TEvent @event) where TEvent : TBaseEvent
		{
			ThrowIfUnusable("GenericEventBus.RaiseImmediately");

			Channel<TEvent> channel = FindChannel<TEvent, Channel<TEvent>>();

			BeginRaise();
			try
			{
				return channel != null && channel.Deliver(this, ref @event);
			}
			finally
			{
				EndRaise();
			}
		}

		/// <summary>
		/// Delivers <paramref name="event"/>, after the event being delivered and those waiting
		/// before it, if any.
		/// </summary>
		/// <returns>Whether a listener consumed it, when it was delivered at once. False when it waits.</returns>
		public virtual bool Raise<TEvent>(in TEvent @event) where TEvent : TBaseEvent
		{
			if (!IsEventBeingRaised)
			{
				TEvent now = @event;
				return RaiseImmediately(ref now);
			}

			ThrowIfUnusable("GenericEventBus.Raise");

			Channel<TEvent> channel = GetChannel<TEvent, Channel<TEvent>>();
			channel.Waiting.Enqueue(@event);
			Wait(channel);
			return false;
		}

		/// <summary>Subscribes <paramref name="handler"/> to <typeparamref name="TEvent"/>. Subscribing it twice makes it hear each event twice.</summary>
		/// <param name="priority">Higher hears the event earlier. Equal priorities hear it in the order they subscribed.</param>
		public virtual void SubscribeTo<TEvent>(EventHandler<TEvent> handler, float priority = 0)
			where TEvent : TBaseEvent
		{
			if (handler == null) throw new ArgumentNullException(nameof(handler));
			ThrowIfUnusable("GenericEventBus.SubscribeTo");

			GetChannel<TEvent, Channel<TEvent>>().Listeners.Add(handler, NextRank(priority));
		}

		/// <summary>Removes one subscription of <paramref name="handler"/>, the latest. Nothing happens when it has none.</summary>
		public virtual void UnsubscribeFrom<TEvent>(EventHandler<TEvent> handler) where TEvent : TBaseEvent
		{
			MainThreadGuard.Assert("GenericEventBus.UnsubscribeFrom");

			FindChannel<TEvent, Channel<TEvent>>()?.Listeners.RemoveLatest(handler);
		}

		/// <summary>Stops the event being delivered from reaching the listeners after the current one.</summary>
		public void ConsumeCurrentEvent()
		{
			if (_raiseDepth > 0)
			{
				_consumed[_raiseDepth - 1] = true;
			}
		}

		/// <summary>Removes every listener of <typeparamref name="TEvent"/>. Its waiting events stay, for whoever subscribes before they are delivered.</summary>
		/// <exception cref="InvalidOperationException">An event is being delivered.</exception>
		public void ClearListeners<TEvent>() where TEvent : TBaseEvent
		{
			MainThreadGuard.Assert("GenericEventBus.ClearListeners");

			if (IsEventBeingRaised)
			{
				throw new InvalidOperationException("Not allowed to clear listeners while an event is being raised.");
			}

			int slot = EventTypeSlot<TEvent>.Index;
			if (slot < _channels.Length)
			{
				_channels[slot]?.ClearListeners();
			}
		}

		/// <summary>
		/// Drops every listener and waiting event. Subscribing and raising afterwards throw
		/// <see cref="ObjectDisposedException"/>; unsubscribing does nothing.
		/// </summary>
		public virtual void Dispose()
		{
			if (_disposed) return;

			_disposed = true;
			foreach (Channel channel in _channels)
			{
				channel?.Clear();
			}

			_channels = Array.Empty<Channel>();
			_waiting.Clear();
		}

		// ---------------------------------------------------------------- for the targeted bus

		/// <summary>The listeners of one event type, and its events waiting to be delivered.</summary>
		private protected abstract class Channel
		{
			/// <summary>Delivers the oldest waiting event.</summary>
			public abstract void DeliverOldestWaiting(GenericEventBus<TBaseEvent> bus);

			public abstract void ClearListeners();

			/// <summary>Drops the listeners and the waiting events.</summary>
			public abstract void Clear();
		}

		private protected TChannel FindChannel<TEvent, TChannel>()
			where TEvent : TBaseEvent
			where TChannel : Channel
		{
			int slot = EventTypeSlot<TEvent>.Index;
			return slot < _channels.Length ? (TChannel)_channels[slot] : null;
		}

		private protected TChannel GetChannel<TEvent, TChannel>()
			where TEvent : TBaseEvent
			where TChannel : Channel, new()
		{
			int slot = EventTypeSlot<TEvent>.Index;
			if (slot >= _channels.Length)
			{
				Array.Resize(ref _channels, Math.Max(slot + 1, _channels.Length * 2));
			}

			return (TChannel)(_channels[slot] ??= new TChannel());
		}

		/// <exception cref="ArgumentOutOfRangeException"><paramref name="priority"/> is NaN.</exception>
		private protected Rank NextRank(float priority) => new(priority, _nextOrder++);

		/// <summary>Records that <paramref name="channel"/> has a new waiting event.</summary>
		private protected void Wait(Channel channel) => _waiting.Enqueue(channel);

		private protected void BeginRaise()
		{
			if (_raiseDepth == _consumed.Length)
			{
				Array.Resize(ref _consumed, _raiseDepth * 2);
			}

			_consumed[_raiseDepth++] = false;
		}

		/// <summary>Ends a delivery. The outermost delivers the waiting events, oldest first.</summary>
		private protected void EndRaise()
		{
			_consumed[--_raiseDepth] = false;
			if (_raiseDepth > 0 || _deliveringWaiting) return;

			_deliveringWaiting = true;
			try
			{
				while (_waiting.Count > 0)
				{
					_waiting.Dequeue().DeliverOldestWaiting(this);
				}
			}
			finally
			{
				_deliveringWaiting = false;
			}
		}

		private protected void ThrowIfUnusable(string what)
		{
			MainThreadGuard.Assert(what);

			if (_disposed)
			{
				throw new ObjectDisposedException(GetType().Name);
			}
		}

		/// <summary>Calls <paramref name="handler"/>, logging what it throws so the other listeners still hear the event.</summary>
		private protected static void Invoke<TEvent>(EventHandler<TEvent> handler, ref TEvent @event) where TEvent : TBaseEvent
		{
			try
			{
				handler(ref @event);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}
		}

		private sealed class Channel<TEvent> : Channel where TEvent : TBaseEvent
		{
			public readonly RankedList<EventHandler<TEvent>> Listeners = new();
			public readonly Queue<TEvent>                    Waiting   = new();

			/// <returns>Whether a listener consumed the event.</returns>
			public bool Deliver(GenericEventBus<TBaseEvent> bus, ref TEvent @event)
			{
				for (Rank last = Rank.BeforeAll; ;)
				{
					int next = Listeners.IndexAfter(last);
					if (next == Listeners.Count) return false;

					last = Listeners.RankAt(next);
					Invoke(Listeners.ItemAt(next), ref @event);

					if (bus.CurrentEventIsConsumed) return true;
				}
			}

			public override void DeliverOldestWaiting(GenericEventBus<TBaseEvent> bus)
			{
				TEvent @event = Waiting.Dequeue();
				bus.RaiseImmediately(ref @event);
			}

			public override void ClearListeners() => Listeners.Clear();

			public override void Clear()
			{
				Listeners.Clear();
				Waiting.Clear();
			}
		}
	}
}
