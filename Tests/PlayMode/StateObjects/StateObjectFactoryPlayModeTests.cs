using System.Collections;
using AK.Core;
using AK.StateMachines;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AK.Tests.StateObjects
{
	/// <summary>
	/// The factory ticks its state objects each frame, and destroys them once, with itself: each
	/// one's state machine is disposed, then its OnDestroy runs.
	/// </summary>
	public class StateObjectFactoryPlayModeTests
	{
		private sealed class Probe : StateObject<Probe, Probe.Counting>
		{
			public int  Created;
			public int  Destroyed;
			public bool MachineDisposedFirst;

			public Counting State => _stateMachine.CurrentState;

			protected override void OnCreate() => Created++;

			protected override void OnDestroy()
			{
				Destroyed++;
				MachineDisposedFirst = _stateMachine.CurrentState == null;
			}

			public sealed class Counting : BaseState<Probe>
			{
				public int Ticks;
				public int Exits;
				public int Disposals;

				public override void Tick() => Ticks++;

				public override void OnExit() => Exits++;

				public override void Dispose() => Disposals++;
			}
		}

		private StateObjectFactory _factory;

		[SetUp]
		public void SetUp()
		{
			_factory = StateObjectFactory.Construct(dontDestroyOnLoad: false);
		}

		[TearDown]
		public void TearDown()
		{
			if (_factory != null) Object.DestroyImmediate(_factory.gameObject);
		}

		[UnityTest]
		public IEnumerator Dispose_DestroysTheStateObjectsOnce_TheirMachinesFirst()
		{
			Probe probe = StateObjectFactory.CreateStateObject<Probe>();
			Probe.Counting state = probe.State;
			Assert.AreEqual(1, probe.Created);

			yield return null;
			yield return null;
			Assert.GreaterOrEqual(state.Ticks, 1, "ticked each frame");

			_factory.Dispose();

			Assert.AreEqual(1, probe.Destroyed);
			Assert.IsTrue(probe.MachineDisposedFirst, "the state machine is disposed before OnDestroy");
			Assert.AreEqual(1, state.Exits);
			Assert.AreEqual(1, state.Disposals);

			int ticks = state.Ticks;
			yield return null;

			Assert.AreEqual(ticks, state.Ticks, "no longer ticked");
			Assert.AreEqual(1, probe.Destroyed, "the GameObject's destruction doesn't destroy it again");
		}

		[UnityTest]
		public IEnumerator DestroyingTheFactorysGameObject_DestroysItsStateObjects()
		{
			Probe probe = StateObjectFactory.CreateStateObject<Probe>();
			Probe.Counting state = probe.State;

			Object.Destroy(_factory.gameObject);
			yield return null;

			Assert.AreEqual(1, probe.Destroyed);
			Assert.AreEqual(1, state.Disposals);
			StateObjectFactory next = StateObjectFactory.Construct(dontDestroyOnLoad: false);
			Assert.AreNotSame(_factory, next, "the next Construct makes a new factory");
			_factory = next;
		}
	}
}
