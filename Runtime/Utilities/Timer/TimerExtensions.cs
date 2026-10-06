using System;
using System.Threading;
using AK.Kernel.Formatting;
using AK.Kernel.Timing;
using UnityEngine.UI;
#if UGFW_TMP
using TMPro;
#endif

namespace AK.Utilities
{
	/// <summary>Formats for <see cref="TimeSpan"/>s, timer factories, and bindings from a <see cref="Timer"/> to UI.</summary>
	public static class TimerExtensions
	{
		private static readonly Func<TimeSpan, string> MinutesAndSeconds = time => time.ToMMSS();

		// ------------------------------------------------------------------ formatting

		/// <summary>
		/// Total minutes and seconds, "65:30", rounded down by default. A time below zero gets a
		/// minus sign, "-01:30", unless it shows as zero; it is rounded by its size.
		/// </summary>
		public static string ToMMSS(this TimeSpan time, TimeRounding rounding = TimeRounding.Floor) =>
			FormatClock(time, ClockLayout.TotalMinutes, rounding);

		/// <summary>
		/// Total hours, minutes and seconds, "25:05:30", rounded down by default. A countdown passes
		/// <see cref="TimeRounding.Ceil"/>, so it shows zero only once it is over. A time below
		/// zero gets a minus sign unless it shows as zero; it is rounded by its size.
		/// </summary>
		public static string ToHHMMSS(this TimeSpan time, TimeRounding rounding = TimeRounding.Floor) =>
			FormatClock(time, ClockLayout.TotalHours, rounding);

		/// <summary>The two largest units that aren't zero, rounded down: "1d 1h", "2h 30m", "45s". A time below zero gets a minus sign.</summary>
		public static string ToCompactFormat(this TimeSpan time)
		{
			Span<char> text = stackalloc char[DurationText.MaxLength + 1];
			int sign = WriteSign(time, text, out TimeSpan size);
			DurationText.TryFormatUnits(size, UnitStyle.Abbreviated, 2, NumberRounding.Down, text.Slice(sign), out int written);
			return Signed(text, sign, written);
		}

		/// <summary>Days before the clock from a day on, rounded down: "2d 05:30:00", or "05:30:00" below a day. A time below zero gets a minus sign.</summary>
		public static string ToDurationFormat(this TimeSpan time) =>
			FormatClock(time, ClockLayout.DaysAndClock, TimeRounding.Floor);

		private static string FormatClock(TimeSpan time, ClockLayout layout, TimeRounding rounding)
		{
			Span<char> text = stackalloc char[DurationText.MaxLength + 1];
			int sign = WriteSign(time, text, out TimeSpan size);
			DurationText.TryFormatClock(size, layout, TimeFormatter.ToNumberRounding(rounding), text.Slice(sign), out int written);
			return Signed(text, sign, written);
		}

		/// <summary>Writes a minus sign for a time below zero, and gives its size; that of <see cref="TimeSpan.MinValue"/> is taken as <see cref="TimeSpan.MaxValue"/>.</summary>
		private static int WriteSign(TimeSpan time, Span<char> text, out TimeSpan size)
		{
			if (time.Ticks >= 0)
			{
				size = time;
				return 0;
			}

			size = time == TimeSpan.MinValue ? TimeSpan.MaxValue : time.Negate();
			text[0] = '-';
			return 1;
		}

		/// <summary>The text with its sign, which is dropped when every digit is zero.</summary>
		private static string Signed(Span<char> text, int sign, int written)
		{
			ReadOnlySpan<char> size = text.Slice(sign, written);
			return sign == 1 && size.IndexOfAny("123456789".AsSpan()) >= 0
				? new string(text.Slice(0, sign + written))
				: new string(size);
		}

		// ------------------------------------------------------------------ factories

		/// <summary>
		/// A wall-clock countdown to the next reset of a schedule that repeats every
		/// <paramref name="duration"/> from <paramref name="targetTime"/>: the first
		/// targetTime + k × duration after now, for any whole k. For a reward that resets daily at
		/// midnight UTC, pass a midnight UTC of any day and one day. A local time is converted to
		/// UTC; a time of unspecified kind is taken as UTC.
		/// </summary>
		/// <param name="onTick">The run's tick callback, which sees the first tick, raised as the timer starts.</param>
		/// <param name="onComplete">The run's completion callback.</param>
		/// <param name="tickInterval">How often to tick; <see cref="Timer.DefaultTickInterval"/> when null.</param>
		/// <param name="cancellationToken">The owner's lifetime, such as <c>GetCancellationTokenOnDestroy()</c>.</param>
		/// <param name="clock">The clocks to read; Unity's when null.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> or <paramref name="tickInterval"/> isn't above zero.</exception>
		public static Timer CreateDailyResetTimer(this DateTime targetTime, TimeSpan duration, Action<TimeSpan, float> onTick = null,
		                                          Action onComplete = null, TimeSpan? tickInterval = null,
		                                          CancellationToken cancellationToken = default, ITimerClock clock = null)
		{
			if (duration.Ticks <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(duration), duration, "A reset repeats after a duration above zero.");
			}

