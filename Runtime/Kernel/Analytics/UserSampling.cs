using System;

namespace AK.Kernel.Analytics
{
	/// <summary>
	/// Deterministic per-user sampling. Each user gets a stable bucket in [0, 1) from their id, and
	/// an event sampled at rate <c>r</c> reaches exactly the users whose bucket is below <c>r</c>. A
	/// user is in or out on every session and every device, and rates nest: everyone in a 10% sample
	/// is in every larger one, so a funnel across differently sampled events keeps whole users.
	///
	/// <para>The bucket is the 32-bit FNV-1a hash of the id's UTF-8 bytes, finished with
	/// MurmurHash3's fmix32 and divided by 2^32. An unpaired surrogate encodes as U+FFFD, as the
	/// .NET UTF-8 encoder does. A port must reproduce these steps exactly, or the same user lands in
	/// different samples on different engines. Hashing allocates nothing.</para>
	/// </summary>
	public static class UserSampling
	{
		private const uint FnvOffsetBasis = 2166136261u;
		private const uint FnvPrime       = 16777619u;
		private const int  Replacement    = 0xFFFD;
		private const double TwoToThe32   = 4294967296.0;

		/// <summary>The id's 32-bit sampling hash.</summary>
		/// <exception cref="ArgumentNullException"><paramref name="userId"/> is null.</exception>
		public static uint Hash(string userId)
		{
			if (userId == null) throw new ArgumentNullException(nameof(userId));

			uint hash = FnvOffsetBasis;
			for (int i = 0; i < userId.Length; i++)
			{
				char c = userId[i];
				int codePoint = c;
				if (char.IsHighSurrogate(c) && i + 1 < userId.Length && char.IsLowSurrogate(userId[i + 1]))
				{
					codePoint = char.ConvertToUtf32(c, userId[i + 1]);
					i++;
				}
				else if (char.IsSurrogate(c))
				{
					codePoint = Replacement;
				}

				hash = AppendUtf8(hash, codePoint);
			}

			return Finish(hash);
		}

		/// <summary>The id's bucket, in [0, 1).</summary>
		/// <exception cref="ArgumentNullException"><paramref name="userId"/> is null.</exception>
		public static double Bucket(string userId) => Hash(userId) / TwoToThe32;

		/// <summary>
		/// True when a user in <paramref name="bucket"/> is in a sample of <paramref name="rate"/>.
		/// A rate of 1 or more takes everyone; 0, less or NaN takes no one.
		/// </summary>
		public static bool Includes(float rate, double bucket)
		{
			if (rate >= 1f) return true;
			if (!(rate > 0f)) return false;
			return bucket < rate;
		}

		private static uint AppendUtf8(uint hash, int codePoint)
		{
			if (codePoint < 0x80)
			{
				return Step(hash, codePoint);
			}

			if (codePoint < 0x800)
			{
				hash = Step(hash, 0xC0 | (codePoint >> 6));
				return Step(hash, 0x80 | (codePoint & 0x3F));
			}

			if (codePoint < 0x10000)
			{
				hash = Step(hash, 0xE0 | (codePoint >> 12));
				hash = Step(hash, 0x80 | ((codePoint >> 6) & 0x3F));
				return Step(hash, 0x80 | (codePoint & 0x3F));
			}

			hash = Step(hash, 0xF0 | (codePoint >> 18));
			hash = Step(hash, 0x80 | ((codePoint >> 12) & 0x3F));
			hash = Step(hash, 0x80 | ((codePoint >> 6) & 0x3F));
			return Step(hash, 0x80 | (codePoint & 0x3F));
		}

		private static uint Step(uint hash, int utf8Byte) => unchecked((hash ^ (uint)utf8Byte) * FnvPrime);

		// MurmurHash3's fmix32: FNV-1a alone mixes its last bytes poorly into the high bits.
		private static uint Finish(uint hash)
		{
			unchecked
			{
				hash ^= hash >> 16;
				hash *= 0x85EBCA6Bu;
				hash ^= hash >> 13;
				hash *= 0xC2B2AE35u;
				hash ^= hash >> 16;
				return hash;
			}
		}
	}
}
