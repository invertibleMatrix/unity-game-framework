using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests.Support
{
	/// <summary>
	/// Keeps the info and warning logs of code under test out of the Console, and records them
	/// for the test to assert on. Errors and exceptions pass through, so the test runner still
	/// fails on an unexpected one; scope a deliberate one with <see cref="ExpectedLog"/>.
	/// The handler is process-wide, so entries logged from worker threads are recorded too.
	/// </summary>
	public sealed class LogRecorder : IDisposable, ILogHandler
	{
		private readonly ILogHandler _previous;
		private readonly List<(LogType Type, string Message)> _entries = new();

		public LogRecorder()
		{
			_previous = Debug.unityLogger.logHandler;
			Debug.unityLogger.logHandler = this;
		}

		/// <summary>How many entries of <paramref name="type"/> match <paramref name="pattern"/>.</summary>
		public int Count(LogType type, string pattern)
		{
			var regex = new Regex(pattern);
			int count = 0;

			lock (_entries)
			{
				foreach ((LogType entryType, string message) in _entries)
				{
					if (entryType == type && regex.IsMatch(message))
					{
						count++;
					}
				}
			}

			return count;
		}

		/// <summary>Forgets what was recorded so far.</summary>
		public void Clear()
		{
			lock (_entries)
			{
				_entries.Clear();
			}
		}

		void ILogHandler.LogFormat(LogType logType, Object context, string format, params object[] args)
		{
			if (logType is LogType.Log or LogType.Warning)
			{
				string message = args == null || args.Length == 0 ? format : string.Format(format, args);
				lock (_entries)
				{
					_entries.Add((logType, message));
				}

				return;
			}

			_previous.LogFormat(logType, context, format, args);
		}

		void ILogHandler.LogException(Exception exception, Object context) => _previous.LogException(exception, context);

		public void Dispose()
		{
			Debug.unityLogger.logHandler = _previous;
		}
	}
}