			clock ??= UnityTimerClock.Instance;
			DateTime target = targetTime.Kind == DateTimeKind.Local ? targetTime.ToUniversalTime() : targetTime;

			// From now to the next reset: in (0, duration], whichever side of now the target is.
			long untilReset = (target.Ticks - clock.UtcNow.Ticks) % duration.Ticks;
			if (untilReset <= 0) untilReset += duration.Ticks;

			var timer = new Timer(clock, cancellationToken);
			timer.StartCountdown(new TimeSpan(untilReset), onTick, onComplete, tickInterval, TimeBase.Wall);
			return timer;
		}

		/// <summary>
		/// A wall-clock countdown of what is left of a cooldown: <paramref name="totalCooldown"/>
		/// less <paramref name="elapsed"/>. A cooldown that is over ticks and completes before this
		/// returns, so pass the callbacks here rather than subscribing afterwards.
		/// </summary>
		/// <param name="onTick">The run's tick callback, which sees the first tick, raised as the timer starts.</param>
		/// <param name="onComplete">The run's completion callback.</param>
		/// <param name="tickInterval">How often to tick; <see cref="Timer.DefaultTickInterval"/> when null.</param>
		/// <param name="cancellationToken">The owner's lifetime, such as <c>GetCancellationTokenOnDestroy()</c>.</param>
		/// <param name="clock">The clocks to read; Unity's when null.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="tickInterval"/> isn't above zero.</exception>
		public static Timer CreateCooldownTimer(this TimeSpan totalCooldown, TimeSpan elapsed, Action<TimeSpan, float> onTick = null,
		                                        Action onComplete = null, TimeSpan? tickInterval = null,
		                                        CancellationToken cancellationToken = default, ITimerClock clock = null)
		{
			var timer = new Timer(clock ?? UnityTimerClock.Instance, cancellationToken);
			timer.StartCountdown(totalCooldown - elapsed, onTick, onComplete, tickInterval, TimeBase.Wall);
			return timer;
		}

		// ------------------------------------------------------------------ bindings

		/// <summary>
		/// Shows the timer's time on <paramref name="text"/> now and on every tick, formatted by
		/// <paramref name="format"/>, or as <see cref="ToMMSS"/> when null. Dispose the result to stop.
		/// </summary>
		public static IDisposable BindToText(this Timer timer, Text text, Func<TimeSpan, string> format = null)
		{
			if (text == null) throw new ArgumentNullException(nameof(text));

			format ??= MinutesAndSeconds;
			return Bind(timer, (time, _) =>
			{
				if (text != null) text.text = format(time);
			});
		}

#if UGFW_TMP
		/// <inheritdoc cref="BindToText(Timer, Text, Func{TimeSpan, string})"/>
		public static IDisposable BindToText(this Timer timer, TMP_Text text, Func<TimeSpan, string> format = null)
		{
			if (text == null) throw new ArgumentNullException(nameof(text));

			format ??= MinutesAndSeconds;
			return Bind(timer, (time, _) =>
			{
				if (text != null) text.text = format(time);
			});
		}
#endif

		/// <summary>Fills <paramref name="image"/> with the timer's progress, from 0 to 1, now and on every tick. Dispose the result to stop.</summary>
		public static IDisposable BindToFill(this Timer timer, Image image)
		{
			if (image == null) throw new ArgumentNullException(nameof(image));

			return Bind(timer, (_, progress) =>
			{
				if (image != null) image.fillAmount = progress;
			});
		}

		/// <summary>Fills <paramref name="image"/> with what is left, from 1 to 0, now and on every tick: for cooldown overlays that empty. Dispose the result to stop.</summary>
		public static IDisposable BindToFillInverse(this Timer timer, Image image)
		{
			if (image == null) throw new ArgumentNullException(nameof(image));

			return Bind(timer, (_, progress) =>
			{
				if (image != null) image.fillAmount = 1f - progress;
			});
		}

		/// <summary>Moves <paramref name="slider"/> across its range with the timer's progress, now and on every tick. Dispose the result to stop.</summary>
		public static IDisposable BindToSlider(this Timer timer, Slider slider)
		{
			if (slider == null) throw new ArgumentNullException(nameof(slider));

			return Bind(timer, (_, progress) =>
			{
				if (slider != null) slider.normalizedValue = progress;
			});
		}

		private static IDisposable Bind(Timer timer, Action<TimeSpan, float> show)
		{
			if (timer == null) throw new ArgumentNullException(nameof(timer));

			timer.OnTick += show;
			show(timer.TickValue, timer.Progress);
			return new Binding(timer, show);
		}

		private sealed class Binding : IDisposable
		{
			private Timer _timer;
			private Action<TimeSpan, float> _show;

			public Binding(Timer timer, Action<TimeSpan, float> show)
			{
				_timer = timer;
				_show  = show;
			}

			public void Dispose()
			{
				if (_timer == null) return;

				_timer.OnTick -= _show;
				_timer = null;
				_show  = null;
			}
		}
	}
}
