using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.Tests.Support;
using NUnit.Framework;

namespace AK.Tests
{
	public class EventBusTests
	{
		private struct Ping : IEvent
		{
			public int Value;
		}

		private struct Pong : IEvent
		{
			public int Value;
		}

		private readonly List<string> _heard = new();

		private GenericEventBus<IEvent>         _bus;
		private GenericEventBus<IEvent, string> _targeted;

		[SetUp]
		public void SetUp()
		{
			_bus      = new GenericEventBus<IEvent>();
			_targeted = new GenericEventBus<IEvent, string>();
		}

		[TearDown]
		public void TearDown()
		{
			_bus.Dispose();
			_targeted.Dispose();
			_heard.Clear();
		}

		// ---------------------------------------------------------------- order

		[Test]
		public void Listeners_HearAnEvent_ByPriority_ThenInTheOrderTheySubscribed()
		{
			_bus.SubscribeTo(Hear("a"));
			_bus.SubscribeTo(Hear("b"), priority: 5);
			_bus.SubscribeTo(Hear("c"));
			_bus.SubscribeTo(Hear("d"), priority: 5);
			_bus.SubscribeTo(Hear("e"), priority: -1);

			Assert.IsFalse(_bus.Raise(new Ping()), "nobody consumed it");

			CollectionAssert.AreEqual(new[] { "b", "d", "a", "c", "e" }, _heard);
		}

		[Test]
		public void AListener_MayChangeTheEvent_ForTheListenersAfterIt()
		{
			_bus.SubscribeTo((ref Ping e) => e.Value += 1, priority: 1);
			_bus.SubscribeTo((ref Ping e) => _heard.Add("saw " + e.Value));

			_bus.Raise(new Ping { Value = 41 });

			CollectionAssert.AreEqual(new[] { "saw 42" }, _heard);
		}

		[Test]
		public void EachBus_HasListenersOfItsOwn()
		{
			using var other = new GenericEventBus<IEvent>();
			_bus.SubscribeTo(Hear("mine"));

			other.Raise(new Ping());
			CollectionAssert.IsEmpty(_heard);

			_bus.Raise(new Ping());
			CollectionAssert.AreEqual(new[] { "mine" }, _heard);
		}

		// ---------------------------------------------------------------- subscriptions

		[Test]
		public void Unsubscribing_RemovesOneSubscription_TheLatest()
		{
			GenericEventBus<IEvent>.EventHandler<Ping> h = Hear("h");
			_bus.SubscribeTo(h, priority: 5);
			_bus.SubscribeTo(Hear("g"), priority: 3);
			_bus.SubscribeTo(h, priority: 1);

			HeardAfterRaise(new[] { "h", "g", "h" });

			_bus.UnsubscribeFrom(h);
			HeardAfterRaise(new[] { "h", "g" }, "the latest subscription went: the one at priority 1");

			_bus.UnsubscribeFrom(h);
			HeardAfterRaise(new[] { "g" });

			_bus.UnsubscribeFrom(h);
			HeardAfterRaise(new[] { "g" }, "unsubscribing with none left does nothing");
		}

		[Test]
		public void AListenerUnsubscribedDuringARaise_DoesNotHearItAfterwards()
		{
			GenericEventBus<IEvent>.EventHandler<Ping> later = Hear("later");
			_bus.SubscribeTo((ref Ping _) =>
			{
				_heard.Add("first");
				_bus.UnsubscribeFrom(later);
			}, priority: 1);
			_bus.SubscribeTo(later);

			_bus.Raise(new Ping());

			CollectionAssert.AreEqual(new[] { "first" }, _heard);
		}

		[Test]
		public void AListenerSubscribedDuringARaise_HearsItOnlyWhenItRanksAfterTheOneCalled()
		{
			bool subscribed = false;
			_bus.SubscribeTo((ref Ping _) =>
			{
				_heard.Add("a");
				if (subscribed) return;

				subscribed = true;
				_bus.SubscribeTo(Hear("before"), priority: 9);
				_bus.SubscribeTo(Hear("level"), priority: 5);
				_bus.SubscribeTo(Hear("after"), priority: 1);
			}, priority: 5);
			_bus.SubscribeTo(Hear("c"), priority: 3);

			HeardAfterRaise(new[] { "a", "level", "c", "after" }, "a listener at a's priority subscribed after it, so it ranks after it");
			HeardAfterRaise(new[] { "before", "a", "level", "c", "after" });
		}

