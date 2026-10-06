using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AK.Core
{
	/// <summary>
	/// A 128-bit identity value. This — not the <see cref="UID"/> asset — is what crosses
	/// every boundary: dictionaries, save files, server payloads, asset bundles, analytics.
	/// It has no Unity dependency, cannot be null, and cannot dangle: a Uid is meaningful
	/// whether or not any asset currently carries it.
	///
	/// Serializes as one string field (32 lowercase hex chars) so YAML and JSON diffs stay
	/// readable; the two ulong halves are the runtime representation that every comparison
	/// and hash touches.
	///
	/// There are deliberately NO implicit conversions to or from string or UID. Every
	/// crossing between identity and asset is explicit: <c>asset.Id</c> one way,
	/// <c>registry.TryResolve(uid, out asset)</c> the other.
	/// </summary>
	[Serializable]
	public struct Uid : IEquatable<Uid>, IComparable<Uid>, ISerializationCallbackReceiver
	{
		public const int ByteLength   = 16;
		public const int StringLength = 32;

		// The dashed form: 8-4-4-4-12 hex digits and four dashes.
		private const int DashedLength = 36;

		[SerializeField] private string _value;

		[NonSerialized] private ulong _hi;
		[NonSerialized] private ulong _lo;
		[NonSerialized] private bool  _parsed;

		public static readonly Uid None = default;

		private Uid(ulong hi, ulong lo)
		{
			_hi     = hi;
			_lo     = lo;
			_parsed = true;
			_value  = null;
		}

		/// <summary>True for the zero identity. A None Uid never resolves to anything.</summary>
		public bool IsNone
		{
			get
			{
				EnsureParsed();
				return (_hi | _lo) == 0;
			}
		}

		public bool IsSet => !IsNone;

		public ulong Hi { get { EnsureParsed(); return _hi; } }
		public ulong Lo { get { EnsureParsed(); return _lo; } }

		// ---------------------------------------------------------------- minting

		/// <summary>Random (UUIDv4) identity. For client-minted content.</summary>
		public static Uid NewRandom()
		{
			Span<byte> bytes = stackalloc byte[ByteLength];
			RandomNumberGenerator.Fill(bytes);

			bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
			bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

			return FromBytes(bytes);
		}

		/// <summary>
		/// Deterministic (UUIDv5-style: SHA-1 over namespace + name) identity. The same
		/// namespace and canonical name yield the same Uid on every machine — for closed
		/// vocabularies that must agree across projects and with a server.
		/// </summary>
		public static Uid Deterministic(Uid ns, string name)
		{
			if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name must not be empty.", nameof(name));

			byte[] nameBytes = Encoding.UTF8.GetBytes(name);
			byte[] input     = new byte[ByteLength + nameBytes.Length];
			ns.WriteBytes(input.AsSpan(0, ByteLength));
			nameBytes.CopyTo(input, ByteLength);

			byte[] hash;
			using (var sha1 = SHA1.Create())
			{
				hash = sha1.ComputeHash(input);
			}

			hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
			hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

			return FromBytes(new ReadOnlySpan<byte>(hash, 0, ByteLength));
		}

		/// <summary>
		/// Text-preserving bridge: the Uid reads the same as <c>guid.ToString("N")</c>.
		/// (<see cref="Guid"/>'s own byte layout is mixed-endian, so bytes are not the bridge.)
		/// </summary>
		public static Uid FromGuid(Guid guid)
		{
			Span<char> chars = stackalloc char[StringLength];
			guid.TryFormat(chars, out _, "N");
			TryParse(new string(chars), out Uid uid);
			return uid;
		}

		/// <summary>
		/// Bytes are network order: <c>bytes[0]</c> is the most significant byte of the text
		/// form, so the hex string, the byte form, and (hi, lo) all agree on every platform.
		/// </summary>
		public static Uid FromBytes(ReadOnlySpan<byte> bytes)
		{
			if (bytes.Length < ByteLength) throw new ArgumentException("Need 16 bytes.", nameof(bytes));

			ulong hi = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(0, 8));
			ulong lo = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(8, 8));
			return new Uid(hi, lo);
		}

		public static Uid FromParts(ulong hi, ulong lo) => new(hi, lo);

		// ---------------------------------------------------------------- parsing

		/// <summary>
		/// Accepts 32 hex chars, or the dashed 36-char form with its dashes in place (8-4-4-4-12),
		/// in any case: bare, or wrapped in matching braces or parentheses. White space around it
		/// is ignored. Returns false for anything else, including null and empty.
		/// </summary>
		public static bool TryParse(string s, out Uid uid)
		{
			uid = None;
			if (string.IsNullOrEmpty(s)) return false;

			ReadOnlySpan<char> span = s.AsSpan().Trim();
			if (span.Length >= 2 && (span[0] == '{' || span[0] == '('))
			{
				char close = span[0] == '{' ? '}' : ')';
				if (span[span.Length - 1] != close) return false;

				span = span.Slice(1, span.Length - 2);
			}

			bool dashed = span.Length == DashedLength;
			if (!dashed && span.Length != StringLength) return false;

			Span<byte> bytes = stackalloc byte[ByteLength];
			int written = 0;
			int nibble  = -1;

			for (int i = 0; i < span.Length; i++)
			{
				char c = span[i];
				if (dashed && (i == 8 || i == 13 || i == 18 || i == 23))
				{
					if (c != '-') return false;
					continue;
				}

				int v = HexValue(c);
				if (v < 0) return false;

				if (nibble < 0)
				{
					nibble = v;
				}
				else
				{
					bytes[written++] = (byte)((nibble << 4) | v);
					nibble = -1;
				}
			}

			uid = FromBytes(bytes);
			return true;
		}

		public static Uid Parse(string s)
		{
			if (!TryParse(s, out Uid uid))
			{
				throw new FormatException($"'{s}' is not a valid Uid.");
			}

			return uid;
		}

		// ---------------------------------------------------------------- formatting

		/// <summary>Canonical wire form: 32 lowercase hex characters, no dashes.</summary>
		public override string ToString()
		{
			EnsureParsed();

			Span<char> chars = stackalloc char[StringLength];
			WriteHex(_hi, chars.Slice(0, 16));
			WriteHex(_lo, chars.Slice(16, 16));
			return new string(chars);
		}

		/// <summary>Dashed RFC 4122 text, for systems that expect it.</summary>
		public string ToDashedString()
		{
			string n = ToString();
			return $"{n.Substring(0, 8)}-{n.Substring(8, 4)}-{n.Substring(12, 4)}-{n.Substring(16, 4)}-{n.Substring(20, 12)}";
		}

		/// <summary>First 8 hex chars. Not unique — for log lines only.</summary>
		public string ToShortString()
		{
			EnsureParsed();
			Span<char> chars = stackalloc char[8];
			for (int i = 0; i < 8; i++)
			{
				chars[i] = HexDigits[(int)((_hi >> (60 - i * 4)) & 0xF)];
			}

			return new string(chars);
		}

		/// <summary>Inverse of <see cref="FromGuid"/>: <c>ToGuid().ToString("N") == ToString()</c>.</summary>
		public Guid ToGuid() => Guid.ParseExact(ToString(), "N");

		public void WriteBytes(Span<byte> destination)
		{
			EnsureParsed();
			BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(0, 8), _hi);
			BinaryPrimitives.WriteUInt64BigEndian(destination.Slice(8, 8), _lo);
		}

		// ---------------------------------------------------------------- equality

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Uid other)
		{
			EnsureParsed();
			other.EnsureParsed();
			return _hi == other._hi && _lo == other._lo;
		}

		public override bool Equals(object obj) => obj is Uid other && Equals(other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
		{
			EnsureParsed();
			ulong x = _hi ^ (_lo * 0x9E3779B97F4A7C15UL);
			x ^= x >> 32;
			return (int)x;
		}

		public int CompareTo(Uid other)
		{
			EnsureParsed();
			other.EnsureParsed();
			int c = _hi.CompareTo(other._hi);
			return c != 0 ? c : _lo.CompareTo(other._lo);
		}

		public static bool operator ==(Uid a, Uid b) => a.Equals(b);
		public static bool operator !=(Uid a, Uid b) => !a.Equals(b);

		// ---------------------------------------------------------------- serialization

		void ISerializationCallbackReceiver.OnBeforeSerialize()
		{
			if (_parsed)
			{
				_value = (_hi | _lo) == 0 ? string.Empty : ToString();
			}
		}

		void ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			_parsed = false;
			EnsureParsed();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void EnsureParsed()
		{
			if (_parsed) return;

			if (TryParse(_value, out Uid parsed))
			{
				_hi = parsed._hi;
				_lo = parsed._lo;
			}
			else
			{
				_hi = 0;
				_lo = 0;
			}

			_parsed = true;
		}

		// ---------------------------------------------------------------- helpers

		private const string HexDigits = "0123456789abcdef";

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int HexValue(char c)
		{
			if (c >= '0' && c <= '9') return c - '0';
			if (c >= 'a' && c <= 'f') return c - 'a' + 10;
			if (c >= 'A' && c <= 'F') return c - 'A' + 10;
			return -1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void WriteHex(ulong value, Span<char> destination)
		{
			for (int i = 0; i < 16; i++)
			{
				destination[i] = HexDigits[(int)((value >> (60 - i * 4)) & 0xF)];
			}
		}
	}
}
