using System;
using DG.Tweening;

namespace AK.Core.Extensions
{
	public static class TweenExt
	{
		/// <summary>
		/// Tween This Value To The Given Value & Give Updates With Values..
		/// </summary>
		/// <returns></returns>
		public static Tween GoTo(this int current, int to, float duration, Action<int> onTweenUpdate)
		{
			var tween = DOTween.To(() => current, x => current = x, to, duration).Play();
			tween.OnUpdate(() => onTweenUpdate.SafeInvoke(current));

			return tween;
		}

		/// <summary>
		/// Tween This Value To The Given Value & Give Updates With Values..
		/// </summary>
		/// <returns></returns>
		public static Tween GoTo(this float current, float to, float duration, Action<float> onTweenUpdate)
		{
			var tween = DOTween.To(() => current, x => current = x, to, duration).Play();
			tween.OnUpdate(() => onTweenUpdate.SafeInvoke(current));

			return tween;
		}

		/// <summary>
		/// Tween This Value To The Given Value & Give Updates With Values..
		/// </summary>
		/// <returns></returns>
		public static Tween GoTo(this double current, double to, float duration, Action<double> onTweenUpdate)
		{
			var tween = DOTween.To(() => current, x => current = x, to, duration).Play();
			tween.OnUpdate(() => onTweenUpdate.SafeInvoke(current));

			return tween;
		}
	}
}