		[Test]
		public void Subscribing_RefusesNoHandler_AndAPriorityThatIsNotANumber()
		{
			Assert.Throws<ArgumentNullException>(() => _bus.SubscribeTo<Ping>(null));
			Assert.Throws<ArgumentOutOfRangeException>(() => _bus.SubscribeTo(Hear("nan"), float.NaN));
			Assert.Throws<ArgumentNullException>(() => _targeted.SubscribeToTarget<Ping>("door", null));

			_bus.Raise(new Ping());
			CollectionAssert.IsEmpty(_heard);
		}

		[Test]
		public void ClearListeners_RemovesTheListenersOfOneType_ButNotDuringARaise()
		{
			_bus.SubscribeTo(Hear("ping"));
			_bus.SubscribeTo((ref Pong _) => _heard.Add("pong"));

			_bus.ClearListeners<Ping>();
			_bus.Raise(new Ping());
			_bus.Raise(new Pong());
			CollectionAssert.AreEqual(new[] { "pong" }, _heard);

			Exception thrown = null;
			_bus.SubscribeTo((ref Pong _) => thrown = Assert.Throws<InvalidOperationException>(() => _bus.ClearListeners<Pong>()));
			_bus.Raise(new Pong());
			Assert.IsNotNull(thrown);
		}

		// ---------------------------------------------------------------- consuming

		[Test]
		public void AConsumedEvent_StopsBeforeTheListenersAfter()
		{
			_bus.SubscribeTo(Hear("a"), priority: 2);
			_bus.SubscribeTo((ref Ping _) =>
			{
				_heard.Add("consumer");
				_bus.ConsumeCurrentEvent();
				Assert.IsTrue(_bus.CurrentEventIsConsumed);
			}, priority: 1);
			_bus.SubscribeTo(Hear("b"));

			Assert.IsTrue(_bus.RaiseImmediately(new Ping()));
			CollectionAssert.AreEqual(new[] { "a", "consumer" }, _heard);
			Assert.IsFalse(_bus.CurrentEventIsConsumed);

			_bus.ConsumeCurrentEvent();
			HeardAfterRaise(new[] { "a", "consumer" }, "consuming outside a raise changes nothing");
		}

		[Test]
		public void ConsumingAnEventDeliveredInsideAnother_StopsOnlyThatOne()
		{
			_bus.SubscribeTo((ref Pong _) =>
			{
				_heard.Add("pong");
				_bus.ConsumeCurrentEvent();
			});
			_bus.SubscribeTo((ref Pong _) => _heard.Add("pong too"), priority: -1);
			_bus.SubscribeTo((ref Ping _) =>
			{
				_heard.Add("ping");
				Assert.IsTrue(_bus.RaiseImmediately(new Pong()));
				Assert.IsFalse(_bus.CurrentEventIsConsumed, "the ping wasn't consumed");
			}, priority: 1);
			_bus.SubscribeTo(Hear("ping too"));

			Assert.IsFalse(_bus.Raise(new Ping()));
			CollectionAssert.AreEqual(new[] { "ping", "pong", "ping too" }, _heard);
		}

		// ---------------------------------------------------------------- raising during a raise

		[Test]
		public void AnEventRaisedDuringARaise_WaitsForTheOneDelivered_AndEventsWaitInTheOrderRaised()
		{
			_bus.SubscribeTo((ref Ping e) =>
			{
				_heard.Add("ping " + e.Value);
				if (e.Value != 1) return;

				Assert.IsFalse(_bus.Raise(new Pong { Value = 1 }), "it waits");
				Assert.IsFalse(_bus.Raise(new Ping { Value = 2 }));
				_heard.Add("raised");
			});
			_bus.SubscribeTo((ref Ping e) => _heard.Add("ping " + e.Value + " too"), priority: -1);
			_bus.SubscribeTo((ref Pong e) =>
			{
				_heard.Add("pong " + e.Value);
				if (e.Value == 1) _bus.Raise(new Pong { Value = 3 });
			});

			_bus.Raise(new Ping { Value = 1 });

			CollectionAssert.AreEqual(new[]
			{
				"ping 1", "raised", "ping 1 too",
				"pong 1", "ping 2", "ping 2 too", "pong 3",
			}, _heard);

			_heard.Clear();
			_bus.Raise(new Pong { Value = 9 });
			CollectionAssert.AreEqual(new[] { "pong 9" }, _heard, "nothing was left waiting");
		}

