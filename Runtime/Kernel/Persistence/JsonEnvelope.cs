using System;

namespace AK.Kernel.Persistence
{
	/// <summary>
	/// Checks saved JSON before a lenient reader sees it. Unity's JsonUtility, for one, reads
	/// <c>{}</c>, a missing member or a member of the wrong kind as defaults without complaint,
	/// and the next save then overwrites the data it couldn't read.
	///
	/// <see cref="Inspect"/> validates the whole text strictly, to RFC 8259 plus the NaN,
	/// Infinity and -Infinity tokens JsonUtility writes for non-finite floats, and reports the
	/// kind of one top-level member. It checks shape, not schema: nothing below the top level
	/// is compared with a type. Allocation-free, with recursion bounded by <see cref="MaxDepth"/>.
	/// </summary>
	public static class JsonEnvelope
	{
		/// <summary>The deepest nesting of objects and arrays accepted. JsonUtility's own output stays far below it.</summary>
		public const int MaxDepth = 128;

		/// <summary>
		/// Validates <paramref name="json"/> as exactly one JSON value with nothing but whitespace
		/// around it. When the value is an object, <paramref name="memberKind"/> is the kind of
		/// its top-level member named <paramref name="member"/>, compared after unescaping
		/// (the last one wins if the name repeats), or <see cref="JsonKind.None"/> when it has no
		/// such member. Otherwise <paramref name="memberKind"/> is None.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="member"/> is null.</exception>
		public static JsonShape Inspect(string json, string member, out JsonKind memberKind)
		{
			if (member == null) throw new ArgumentNullException(nameof(member));

			memberKind = JsonKind.None;
			if (json == null) return JsonShape.Malformed;

			var scanner = new Scanner(json, member);
			scanner.SkipWhitespace();
			if (!scanner.Value(1, true, out JsonKind root)) return JsonShape.Malformed;

			scanner.SkipWhitespace();
			if (!scanner.AtEnd) return JsonShape.Malformed;
			if (root != JsonKind.Object) return JsonShape.NotAnObject;

			memberKind = scanner.MemberKind;
			return JsonShape.Object;
		}

		/// <summary>
		/// A recursive-descent validator over one text. Each method starts at the first character
		/// of what it reads and, on success, leaves the position just past it. A false return
		/// means the text is malformed; the position is then meaningless.
		/// </summary>
		private struct Scanner
		{
			private readonly string _text;
			private readonly string _member;
			private int _position;

			/// <summary>The kind of the root object's member named <c>_member</c>, if it has one.</summary>
			public JsonKind MemberKind;

			public Scanner(string text, string member)
			{
				_text      = text;
				_member    = member;
				_position  = 0;
				MemberKind = JsonKind.None;
			}

			public bool AtEnd => _position >= _text.Length;

			public void SkipWhitespace()
			{
				while (_position < _text.Length)
				{
					char c = _text[_position];
					if (c != ' ' && c != '\t' && c != '\n' && c != '\r') return;
					_position++;
				}
			}

			/// <summary>Reads one value. <paramref name="depth"/> is the nesting level a container here would have.</summary>
			public bool Value(int depth, bool isRoot, out JsonKind kind)
			{
				kind = JsonKind.None;
				if (AtEnd) return false;

				switch (_text[_position])
				{
					case '{':
						kind = JsonKind.Object;
						return Object(depth, isRoot);
					case '[':
						kind = JsonKind.Array;
						return Array(depth);
					case '"':
						kind = JsonKind.String;
						return String(out _, out _, out _);
					case 't':
						kind = JsonKind.Boolean;
						return Word("true");
					case 'f':
						kind = JsonKind.Boolean;
						return Word("false");
					case 'n':
						kind = JsonKind.Null;
						return Word("null");
					default:
						kind = JsonKind.Number;
						return Number();
				}
			}

			private bool Object(int depth, bool isRoot)
			{
				if (depth > MaxDepth) return false;

				_position++;
				SkipWhitespace();
				if (Next('}')) return true;

				while (true)
				{
					if (AtEnd || _text[_position] != '"') return false;
					if (!String(out int nameStart, out int nameEnd, out bool escaped)) return false;

					SkipWhitespace();
					if (!Next(':')) return false;

					SkipWhitespace();
					if (!Value(depth + 1, false, out JsonKind kind)) return false;

					if (isRoot && NameIsMember(nameStart, nameEnd, escaped)) MemberKind = kind;

					SkipWhitespace();
					if (Next('}')) return true;
					if (!Next(',')) return false;

					SkipWhitespace();
				}
			}

