using System;

namespace AK.Kernel.Formatting
{
	/// <summary>The suffixes a compact number uses for powers of a thousand.</summary>
	public enum NumberSuffixes : byte
	{
		/// <summary>
		/// K, M, B, T and Q, for 10^3 to 10^15. Past Q there is no suffix left, and the number
		/// before Q keeps growing: 10^18 is "1000Q".
		/// </summary>
		ShortScale = 0,

		/// <summary>
		/// K, M, B and T, then two letters from aa to zz: aa is 10^15, ab 10^18, and zz 10^2040,
		/// which covers every double.
		/// </summary>
		Letters = 1,
	}

	/// <summary>How <see cref="CompactNumber"/> writes a number.</summary>
	public readonly struct CompactNumberFormat
	{
		/// <summary>The most decimals a format shows. A double holds fifteen significant digits.</summary>
		public const int DecimalsLimit = 15;

		/// <summary>Decimals that are always shown, as zeros if need be.</summary>
		public readonly int MinDecimals;

		/// <summary>The most decimals shown. Zeros past <see cref="MinDecimals"/> are left off.</summary>
		public readonly int MaxDecimals;

		public readonly NumberRounding Rounding;

		public readonly NumberSuffixes Suffixes;

		/// <summary>
		/// Numbers below a thousand, which take no suffix, are shown without decimals. A number that
		/// rounds up to a thousand takes its suffix and the decimals again.
		/// </summary>
		public readonly bool WholeBelowThousand;

		public CompactNumberFormat(int minDecimals, int maxDecimals, NumberRounding rounding = NumberRounding.Nearest,
		                           NumberSuffixes suffixes = NumberSuffixes.ShortScale, bool wholeBelowThousand = false)
		{
			if (minDecimals < 0 || minDecimals > DecimalsLimit)
			{
				throw new ArgumentOutOfRangeException(nameof(minDecimals), minDecimals, $"Decimals go from 0 to {DecimalsLimit}.");
			}

			if (maxDecimals < minDecimals || maxDecimals > DecimalsLimit)
			{
				throw new ArgumentOutOfRangeException(nameof(maxDecimals), maxDecimals, $"The most decimals go from {nameof(minDecimals)} ({minDecimals}) to {DecimalsLimit}.");
			}

			if (rounding > NumberRounding.Up)
			{
				throw new ArgumentOutOfRangeException(nameof(rounding), rounding, null);
			}

			if (suffixes > NumberSuffixes.Letters)
			{
				throw new ArgumentOutOfRangeException(nameof(suffixes), suffixes, null);
			}

			MinDecimals        = minDecimals;
			MaxDecimals        = maxDecimals;
			Rounding           = rounding;
			Suffixes           = suffixes;
			WholeBelowThousand = wholeBelowThousand;
		}

		public override string ToString() =>
			$"{MinDecimals}-{MaxDecimals} decimals, {Rounding}, {Suffixes}{(WholeBelowThousand ? ", whole below a thousand" : "")}";
	}
}
