using System;
using AK.Systems;
using Cysharp.Threading.Tasks;

namespace UI
{
	public class UIFragLoadSpinner : UIView
	{
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
					await UniTask.WaitForSeconds(seconds, cancellationToken: gameObject.GetCancellationTokenOnDestroy());
					Close();
				}
				catch (OperationCanceledException) { }
			}
		}
	}
}