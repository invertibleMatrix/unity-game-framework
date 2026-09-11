using System;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests.Support
{
	/// <summary>
	/// Scopes a deliberate diagnostic so it is asserted but never reaches the Console.
	/// <c>LogAssert.Expect</c> satisfies the test runner yet still prints the entry, which then
	/// sits in the Console looking like a real project error until someone clears it.
	/// The handler is process-wide, so entries logged from worker threads are captured too.
	/// </summary>
	public sealed class ExpectedLog : IDisposable, ILogHandler
	{
		private readonly ILogHandler _previous;
		private readonly LogType     _type;
		private readonly Regex       _pattern;
		private readonly int         _expected;
		private int                  _matches;

		private ExpectedLog(LogType type, Regex pattern, int expected)
		{
			_type     = type;
			_pattern  = pattern;
			_expected = expected;
			_previous = Debug.unityLogger.logHandler;

			Debug.unityLogger.logHandler = this;
		}

		public static ExpectedLog Error(string pattern, int expected = 1) => new(LogType.Error, new Regex(pattern), expected);
		public static ExpectedLog Warning(string pattern, int expected = 1) => new(LogType.Warning, new Regex(pattern), expected);

		/// <summary>Captures <c>Debug.LogException</c> whose message matches.</summary>
		public static ExpectedLog Exception(string pattern, int expected = 1) => new(LogType.Exception, new Regex(pattern), expected);

		void ILogHandler.LogFormat(LogType logType, Object context, string format, params object[] args)
		{
			string message = args == null || args.Length == 0 ? format : string.Format(format, args);

			if (logType == _type && _pattern.IsMatch(message))
			{
				Interlocked.Increment(ref _matches);
				return;
			}

			_previous.LogFormat(logType, context, format, args);
		}

		void ILogHandler.LogException(Exception exception, Object context)
		{
			if (_type == LogType.Exception && exception != null && _pattern.IsMatch(exception.Message))
			{
				Interlocked.Increment(ref _matches);
				return;
			}

			_previous.LogException(exception, context);
		}

		public void Dispose()
		{
			Debug.unityLogger.logHandler = _previous;
			Assert.AreEqual(_expected, _matches, $"expected exactly {_expected} {_type} matching /{_pattern}/, saw {_matches}");
		}
	}
}
