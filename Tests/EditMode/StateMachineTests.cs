using System;
using System.Collections.Generic;
using AK.StateMachines;
using NUnit.Framework;

namespace AK.Tests
{
	public class StateMachineTests
	{
		private sealed class Owner
		{
			public readonly List<string> Log = new();
		}

		// The machine's base state. The default one takes its owner's log when entered; the tests
		// hand the others the log, since a state that is never entered has no owner.
		private class TestState : BaseState<Owner>
		{
			public string       Name = "default";
			public List<string> Log;
			public Action       Entering;
			public Action       Exiting;
			public int          Ticks;

			public Owner Owner => Mediator;

			public override void OnEnter()
			{
				Log ??= Mediator.Log;
				Log.Add("enter " + Name);
				Entering?.Invoke();
			}

			public override void Tick() => Ticks++;

			public override void OnExit()
			{
				Log.Add("exit " + Name);
				Exiting?.Invoke();
			}

			public override void Dispose() => Log?.Add("dispose " + Name);
		}

		private Owner                          _owner;
		private StateMachine<Owner, TestState> _machine;

		[SetUp]
		public void SetUp()
		{
			_owner   = new Owner();
			_machine = new StateMachine<Owner, TestState>(_owner);
			_owner.Log.Clear();
		}

		private TestState State(string name) => new() { Name = name, Log = _owner.Log };

		// ---------------------------------------------------------------- changes

		[Test]
		public void TheMachine_StartsInANewBaseState_WithItsOwner()
		{
			var owner   = new Owner();
			var machine = new StateMachine<Owner, TestState>(owner);

			CollectionAssert.AreEqual(new[] { "enter default" }, owner.Log);
			Assert.AreEqual("default", machine.CurrentState.Name);
			Assert.AreSame(owner, machine.CurrentState.Owner);
		}

		[Test]
		public void AChange_ExitsAndDisposesThePreviousState_ThenEntersTheNext()
		{
			TestState a = State("a");

			_machine.ChangeState(a);

			CollectionAssert.AreEqual(new[] { "exit default", "dispose default", "enter a" }, _owner.Log);
			Assert.AreSame(a, _machine.CurrentState);
			Assert.AreSame(_owner, a.Owner);
		}

		[Test]
		public void ChangingToTheCurrentState_ExitsAndEntersItAgain_WithoutDisposingIt()
		{
			TestState a = State("a");
			_machine.ChangeState(a);
			_owner.Log.Clear();

			_machine.ChangeState(a);

			CollectionAssert.AreEqual(new[] { "exit a", "enter a" }, _owner.Log);
			Assert.AreSame(a, _machine.CurrentState);
		}

		[Test]
		public void AChangeRequestedInOnEnter_RunsOnceTheEntryHasFinished()
		{
			TestState a = State("a");
			TestState b = State("b");
			a.Entering = () =>
			{
				_machine.ChangeState(b);
				_owner.Log.Add("entered a");
			};

			_machine.ChangeState(a);

			CollectionAssert.AreEqual(new[]
			{
				"exit default", "dispose default", "enter a", "entered a",
				"exit a", "dispose a", "enter b",
			}, _owner.Log);
			Assert.AreSame(b, _machine.CurrentState);
		}

		[Test]
		public void AChangeRequestedInOnExit_RunsAfterTheChangeUnderWay()
		{
			TestState a = State("a");
			TestState b = State("b");
			_machine.ChangeState(a);
			_owner.Log.Clear();
			a.Exiting = () => _machine.ChangeState(b);
			TestState c = State("c");

			_machine.ChangeState(c);

			CollectionAssert.AreEqual(new[] { "exit a", "dispose a", "enter c", "exit c", "dispose c", "enter b" }, _owner.Log);
			Assert.AreSame(b, _machine.CurrentState);
		}

		[Test]
		public void ChangesRequestedTogether_RunInTheOrderRequested()
		{
			TestState a = State("a");
			TestState b = State("b");
			TestState c = State("c");
			a.Entering = () =>
			{
				_machine.ChangeState(b);
				_machine.ChangeState(c);
			};

			_machine.ChangeState(a);

			CollectionAssert.AreEqual(new[]
			{
				"exit default", "dispose default", "enter a",
				"exit a", "dispose a", "enter b",
				"exit b", "dispose b", "enter c",
			}, _owner.Log);
			Assert.AreSame(c, _machine.CurrentState);
		}

		[Test]
		public void ANullState_IsRefused()
		{
			Assert.Throws<ArgumentNullException>(() => _machine.ChangeState(null));
			Assert.AreEqual("default", _machine.CurrentState.Name);
		}