		[Test]
		public void AnEventRaisedImmediatelyDuringARaise_IsDeliveredInsideIt()
		{
			_bus.SubscribeTo((ref Ping _) =>
			{
				_heard.Add("ping");
				_bus.RaiseImmediately(new Pong());
				_heard.Add("ping done");
			});
			_bus.SubscribeTo((ref Pong _) => _heard.Add("pong"));

			_bus.Raise(new Ping());

			CollectionAssert.AreEqual(new[] { "ping", "pong", "ping done" }, _heard);
		}

		// ---------------------------------------------------------------- failures and lifetime

		[Test]
		public void AListenerThatThrows_IsLogged_AndTheListenersAfterItStillHear()
		{
			_bus.SubscribeTo((ref Ping _) => throw new InvalidOperationException("listener broke"), priority: 1);
			_bus.SubscribeTo(Hear("after"));

			using (ExpectedLog.Exception("listener broke"))
			{
				_bus.Raise(new Ping());
			}

			CollectionAssert.AreEqual(new[] { "after" }, _heard);
			Assert.IsFalse(_bus.IsEventBeingRaised);
		}

		[Test]
		public void Dispose_DropsListenersAndWaitingEvents_AndTheBusRefusesFurtherUse()
		{
			GenericEventBus<IEvent>.EventHandler<Ping> listener = Hear("listener");
			_bus.SubscribeTo((ref Pong _) =>
			{
				_heard.Add("pong");
				_bus.Raise(new Ping());
				_bus.Dispose();
			});
			_bus.SubscribeTo(listener);

			_bus.Raise(new Pong());

			CollectionAssert.AreEqual(new[] { "pong" }, _heard, "the waiting ping was dropped");
			Assert.Throws<ObjectDisposedException>(() => _bus.SubscribeTo(listener));
			Assert.Throws<ObjectDisposedException>(() => _bus.Raise(new Ping()));
			Assert.Throws<ObjectDisposedException>(() => _bus.RaiseImmediately(new Ping()));
			Assert.DoesNotThrow(() => _bus.UnsubscribeFrom(listener));
			Assert.DoesNotThrow(() => _bus.Dispose());
		}

		[Test]
		public void UsingTheBusFromAnotherThread_IsReported()
		{
			using (ExpectedLog.Error("GenericEventBus.RaiseImmediately is main-thread only"))
			{
				var worker = new Thread(() => _bus.Raise(new Ping()));
				worker.Start();
				worker.Join();
			}
		}

		[Test]
		public void RaisingToListeners_AllocatesNothing()
		{
			int count = 0;
			_bus.SubscribeTo((ref Ping e) =>
			{
				count++;
				if (e.Value == 0) _bus.Raise(new Ping { Value = 1 });
			});
			_bus.SubscribeTo((ref Ping _) => count++, priority: 2);
			_targeted.SubscribeToTarget("door", (ref Ping _, string _, string _) => count++);
			_targeted.SubscribeTo((ref Ping _, string _, string _) => count++, priority: 1);

			int Window() => GcAllocations.Count(() =>
			{
				for (int i = 0; i < 10; i++)
				{
					_bus.Raise(new Ping());
					_targeted.Raise(new Ping(), "door", "key");
				}
			});

			Window();
			Assert.AreEqual(0, Window());
			Assert.AreEqual(2 * 10 * (4 + 2), count, "each window: the bus calls 2 listeners for each ping and the 1 it raises, the targeted bus 2");
		}

		// ---------------------------------------------------------------- targets and sources

		[Test]
		public void TargetAndSourceListeners_HearOnlyTheirOwnEvents_AndTheOthersHearEverything()
		{
			_targeted.SubscribeTo(HearTargeted("all"));
			_targeted.SubscribeTo((ref Ping _) => _heard.Add("plain"));
			_targeted.SubscribeToTarget("door", HearTargeted("door"));
			_targeted.SubscribeToTarget("window", HearTargeted("window"));
			_targeted.SubscribeToSource("key", HearTargeted("from key"));

			_targeted.Raise(new Ping(), "door", "key");
			CollectionAssert.AreEqual(new[] { "all door key", "plain", "door door key", "from key door key" }, _heard);

			_heard.Clear();
			_targeted.Raise(new Ping());
			CollectionAssert.AreEqual(new[] { "all - -", "plain" }, _heard, "no target and no source");
		}

