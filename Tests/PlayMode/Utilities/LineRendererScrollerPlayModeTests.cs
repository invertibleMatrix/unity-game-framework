using System.Collections;
using AK.Utilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AK.Tests.Utilities
{
	public class LineRendererScrollerPlayModeTests
	{
		[UnityTest]
		public IEnumerator TheLine_ScrollsAMaterialOfItsOwn_DestroyedWithTheComponent()
		{
			var shared = new Material(Shader.Find("Sprites/Default"));
			var go = new GameObject("Line");
			var line = go.AddComponent<LineRenderer>();
			line.sharedMaterial = shared;

			go.AddComponent<LineRendererScroller>();
			Material own = line.sharedMaterial;

			try
			{
				Assert.AreNotSame(shared, own, "the line got a copy of its own");

				yield return null;
				yield return null;

				Assert.AreEqual(Vector2.zero, shared.mainTextureOffset, "the shared material stays still");
				Assert.That(own.mainTextureOffset.x, Is.InRange(-1f, 1f), "the offset wraps");
				Assert.AreNotEqual(0f, own.mainTextureOffset.x);

				Object.Destroy(go);
				yield return null;

				Assert.IsTrue(own == null, "destroyed with the component");
			}
			finally
			{
				if (go != null) Object.Destroy(go);
				Object.Destroy(shared);
			}
		}
	}
}
