using System.Collections.Generic;
using AK.Tests.Support;
using AK.UI;
using AK.Utilities;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AK.Tests.Utilities
{
	/// <summary>
	/// The cooldown button counted down by hand: edit mode runs no Update, so the tests tick it
	/// themselves.
	/// </summary>
	public class CooldownButtonTests
	{
		private readonly List<Object> _created = new();

		private CooldownButton  _button;
		private Image           _overlay;
		private TextMeshProUGUI _text;

		[SetUp]
		public void SetUp()
		{
			GameObject go = Track(new GameObject("Cooldown", typeof(RectTransform)));
			go.AddComponent<Image>();
			go.AddComponent<Button>();
			_button = go.AddComponent<CooldownButton>();

			_overlay = Track(new GameObject("Overlay", typeof(RectTransform))).AddComponent<Image>();
			_text    = Track(new GameObject("Text", typeof(RectTransform))).AddComponent<TextMeshProUGUI>();

			var serialized = new SerializedObject(_button);
			serialized.FindProperty("_cooldownDuration").floatValue       = 1f;
			serialized.FindProperty("_overlayImage").objectReferenceValue = _overlay;
			serialized.FindProperty("_timerText").objectReferenceValue    = _text;
			serialized.FindProperty("_textFormat").intValue               = (int)TimeFormat.Abbreviated;
			serialized.FindProperty("_rounding").intValue                 = (int)TimeRounding.Ceil;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		[TearDown]
		public void TearDown()
		{
			for (int i = _created.Count - 1; i >= 0; i--)
			{
				if (_created[i] != null) Object.DestroyImmediate(_created[i]);
			}

			_created.Clear();
		}

		private T Track<T>(T obj) where T : Object
		{
			_created.Add(obj);
			return obj;
		}

		[Test]
		public void TheOverlay_EmptiesOverTheCooldownStarted_NotTheButtonsOwnLength()
		{
			_button.StartCooldown(4f);
			Assert.AreEqual(1f, _overlay.fillAmount, 1e-5f, "full from the start");

			_button.Tick(2f);
			Assert.AreEqual(0.5f, _overlay.fillAmount, 1e-5f);
		}

		[Test]
		public void TheText_CountsDown_AndClearsAtTheEnd()
		{
			_button.StartCooldown(3f);
			Assert.AreEqual("3s", _text.text);

			_button.Tick(0.5f);
			Assert.AreEqual("3s", _text.text, "rounded up");

			_button.Tick(1f);
			Assert.AreEqual("2s", _text.text);

			_button.Tick(2f);
			Assert.AreEqual(string.Empty, _text.text);
			Assert.IsFalse(_text.gameObject.activeSelf);
			Assert.IsFalse(_overlay.gameObject.activeSelf);
		}

		[Test]
		public void AThousandTicks_ThatLeaveTheTextAsItIs_AllocateNothing()
		{
			_button.StartCooldown(10_000f);

			void Ticks()
			{
				for (int i = 0; i < 1000; i++)
				{
					_button.Tick(0.001f);
				}
			}

			Ticks();
			int allocations = GcAllocations.Count(Ticks);

			Assert.AreEqual("3h", _text.text);
			Assert.AreEqual(0, allocations);
		}
	}
}