			private bool Array(int depth)
			{
				if (depth > MaxDepth) return false;

				_position++;
				SkipWhitespace();
				if (Next(']')) return true;

				while (true)
				{
					if (!Value(depth + 1, false, out _)) return false;

					SkipWhitespace();
					if (Next(']')) return true;
					if (!Next(',')) return false;

					SkipWhitespace();
				}
			}

			/// <summary>Reads a string. Its raw content, escapes still in, is <c>[start, end)</c>.</summary>
			private bool String(out int start, out int end, out bool escaped)
			{
				start   = ++_position;
				end     = start;
				escaped = false;

				while (_position < _text.Length)
				{
					char c = _text[_position];

					if (c == '"')
					{
						end = _position++;
						return true;
					}

					// Control characters must be escaped.
					if (c < ' ') return false;

					_position++;
					if (c != '\\') continue;

					escaped = true;
					if (AtEnd) return false;

					switch (_text[_position])
					{
						case '"':
						case '\\':
						case '/':
						case 'b':
						case 'f':
						case 'n':
						case 'r':
						case 't':
							_position++;
							break;
						case 'u':
							if (_position + 4 >= _text.Length) return false;
							for (int i = 1; i <= 4; i++)
							{
								if (HexValue(_text[_position + i]) < 0) return false;
							}

							_position += 5;
							break;
						default:
							return false;
					}
				}

				return false;
			}

			private bool Number()
			{
				bool negative = Next('-');
				if (Word("Infinity")) return true;
				if (!negative && Word("NaN")) return true;

				// Integer part: 0, or a digit 1-9 followed by any digits.
				if (AtEnd) return false;
				char first = _text[_position];
				if (first == '0')
				{
					_position++;
				}
				else if (first >= '1' && first <= '9')
				{
					_position++;
					SkipDigits();
				}
				else
				{
					return false;
				}

				if (Next('.') && SkipDigits() == 0) return false;

				if (Next('e') || Next('E'))
				{
					if (!Next('+')) Next('-');
					if (SkipDigits() == 0) return false;
				}

				return true;
			}

			private int SkipDigits()
			{
				int start = _position;
				while (_position < _text.Length && _text[_position] >= '0' && _text[_position] <= '9')
				{
					_position++;
				}

				return _position - start;
			}

			private bool Word(string word)
			{
				if (_position + word.Length > _text.Length ||
				    string.CompareOrdinal(_text, _position, word, 0, word.Length) != 0)
				{
					return false;
				}

				_position += word.Length;
				return true;
			}

			private bool Next(char c)
			{
				if (AtEnd || _text[_position] != c) return false;

				_position++;
				return true;
			}

			/// <summary>Compares a member name's raw content with the member sought, decoding escapes as it goes.</summary>
			private bool NameIsMember(int start, int end, bool escaped)
			{
				if (!escaped)
				{
					return end - start == _member.Length &&
					       string.CompareOrdinal(_text, start, _member, 0, _member.Length) == 0;
				}

				int matched = 0;
				for (int i = start; i < end; i++)
				{
					char c = _text[i];
					if (c == '\\')
					{
						char escape = _text[++i];
						switch (escape)
						{
							case 'b': c = '\b'; break;
							case 'f': c = '\f'; break;
							case 'n': c = '\n'; break;
							case 'r': c = '\r'; break;
							case 't': c = '\t'; break;
							case 'u':
								c = (char)((HexValue(_text[i + 1]) << 12) | (HexValue(_text[i + 2]) << 8) |
								           (HexValue(_text[i + 3]) << 4)  |  HexValue(_text[i + 4]));
								i += 4;
								break;
							default:
								c = escape;
								break;
						}
					}

					if (matched == _member.Length || _member[matched] != c) return false;
					matched++;
				}

				return matched == _member.Length;
			}

			private static int HexValue(char c)
			{
				if (c >= '0' && c <= '9') return c - '0';
				if (c >= 'a' && c <= 'f') return c - 'a' + 10;
				if (c >= 'A' && c <= 'F') return c - 'A' + 10;
				return -1;
			}
		}
	}
}
