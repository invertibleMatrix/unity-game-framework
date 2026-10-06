using System.Collections;
using AK.Kernel.Timing;
using AK.Systems;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AK.Tests.Cameras
{
	/// <summary>
	/// A shake moves the camera, and when it ends leaves it where it would be without it, wherever
	/// something else moved it meanwhile. On scaled time it holds while the game is paused; on
	/// unscaled time it doesn't.
	/// </summary>
	public class CameraShakePlayModeTests
	{
		private const float Intensity        = 0.5f;
		private const float Tolerance        = 1e-4f;
		private const float WaitLimitSeconds = 10f;

		private static readonly Vector3 Rest = new(1f, 2f, 3f);

		/// <summary>Shakes on unscaled time.</summary>
		private sealed class UnscaledShakeCamera : BaseCamera
		{
			protected override void Awake()
			{
				_shakeTime = TimeDomain.Unscaled;
				base.Awake();
			}
		}

		private BaseCamera _camera;
		private float      _timeScale;

		[SetUp]
		public void SetUp()
		{
			_timeScale     = Time.timeScale;
			Time.timeScale = 1f;
		}

		[TearDown]
		public void TearDown()
		{
			if (_camera != null) Object.DestroyImmediate(_camera.gameObject);
			Time.timeScale = _timeScale;
		}

		private T Make<T>() where T : BaseCamera
		{
			var camera = new GameObject("Shaken").AddComponent<T>();
			camera.transform.localPosition = Rest;
			_camera = camera;
			return camera;
		}

		private Vector3 Position => _camera.transform.localPosition;

		private bool IsAt(Vector3 position) => Vector3.Distance(position, Position) < Tolerance;

		// Waits out seconds of game time; fails if the game clock doesn't get there.
		private static IEnumerator WaitGameTime(float seconds)
		{
			float until = Time.time + seconds;
			float limit = Time.realtimeSinceStartup + WaitLimitSeconds;
			while (Time.time < until)
			{
				Assert.Less(Time.realtimeSinceStartup, limit, "the game clock stalled");
				yield return null;
			}

			// The shake ends at the first Update past its duration.
			yield return null;
		}

		[UnityTest]
		public IEnumerator AShake_EndsWhereTheCameraWouldBeWithoutIt()
		{
			Make<BaseCamera>().Shake(Intensity, 0.2f);
			Assert.IsFalse(IsAt(Rest), "the shake moves the camera at once");

			yield return null;
			_camera.transform.localPosition += Vector3.right * 10f;

			yield return WaitGameTime(0.2f);

			Assert.IsTrue(IsAt(Rest + Vector3.right * 10f), $"ended at {Position}");
		}

		[UnityTest]
		public IEnumerator AShakeStartedDuringAnother_RunsAsLongAsTheLonger()
		{
			Make<BaseCamera>().Shake(Intensity, 0.1f);
			_camera.Shake(Intensity * 0.5f, 0.4f);

			yield return WaitGameTime(0.2f);
			Assert.IsFalse(IsAt(Rest), "still shaking");

			yield return WaitGameTime(0.2f);
			Assert.IsTrue(IsAt(Rest), $"ended at {Position}");
		}

		[UnityTest]
		public IEnumerator DeactivatingAShakingCamera_PutsItBackAtOnce()
		{
			Make<BaseCamera>().Shake(Intensity, 10f);
			yield return null;

			_camera.gameObject.SetActive(false);
			Assert.IsTrue(IsAt(Rest), $"put back at {Position}");

			_camera.gameObject.SetActive(true);
			yield return null;
			yield return null;
			Assert.IsTrue(IsAt(Rest), "the shake ended with the camera");
		}

		[UnityTest]
		public IEnumerator AShakeOnScaledTime_HoldsWhileTheGameIsPaused()
		{
			Time.timeScale = 0f;
			Make<BaseCamera>().Shake(Intensity, 0.1f);

			yield return new WaitForSecondsRealtime(0.3f);
			Assert.IsFalse(IsAt(Rest), "held, not ended");

			Time.timeScale = 1f;
			yield return WaitGameTime(0.1f);
			Assert.IsTrue(IsAt(Rest), $"ended at {Position}");
		}

		[UnityTest]
		public IEnumerator AShakeOnUnscaledTime_EndsWhileTheGameIsPaused()
		{
			Time.timeScale = 0f;
			Make<UnscaledShakeCamera>().Shake(Intensity, 0.1f);

			float limit = Time.realtimeSinceStartup + WaitLimitSeconds;
			while (!IsAt(Rest))
			{
				Assert.Less(Time.realtimeSinceStartup, limit, "the shake never ended");
				yield return null;
			}
		}

		[Test]
		public void AShake_OnAnInactiveCameraOrWithNothingToDo_DoesNothing()
		{
			Make<BaseCamera>().gameObject.SetActive(false);
			_camera.Shake(Intensity, 1f);
			Assert.IsTrue(IsAt(Rest));

			_camera.gameObject.SetActive(true);
			_camera.Shake(0f, 1f);
			_camera.Shake(Intensity, 0f);
			_camera.Shake(float.NaN, 1f);
			Assert.IsTrue(IsAt(Rest));
		}
	}
}
