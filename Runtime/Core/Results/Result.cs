using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AK.Core
{
	/// <summary>
	/// Outcome of an operation with no payload: either <see cref="Ok"/> or a failure code.
	/// Sixteen bytes, no heap on the happy path — <see cref="Detail"/> is null unless a
	/// failure has something to say beyond its code. Expected failures (cannot afford, not
	/// found, declined by the store) travel this way; bugs still throw.
	///
	/// Callers switch on <see cref="Code"/>. UI maps codes to strings; nothing parses
	/// <see cref="Detail"/>, which exists for logs and bug reports only.
	/// </summary>
	public readonly struct Result : IEquatable<Result>
	{
		public readonly ErrorCode Code;
		public readonly string    Detail;

		private Result(ErrorCode code, string detail)
		{
			Code   = code;
			Detail = detail;
		}

		public static readonly Result Ok = default;

		public bool IsOk     => Code == ErrorCode.None;
		public bool IsFailed => Code != ErrorCode.None;

		public static Result Fail(ErrorCode code, string detail = null)
		{
			return code == ErrorCode.None
				? throw new ArgumentException("ErrorCode.None is not a failure.", nameof(code))
				: new Result(code, detail);
		}

		/// <summary>For game-defined codes at or above <see cref="ErrorCode.GameDefined"/>.</summary>
		public static Result Fail(int gameCode, string detail = null)
		{
			return gameCode < (int)ErrorCode.GameDefined
				? throw new ArgumentOutOfRangeException(nameof(gameCode), $"Game codes start at {(int)ErrorCode.GameDefined}.")
				: new Result((ErrorCode)gameCode, detail);
		}

		/// <summary>Carries the failure into a typed result. Throws on Ok — there is no value to carry.</summary>
		public Result<T> As<T>()
		{
			return IsOk
				? throw new InvalidOperationException("Cannot convert an Ok result to a typed failure.")
				: Result<T>.Fail(Code, Detail);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Result other) => Code == other.Code;

		public override bool Equals(object obj) => obj is Result other && Equals(other);
		public override int GetHashCode() => (int)Code;

		public static bool operator ==(Result a, Result b) => a.Code == b.Code;
		public static bool operator !=(Result a, Result b) => a.Code != b.Code;

		public static implicit operator bool(Result r) => r.IsOk;

		public override string ToString() => IsOk ? "Ok" : string.IsNullOrEmpty(Detail) ? $"Fail({Code})" : $"Fail({Code}: {Detail})";
	}

	/// <summary>
	/// Outcome of an operation with a payload. <see cref="Value"/> is meaningful only when
	/// <see cref="IsOk"/>; reading it on a failure returns <c>default</c> rather than throwing,
	/// so callers that already checked the code pay nothing for the check twice.
	/// </summary>
	public readonly struct Result<T> : IEquatable<Result<T>>
	{
		public readonly T         Value;
		public readonly ErrorCode Code;
		public readonly string    Detail;

		private Result(T value, ErrorCode code, string detail)
		{
			Value  = value;
			Code   = code;
			Detail = detail;
		}

		public bool IsOk     => Code == ErrorCode.None;
		public bool IsFailed => Code != ErrorCode.None;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Result<T> Ok(T value) => new(value, ErrorCode.None, null);

		public static Result<T> Fail(ErrorCode code, string detail = null)
		{
			return code == ErrorCode.None
				? throw new ArgumentException("ErrorCode.None is not a failure.", nameof(code))
				: new Result<T>(default, code, detail);
		}

		public static Result<T> Fail(int gameCode, string detail = null)
		{
			return gameCode < (int)ErrorCode.GameDefined
				? throw new ArgumentOutOfRangeException(nameof(gameCode), $"Game codes start at {(int)ErrorCode.GameDefined}.")
				: new Result<T>(default, (ErrorCode)gameCode, detail);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGet(out T value)
		{
			value = Value;
			return IsOk;
		}

		/// <summary>The value, or <paramref name="fallback"/> on failure.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Or(T fallback) => IsOk ? Value : fallback;

		/// <summary>Drops the payload, keeps the outcome.</summary>
		public Result Untyped => IsOk ? Result.Ok : Result.Fail(Code, Detail);

		public bool Equals(Result<T> other) => Code == other.Code && (IsFailed || EqualityComparer<T>.Default.Equals(Value, other.Value));

		public override bool Equals(object obj) => obj is Result<T> other && Equals(other);

		public override int GetHashCode() => IsOk ? EqualityComparer<T>.Default.GetHashCode(Value) : (int)Code;

		public static implicit operator bool(Result<T> r) => r.IsOk;

		/// <summary>Lets a method returning <c>Result&lt;T&gt;</c> write <c>return value;</c> for the success path.</summary>
		public static implicit operator Result<T>(T value) => Ok(value);

		/// <summary>Lets a method returning <c>Result&lt;T&gt;</c> write <c>return Result.Fail(code);</c>.</summary>
		public static implicit operator Result<T>(Result untyped) => untyped.As<T>();

		public override string ToString() => IsOk ? $"Ok({Value})" : string.IsNullOrEmpty(Detail) ? $"Fail({Code})" : $"Fail({Code}: {Detail})";
	}
}
