using System.Collections.Generic;
using AK.Core.Extensions;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests
{
	public class TransformExtTests
	{
		private readonly List<Object> _created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (Object o in _created)
			{
				if (o != null) Object.DestroyImmediate(o);
			}

			_created.Clear();
		}

		private Transform Make(string name)
		{
			var go = new GameObject(name);
			_created.Add(go);
			return go.transform;
		}

		private RectTransform MakeRect(string name)
		{
			var go = new GameObject(name, typeof(RectTransform));
			_created.Add(go);
			return (RectTransform)go.transform;
		}

		[Test]
		public void MatchLocalPose_CopiesPositionRotationAndScale()
		{
			Transform source = Make("Source");
			source.SetLocalPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30));
			source.localScale = new Vector3(2, 3, 4);
			Transform target = Make("Target");

			target.MatchLocalPose(source);

			Assert.AreEqual(new Vector3(1, 2, 3), target.localPosition);
			Assert.Less(Quaternion.Angle(source.localRotation, target.localRotation), 0.01f);
			Assert.AreEqual(new Vector3(2, 3, 4), target.localScale);
		}

		[Test]
		public void MatchLocalPose_BetweenRectTransforms_CopiesTheirLayout()
		{
			RectTransform source = MakeRect("Source");
			source.anchorMin = new Vector2(0.1f, 0.2f);
			source.anchorMax = new Vector2(0.8f, 0.9f);
			source.pivot = new Vector2(0, 1);
			source.sizeDelta = new Vector2(-20, 40);
			source.anchoredPosition3D = new Vector3(5, 6, 7);
			source.localRotation = Quaternion.Euler(0, 0, 15);
			source.localScale = new Vector3(0.5f, 0.5f, 1);
			RectTransform target = MakeRect("Target");

			target.MatchLocalPose(source);

			Assert.AreEqual(source.anchorMin, target.anchorMin);
			Assert.AreEqual(source.anchorMax, target.anchorMax);
			Assert.AreEqual(source.pivot, target.pivot);
			Assert.AreEqual(source.sizeDelta, target.sizeDelta);
			Assert.AreEqual(source.anchoredPosition3D, target.anchoredPosition3D);
			Assert.Less(Quaternion.Angle(source.localRotation, target.localRotation), 0.01f);
			Assert.AreEqual(source.localScale, target.localScale);
		}
	}
}