		[Test]
		public void StatesThatKeepChangingState_Throw_AndTheStateLeftWaitingIsDisposed()
		{
			int entered = 0;
			TestState Looping()
			{
				TestState state = State("loop");
				state.Entering = () =>
				{
					entered++;
					_machine.ChangeState(Looping());
				};
				return state;
			}

			Assert.Throws<InvalidOperationException>(() => _machine.ChangeState(Looping()));

			Assert.AreEqual(StateMachine<Owner, TestState>.MaxChangesPerRun, entered);
			Assert.AreEqual(entered, _owner.Log.FindAll(line => line == "dispose loop").Count,
				"each state entered and left is disposed, and so is the one left waiting");
			Assert.AreEqual("loop", _machine.CurrentState.Name);

			// The machine still works.
			TestState after = State("after");
			_machine.ChangeState(after);
			Assert.AreSame(after, _machine.CurrentState);
		}

		[Test]
		public void AStateThatThrowsInOnEnter_StaysCurrent_AndTheChangesItRequestedAreDropped()
		{
			TestState a = State("a");
			TestState b = State("b");
			a.Entering = () =>
			{
				_machine.ChangeState(b);
				throw new InvalidOperationException("a failed");
			};

			Assert.Throws<InvalidOperationException>(() => _machine.ChangeState(a));

			Assert.AreSame(a, _machine.CurrentState);
			CollectionAssert.AreEqual(new[] { "exit default", "dispose default", "enter a", "dispose b" }, _owner.Log);
		}

		[Test]
		public void AStateThatThrowsInOnExit_StaysCurrent_AndTheNextStateIsDisposed()
		{
			TestState a = State("a");
			_machine.ChangeState(a);
			_owner.Log.Clear();
			a.Exiting = () => throw new InvalidOperationException("a failed");
			TestState b = State("b");

			Assert.Throws<InvalidOperationException>(() => _machine.ChangeState(b));

			Assert.AreSame(a, _machine.CurrentState);
			CollectionAssert.AreEqual(new[] { "exit a", "dispose b" }, _owner.Log);
		}

		// ---------------------------------------------------------------- ticks

		[Test]
		public void Tick_TicksTheCurrentState()
		{
			TestState a = State("a");
			_machine.ChangeState(a);

			_machine.Tick();
			_machine.Tick();

			Assert.AreEqual(2, a.Ticks);
		}

		// ---------------------------------------------------------------- disposal

		[Test]
		public void Dispose_ExitsAndDisposesTheCurrentState_Once()
		{
			TestState a = State("a");
			_machine.ChangeState(a);
			_owner.Log.Clear();

			_machine.Dispose();
			_machine.Dispose();

			CollectionAssert.AreEqual(new[] { "exit a", "dispose a" }, _owner.Log);
			Assert.IsNull(_machine.CurrentState);
		}

		[Test]
		public void AfterDispose_AChangeDisposesTheStateHandedIn_AndATickDoesNothing()
		{
			TestState a = State("a");
			_machine.ChangeState(a);
			_machine.Dispose();
			_owner.Log.Clear();
			TestState b = State("b");

			_machine.ChangeState(b);
			_machine.Tick();

			CollectionAssert.AreEqual(new[] { "dispose b" }, _owner.Log);
			Assert.AreEqual(0, a.Ticks);
			Assert.IsNull(_machine.CurrentState);
		}

		[Test]
		public void DisposingFromOnEnter_ClosesTheMachineOnceOnEnterReturns()
		{
			TestState a = State("a");
			TestState b = State("b");
			a.Entering = () =>
			{
				_machine.ChangeState(b);
				_machine.Dispose();
				_owner.Log.Add("entered a");
			};

			_machine.ChangeState(a);

			CollectionAssert.AreEqual(new[]
			{
				"exit default", "dispose default", "enter a", "entered a",
				"exit a", "dispose a", "dispose b",
			}, _owner.Log);
			Assert.IsNull(_machine.CurrentState);
		}

		[Test]
		public void DisposingFromOnExit_NeverEntersTheNextState()
		{
			TestState a = State("a");
			_machine.ChangeState(a);
			_owner.Log.Clear();
			a.Exiting = () => _machine.Dispose();
			TestState b = State("b");

			_machine.ChangeState(b);

			CollectionAssert.AreEqual(new[] { "exit a", "dispose a", "dispose b" }, _owner.Log);
			Assert.IsNull(_machine.CurrentState);
		}

		[Test]
		public void DisposingFromOnExit_OfAChangeToTheSameState_DisposesItOnce()
		{
			TestState a = State("a");
			_machine.ChangeState(a);
			_owner.Log.Clear();
			a.Exiting = () => _machine.Dispose();

			_machine.ChangeState(a);

			CollectionAssert.AreEqual(new[] { "exit a", "dispose a" }, _owner.Log);
			Assert.IsNull(_machine.CurrentState);
		}
	}
}
