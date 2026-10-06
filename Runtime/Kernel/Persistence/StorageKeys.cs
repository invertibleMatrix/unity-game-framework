using System;
using System.Globalization;

namespace AK.Kernel.Persistence
{
	/// <summary>
	/// How stored data is named.
	/// <list type="bullet">
	/// <item>A key is a non-empty string without control characters.</item>
	/// <item>Data that belongs to one account, or another scope, is stored under
	/// <c>{key}@{scope}</c>. The bare key holds the device's data.</item>
	/// <item>A value set aside as unreadable is kept under <c>{key}.corrupt.{utc}</c>, the time
	/// written as <c>yyyyMMddTHHmmssfffZ</c>. A second one set aside within the same
	/// millisecond gets <c>-2</c> appended, a third <c>-3</c>, and so on.</item>
	/// <item><see cref="Index"/> is reserved for the list of keys a <see cref="RecordStore"/> owns.</item>
	/// </list>
	/// </summary>
	public static class StorageKeys
	{
		/// <summary>The reserved key under which a <see cref="RecordStore"/> lists the keys it owns.</summary>
		public const string Index = "UGFW.keys";

		/// <summary>Separates a key from the scope its data belongs to.</summary>
		public const char ScopeSeparator = '@';

		/// <summary>Separates a key from the time its value was set aside as unreadable.</summary>
		public const string QuarantineInfix = ".corrupt.";

		private const string QuarantineTimeFormat = "yyyyMMdd'T'HHmmssfff'Z'";
		private const int    QuarantineTimeLength = 19;
		private const int    QuarantineTimeT      = 8;

		/// <summary>True when <paramref name="key"/> can name stored data: non-empty, free of control characters, and not <see cref="Index"/>.</summary>
		public static bool IsValid(string key) => IsWellFormed(key) && !IsIndex(key);

		/// <summary>Throws unless <paramref name="key"/> can name stored data (see <see cref="IsValid"/>).</summary>
		/// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="key"/> is empty, has a control character, or is <see cref="Index"/>.</exception>
		public static void Validate(string key)
		{
			if (key == null) throw new ArgumentNullException(nameof(key));
			if (!IsWellFormed(key)) throw new ArgumentException("A storage key must be non-empty and free of control characters.", nameof(key));
			if (IsIndex(key)) throw new ArgumentException($"'{Index}' is reserved for the store's own list of keys.", nameof(key));
		}

		/// <summary>Throws unless <paramref name="scope"/> can scope a key: non-empty and free of control characters.</summary>
		/// <exception cref="ArgumentNullException"><paramref name="scope"/> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="scope"/> is empty or has a control character.</exception>
		public static void ValidateScope(string scope)
		{
			if (scope == null) throw new ArgumentNullException(nameof(scope));
			if (!IsWellFormed(scope)) throw new ArgumentException("A scope must be non-empty and free of control characters.", nameof(scope));
		}

		/// <summary>
		/// The key holding <paramref name="key"/>'s data for <paramref name="scope"/>, such as an
		/// account id: <c>{key}@{scope}</c>. A null scope means the device's data, under
		/// <paramref name="key"/> itself.
		/// </summary>
		public static string Scoped(string key, string scope)
		{
			Validate(key);
			if (scope == null) return key;

			ValidateScope(scope);
			return key + ScopeSeparator + scope;
		}

		/// <summary>
		/// The key a value of <paramref name="key"/> is set aside under when it is found
		/// unreadable at <paramref name="utc"/>. A local time is converted to UTC; an unspecified
		/// one is taken as UTC.
		/// </summary>
		public static string Quarantined(string key, DateTime utc)
		{
			Validate(key);
			if (utc.Kind == DateTimeKind.Local) utc = utc.ToUniversalTime();

			return key + QuarantineInfix + utc.ToString(QuarantineTimeFormat, CultureInfo.InvariantCulture);
		}

		/// <summary>True when <paramref name="candidate"/> names a value of <paramref name="key"/> set aside as unreadable.</summary>
		public static bool IsQuarantineOf(string candidate, string key)
		{
			if (candidate == null || key == null) return false;

			int time = key.Length + QuarantineInfix.Length;
			int end  = time + QuarantineTimeLength;
			if (candidate.Length < end) return false;
			if (string.CompareOrdinal(candidate, 0, key, 0, key.Length) != 0) return false;
			if (string.CompareOrdinal(candidate, key.Length, QuarantineInfix, 0, QuarantineInfix.Length) != 0) return false;

			for (int i = 0; i < QuarantineTimeLength; i++)
			{
				char c = candidate[time + i];
				bool expected = i == QuarantineTimeT          ? c == 'T'
				              : i == QuarantineTimeLength - 1 ? c == 'Z'
				              : IsDigit(c);
				if (!expected) return false;
			}

			if (candidate.Length == end) return true;

			// A same-millisecond suffix: "-" and digits.
			if (candidate[end] != '-' || candidate.Length == end + 1) return false;
			for (int i = end + 1; i < candidate.Length; i++)
			{
				if (!IsDigit(candidate[i])) return false;
			}

			return true;
		}

		private static bool IsWellFormed(string text)
		{
			if (string.IsNullOrEmpty(text)) return false;

			for (int i = 0; i < text.Length; i++)
			{
				if (char.IsControl(text[i])) return false;
			}

			return true;
		}

		private static bool IsIndex(string key) => string.Equals(key, Index, StringComparison.Ordinal);

		private static bool IsDigit(char c) => c >= '0' && c <= '9';
	}
}
