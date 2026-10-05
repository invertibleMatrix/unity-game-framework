using System;
using AK.Kernel.Timing;
using DG.Tweening;

namespace AK.Core.Extensions
{
	public static class TweenExt
	{
		/// <summary>
		/// Updates <paramref name="tween"/> every frame on <paramref name="domain"/>'s time:
		/// <see cref="UpdateType.Normal"/>, independent of the time scale for
		/// <see cref="TimeDomain.Unscaled"/>. Set it on the root tween; a tween nested in a sequence
		/// follows its sequence. Never leave it to DOTween's default, which a project's DOTween
		/// settings can change.
		/// DOTween's unscaled clock skips the time the app spends paused but caps no frame: a long
		/// frame moves an unscaled tween on by all of it, where the other unscaled clocks count at
		/// most <see cref="ForegroundTime.MaxFrameSeconds"/>.
		/// </summary>
		public static T SetTimeDomain<T>(this T tween, TimeDomain domain) where T : Tween
		{
			return tween.SetUpdate(UpdateType.Normal, domain == TimeDomain.Unscaled);
		}

		/// <summary>
		/// Tweens from <paramref name="current"/> to <paramref name="to"/> over
		/// <paramref name="duration"/> seconds of <paramref name="time"/>, passing each value to
		/// <paramref name="onTweenUpdate"/>.
		/// </summary>
		public static Tween GoTo(this int current, int to, float duration, TimeDomain time, Action<int> onTweenUpdate)
		{
			var tween = DOTween.To(() => current, x => current = x, to, duration).SetTimeDomain(time).Play();
			tween.OnUpdate(() => onTweenUpdate.SafeInvoke(current));

			return tween;
		}

		/// <inheritdoc cref="GoTo(int, int, float, TimeDomain, Action{int})"/>
		public static Tween GoTo(this float current, float to, float duration, TimeDomain time, Action<float> onTweenUpdate)
		{
			var tween = DOTween.To(() => current, x => current = x, to, duration).SetTimeDomain(time).Play();
			tween.OnUpdate(() => onTweenUpdate.SafeInvoke(current));

			return tween;
		}

		/// <inheritdoc cref="GoTo(int, int, float, TimeDomain, Action{int})"/>
		public static Tween GoTo(this double current, double to, float duration, TimeDomain time, Action<double> onTweenUpdate)
		{
			var tween = DOTween.To(() => current, x => current = x, to, duration).SetTimeDomain(time).Play();
			tween.OnUpdate(() => onTweenUpdate.SafeInvoke(current));

			return tween;
		}
	}
}
