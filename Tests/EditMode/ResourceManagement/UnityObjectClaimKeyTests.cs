using AK.Kernel.Collections;
using NUnit.Framework;
using UnityEngine;

namespace AK.Tests.ResourceManagement
{
	/// <summary>
	/// The Addressables strategy keys its claims on the loaded objects themselves, and must still
	/// find a claim after the object is destroyed, so it can give the claim back. That rests on
	/// Unity's Object equality: hash and equality follow the instance id, alive or not.
	/// </summary>
	public class UnityObjectClaimKeyTests
	{
		[Test]
		public void DestroyedObject_StillFindsItsClaims()
		{
			var claims = new ClaimTable<Object, int>();
			var target = new GameObject("ClaimKey");
			claims.Acquire(target, 1);
			claims.Acquire(target, 2);

			Object.DestroyImmediate(target);

			Assert.IsTrue(target == null, "Unity reports the object as destroyed");
			Assert.IsTrue(claims.Contains(target));
			Assert.AreEqual(2, claims.ClaimsOf(target));
			Assert.IsTrue(claims.TryRelease(target, out int handle));
			Assert.AreEqual(2, handle);
		}

		[Test]
		public void DistinctObjects_AreDistinctKeys_EvenWithTheSameName()
		{
			var claims = new ClaimTable<Object, int>();
			var first = new GameObject("Same");
			var second = new GameObject("Same");
			try
			{
				claims.Acquire(first, 1);
				claims.Acquire(second, 2);

				Assert.AreEqual(2, claims.Count);
				Assert.AreEqual(1, claims.ClaimsOf(first));
			}
			finally
			{
				Object.DestroyImmediate(first);
				Object.DestroyImmediate(second);
			}
		}
	}
}
