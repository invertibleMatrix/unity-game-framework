using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests.Support
{
	/// <summary>
	/// Scopes a deliberate diagnostic so it is asserted but never reaches the Console.
	/// <c>LogAssert.Expect</c> satisfies the test runner yet still prints the entry, which then
	/// sits in the Console looking like a real project error until someone clears it.
	/// </summary>
	public sealed class ExpectedLog : IDisposable, ILogHandler
	{
		private readonly ILogHandler _previous;
		private readonly LogType     _type;
		private readonly Regex       _pattern;
		private int                  _matches;

		private ExpectedLog(LogType type, Regex pattern)
		{
			_type     = type;
			_pattern  = pattern;
			_previous = Debug.unityLogger.logHandler;

			Debug.unityLogger.logHandler = this;
		}

		public static ExpectedLog Error(string pattern) => new(LogType.Error, new Regex(pattern));
		public static ExpectedLog Warning(string pattern) => new(LogType.Warning, new Regex(pattern));

		void ILogHandler.LogFormat(LogType logType, Object context, string format, params object[] args)
		{
			string message = args == null || args.Length == 0 ? format : string.Format(format, args);

			if (logType == _type && _pattern.IsMatch(message))
			{
				_matches++;
				return;
			}

			_previous.LogFormat(logType, context, format, args);
		}

		void ILogHandler.LogException(Exception exception, Object context) => _previous.LogException(exception, context);

		public void Dispose()
		{
			Debug.unityLogger.logHandler = _previous;
			Assert.AreEqual(1, _matches, $"expected exactly one {_type} matching /{_pattern}/, saw {_matches}");
		}
	}
}
