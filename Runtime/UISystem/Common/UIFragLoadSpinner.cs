using System;
using AK.Core.Extensions;
using Cysharp.Threading.Tasks;

namespace AK.Systems
{
	public class UIFragLoadSpinner : UIView
	{
		/// <summary>Closes the spinner after <paramref name="seconds"/> of the UI's time, unless it is destroyed first.</summary>
		public void AutoCloseAfterSeconds(int seconds)
		{
			if (seconds > 0)
			{
				Action().Forget();
			}

			async UniTask Action()
			{
				try
				{
					await TimeDomain.Delay(seconds, gameObject.GetCancellationTokenOnDestroy());
					Close();
				}
				catch (OperationCanceledException) { }
			}
		}
	}
}