		[Test]
		public void AllThreeKindsOfListener_HearAnEventInOneOrder()
		{
			_targeted.SubscribeToSource("key", HearTargeted("source 5"), priority: 5);
			_targeted.SubscribeTo(HearTargeted("all 5"), priority: 5);
			_targeted.SubscribeToTarget("door", HearTargeted("target 5"), priority: 5);
			_targeted.SubscribeToTarget("door", HearTargeted("target 9"), priority: 9);
			_targeted.SubscribeTo(HearTargeted("all 1"), priority: 1);
			_targeted.SubscribeToSource("key", HearTargeted("source 3"), priority: 3);

			_targeted.Raise(new Ping(), "door", "key");

			CollectionAssert.AreEqual(new[]
			{
				"target 9 door key",
				"source 5 door key", "all 5 door key", "target 5 door key",
				"source 3 door key",
				"all 1 door key",
			}, _heard, "by priority, then in the order they subscribed, whichever kind");
		}

		[Test]
		public void SubscribingToNoObject_SubscribesToEveryEvent()
		{
			_targeted.SubscribeToTarget(null, HearTargeted("no target"));

			using var withDefault = new GenericEventBus<IEvent, string>("nobody");
			withDefault.SubscribeToSource("nobody", HearTargeted("default source"));

			_targeted.Raise(new Ping(), "door", "key");
			withDefault.Raise(new Ping(), "door", "key");
			withDefault.Raise(new Ping());

			CollectionAssert.AreEqual(new[] { "no target door key", "default source door key", "default source nobody nobody" }, _heard);
			Assert.AreEqual("nobody", withDefault.DefaultObject);
		}

		[Test]
		public void APlainListenerOnATargetedBus_IsSubscribedAndUnsubscribedOneAtATime()
		{
			GenericEventBus<IEvent>.EventHandler<Ping> plain = Hear("plain");
			_targeted.SubscribeTo(plain);
			_targeted.SubscribeTo(plain);

			_targeted.Raise(new Ping());
			CollectionAssert.AreEqual(new[] { "plain", "plain" }, _heard);

			_heard.Clear();
			_targeted.UnsubscribeFrom(plain);
			_targeted.Raise(new Ping());
			CollectionAssert.AreEqual(new[] { "plain" }, _heard);

			_targeted.ClearListeners<Ping>();
			_targeted.SubscribeTo(plain);
			_targeted.UnsubscribeFrom(plain);
			_heard.Clear();
			_targeted.Raise(new Ping());
			CollectionAssert.IsEmpty(_heard, "nothing left behind by the clear");
		}

		[Test]
		public void ATargetListenerSubscribedDuringARaise_HearsIt_AndOneUnsubscribedStopsHearing()
		{
			GenericEventBus<IEvent, string>.TargetedEventHandler<Ping> leaving = HearTargeted("leaving");
			_targeted.SubscribeToTarget("door", leaving, priority: 1);
			_targeted.SubscribeTo((ref Ping _, string target, string _) =>
			{
				_heard.Add("first");
				_targeted.UnsubscribeFromTarget(target, leaving);
				_targeted.SubscribeToTarget(target, HearTargeted("arriving"), priority: -1);
			}, priority: 5);

			_targeted.Raise(new Ping(), "door", null);

			CollectionAssert.AreEqual(new[] { "first", "arriving door -" }, _heard);
		}

		[Test]
		public void ATargetedEventRaisedDuringARaise_KeepsItsTargetAndSource()
		{
			_targeted.SubscribeTo((ref Pong _, string _, string _) => _targeted.Raise(new Ping(), "door", "key"));
			_targeted.SubscribeToTarget("door", HearTargeted("door"));

			_targeted.Raise(new Pong());

			CollectionAssert.AreEqual(new[] { "door door key" }, _heard);
		}

		// ---------------------------------------------------------------- helpers

		private GenericEventBus<IEvent>.EventHandler<Ping> Hear(string name) => (ref Ping _) => _heard.Add(name);

		private GenericEventBus<IEvent, string>.TargetedEventHandler<Ping> HearTargeted(string name) =>
			(ref Ping _, string target, string source) => _heard.Add($"{name} {target ?? "-"} {source ?? "-"}");

		private void HeardAfterRaise(string[] expected, string message = null)
		{
			_heard.Clear();
			_bus.Raise(new Ping());
			CollectionAssert.AreEqual(expected, _heard, message);
		}
	}
}
