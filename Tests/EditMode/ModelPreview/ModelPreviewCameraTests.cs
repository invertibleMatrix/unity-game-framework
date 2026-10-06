using System.Collections.Generic;
using AK.Utilities.Previews;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using Object = UnityEngine.Object;

namespace AK.Tests.Previews
{
	public class ModelPreviewCameraTests
	{
		private const float Frame60 = 1f / 60f;

		private ModelPreviewCamera _rig;
		private RenderTexture _texture;

		[SetUp]
		public void SetUp()
		{
			_rig = PreviewRig.CreateStage();
			_texture = new RenderTexture(256, 256, 16);
			_rig.SetTargetTexture(_texture);
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_rig.gameObject);
			Object.DestroyImmediate(_texture);
		}

		[Test]
		public void Frame_FitsTheBoundingSphere()
		{
			// A 2x2x2 box: sphere radius sqrt(3). Square texture, 60 degree vertical FOV.
			_rig.Frame(new Bounds(Vector3.zero, Vector3.one * 2f), 1f);

			float expected = Mathf.Sqrt(3f) / Mathf.Tan(30f * Mathf.Deg2Rad);
			Assert.AreEqual(expected, Vector3.Distance(_rig.Camera.transform.position, Vector3.zero), 1e-3f);
		}

		[Test]
		public void Frame_KeepsTheAuthoredViewDirection_AcrossReframes()
		{
			Vector3 authored = PreviewRig.CameraOffset.normalized;

			_rig.Frame(new Bounds(new Vector3(0f, 1f, 0f), Vector3.one), 1f);
			Assert.That(DirectionFrom(new Vector3(0f, 1f, 0f)), Is.EqualTo(authored).Using(ApproximatelyEqual));

			_rig.Frame(new Bounds(new Vector3(0f, -2f, 0f), Vector3.one * 3f), 1.2f);
			Assert.That(DirectionFrom(new Vector3(0f, -2f, 0f)), Is.EqualTo(authored).Using(ApproximatelyEqual),
				"a reframe must not take its direction from where the last framing left the camera");
		}

		[Test]
		public void Zoom_IsClampedToItsLimits()
		{
			_rig.SetStatic(false, 1);
			_rig.Frame(new Bounds(Vector3.zero, Vector3.one), 1f);
			float framed = Distance();

			_rig.ZoomBy(100f);
			Run(240);
			Assert.AreEqual(framed * 2f, Distance(), 1e-3f, "zoomed out to the far limit");

			_rig.ZoomBy(0.0001f);
			Run(240);
			Assert.AreEqual(framed * 0.5f, Distance(), 1e-3f, "zoomed in to the near limit");
		}

		[Test]
		public void Static_StopsRendering_AfterItsWarmup()
		{
			_rig.SetStatic(true, 3);
			Assert.IsTrue(_rig.Camera.enabled);

			Run(2);
			Assert.IsTrue(_rig.Camera.enabled);

			Run(1);
			Assert.IsFalse(_rig.Camera.enabled);
		}

		[Test]
		public void Static_RendersAgain_WhileTheViewMoves()
		{
			_rig.SetStatic(true, 3);
			Run(3);
			Assert.IsFalse(_rig.Camera.enabled);

			_rig.RotateBy(30f, 0f);
			Assert.IsTrue(_rig.Camera.enabled, "a view change wakes the camera at once");

			Run(10);
			Assert.IsTrue(_rig.Camera.enabled, "still turning");

			Run(120);
			Assert.IsFalse(_rig.Camera.enabled, "settled, so it stops again");
		}

		[Test]
		public void Static_Hold_KeepsRendering_UntilReleased()
		{
			_rig.SetStatic(true, 1);
			_rig.BeginHold();

			Run(30);
			Assert.IsTrue(_rig.Camera.enabled, "held, e.g. by an intro");

			_rig.EndHold();
			Run(1);
			Assert.IsFalse(_rig.Camera.enabled);
		}

		[Test]
		public void Static_AutoRotate_KeepsRendering()
		{
			_rig.SetStatic(true, 1);
			_rig.SetAutoRotate(45f);

			Run(30);
			Assert.IsTrue(_rig.Camera.enabled);

			_rig.SetAutoRotate(0f);
			Run(120);
			Assert.IsFalse(_rig.Camera.enabled);
		}

		[Test]
		public void Live_NeverStopsRendering()
		{
			_rig.SetStatic(false, 1);
			Run(30);
			Assert.IsTrue(_rig.Camera.enabled);
		}

		[Test]
		public void Rotation_SettlesExactlyOnTarget()
		{
			_rig.SetStatic(false, 1);
			_rig.RotateBy(90f, 20f);
			Run(240);

			Vector3 euler = _rig.Pivot.localRotation.eulerAngles;
			Assert.AreEqual(90f, euler.y, 1e-3f);
			Assert.AreEqual(20f, euler.x, 1e-3f);
		}

		[Test]
		public void LongAutoRotate_StillTakesSmallTurnsExactly()
		{
			_rig.SetStatic(false, 1);
			_rig.SetAutoRotate(360f);
			_rig.Tick(100000f); // over a day of turning in one step: 36 million degrees
			_rig.SetAutoRotate(0f);

			_rig.RotateBy(0.5f, 0f);
			Run(240);

			Assert.AreEqual(0.5f, Yaw(), 1e-3f, "that far out, float precision would swallow a half-degree turn");
		}

		[Test]
		public void ResetView_TurnsBackTheShortWay()
		{
			_rig.SetStatic(false, 1);
			_rig.RotateBy(300f, 20f);
			Run(240);
			float turned = Yaw(); // -60: 300 degrees round is 60 short of a full turn

			_rig.ResetView();
			_rig.Tick(Frame60);
			Assert.Greater(Yaw(), turned, "on through the remaining 60 degrees, not 300 back");

			Run(240);
			Assert.AreEqual(0f, Yaw(), 1e-3f);
			Assert.AreEqual(0f, Mathf.DeltaAngle(0f, _rig.Pivot.localRotation.eulerAngles.x), 1e-3f);
		}

		private void Run(int frames)
		{
			for (int i = 0; i < frames; i++)
			{
				_rig.Tick(Frame60);
			}
		}

		private float Distance() => Vector3.Distance(_rig.Camera.transform.position, _rig.Pivot.position);

		/// <summary>The turntable's yaw in (-180, 180].</summary>
		private float Yaw() => Mathf.DeltaAngle(0f, _rig.Pivot.localRotation.eulerAngles.y);

		private Vector3 DirectionFrom(Vector3 centre) => (_rig.Camera.transform.position - centre).normalized;

		private static readonly IEqualityComparer<Vector3> ApproximatelyEqual = Vector3EqualityComparer.Instance;
	}
}
