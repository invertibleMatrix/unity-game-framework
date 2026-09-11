using System.Text.RegularExpressions;
using AK.Systems;
using AK.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AK.Tests
{
	/// <summary>
	/// Edit-mode coverage of ViewHighlight: what it adds to a view, what it borrows, and
	/// that entering twice or re-entering never tears down and re-adds components. In edit
	/// mode teardown is immediate (DestroyImmediate), so a restore is observable right away.
	/// </summary>
	public class ViewHighlightTests
	{
		private sealed class HostScreen : UIView { }
		private sealed class Card : UIView { }

		private UISystemHarness _h;

		[SetUp]    public void SetUp()    => _h = new UISystemHarness();
		[TearDown] public void TearDown() => _h.Dispose();

		private Card ShowCard()
		{
			_h.MakePrefab<HostScreen>(screen: true);
			_h.MakePrefab<Card>();
			var host = _h.System.Show<HostScreen>();
			return _h.System.Show<Card>(ShowOptions.Under(host));
		}

		[Test]
		public void Enter_OnAFragment_AddsCanvasRaycasterAndDim_RestoreRemovesThem()
		{
			var card = ShowCard();

			var highlight = ViewHighlight.Enter(card);

			Assert.That(highlight.IsActive, Is.True);
			Assert.That(card.TryGetComponent(out Canvas canvas), Is.True);
			Assert.That(canvas.overrideSorting, Is.True);
			Assert.That(card.GetComponent<GraphicRaycaster>(), Is.Not.Null);
			Assert.That(card.GetComponent<ViewBackgroundOverlay>(), Is.Not.Null);

			highlight.Restore();

			Assert.That(highlight.IsActive, Is.False);
			Assert.That(card.GetComponent<Canvas>(), Is.Null);
			Assert.That(card.GetComponent<GraphicRaycaster>(), Is.Null);
			Assert.That(card.GetComponent<ViewBackgroundOverlay>(), Is.Null);
		}

		[Test]
		public void Enter_Twice_KeepsTheFirstHighlightAndWarns()
		{
			var card = ShowCard();
			ViewHighlight.Enter(card);
			Canvas first = card.GetComponent<Canvas>();

			LogAssert.Expect(LogType.Warning, new Regex("highlighted again while already highlighted"));
			var highlight = ViewHighlight.Enter(card);

			Assert.That(highlight.IsActive, Is.True);
			Assert.That(card.GetComponents<Canvas>().Length, Is.EqualTo(1));
			Assert.That(card.GetComponent<Canvas>(), Is.SameAs(first));
			Assert.That(card.GetComponents<ViewBackgroundOverlay>().Length, Is.EqualTo(1));
		}

		[Test]
		public void Enter_OnAViewThatAlreadyHasACanvas_BorrowsItAndHandsItBackOnRestore()
		{
			var card = ShowCard();
			var own = card.gameObject.AddComponent<Canvas>();
			own.overrideSorting = true;
			own.sortingOrder = 7;

			var highlight = ViewHighlight.Enter(card);

			Assert.That(card.GetComponents<Canvas>().Length, Is.EqualTo(1));
			Assert.That(own.overrideSorting, Is.True);
			Assert.That(own.sortingOrder, Is.Not.EqualTo(7));

			highlight.Restore();

			Assert.That(card.GetComponent<Canvas>(), Is.SameAs(own));
			Assert.That(own.overrideSorting, Is.True);
			Assert.That(own.sortingOrder, Is.EqualTo(7));
			Assert.That(card.GetComponent<GraphicRaycaster>(), Is.Null);
		}

		[Test]
		public void Enter_OnAViewWithItsOwnOverlay_DoesNotDestroyItOnRestore()
		{
			var card = ShowCard();
			var own = card.gameObject.AddComponent<ViewBackgroundOverlay>();

			var highlight = ViewHighlight.Enter(card);
			highlight.Restore();

			Assert.That(card.GetComponent<ViewBackgroundOverlay>(), Is.SameAs(own));
			Assert.That(own.IsBuilt, Is.False);
		}

		[Test]
		public void Exit_ThenEnter_HighlightsAgain()
		{
			var card = ShowCard();
			var highlight = ViewHighlight.Enter(card);

			highlight.Exit();
			Assert.That(highlight.IsActive, Is.False);
			Assert.That(card.GetComponent<Canvas>(), Is.Null);

			highlight.Enter();

			Assert.That(highlight.IsActive, Is.True);
			Assert.That(card.GetComponents<Canvas>().Length, Is.EqualTo(1));
			Assert.That(card.GetComponent<Canvas>().overrideSorting, Is.True);
		}

		[Test]
		public void Enter_OnAScreen_UsesTheChannelCanvasAndAddsNothing()
		{
			_h.MakePrefab<HostScreen>(screen: true);
			var screen = _h.System.Show<HostScreen>();
			Canvas channelCanvas = screen.GetComponent<Canvas>();
			int sortingBefore = channelCanvas.sortingOrder;

			var highlight = ViewHighlight.Enter(screen);

			// A root canvas ignores overrideSorting, so only the order is observable here.
			Assert.That(sortingBefore, Is.Not.EqualTo((int)UIChannel.Overlay + 1));
			Assert.That(channelCanvas.sortingOrder, Is.EqualTo((int)UIChannel.Overlay + 1));
			Assert.That(screen.GetComponents<Canvas>().Length, Is.EqualTo(1));
			Assert.That(screen.GetComponent<GraphicRaycaster>(), Is.Null);

			highlight.Restore();

			Assert.That(screen.GetComponents<Canvas>().Length, Is.EqualTo(1));
			Assert.That(channelCanvas.sortingOrder, Is.EqualTo((int)UIChannel.Overlay + 1), "sortingOrder belongs to the UISystem and is left alone");
		}

		[Test]
		public void Exit_WhenNotActive_DoesNothing()
		{
			var card = ShowCard();

			ViewHighlight.Exit(card);

			Assert.That(card.GetComponent<ViewHighlight>(), Is.Null);
			Assert.That(card.GetComponent<Canvas>(), Is.Null);
		}
	}
}
