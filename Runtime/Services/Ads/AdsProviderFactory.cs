using System;

namespace AK.Services.Ads
{
	/// <summary>
	/// Optional SDK adapters register here from their own assemblies so
	/// <see cref="AK.Services.AdServiceBuilder"/> does not hard-reference vendor SDKs.
	/// </summary>
	public static class AdsProviderFactory
	{
		public static Func<IAdProvider> Max { get; set; }

		public static IAdProvider TryCreateMax()
		{
			return Max?.Invoke();
		}
	}
}
