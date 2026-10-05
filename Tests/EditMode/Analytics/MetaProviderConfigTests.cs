using System.Collections.Generic;
using AK.Services.Analytics;
using NUnit.Framework;

namespace AK.Tests.Analytics
{
	public class MetaProviderConfigTests
	{
		[Test]
		public void IsInstallsOnly_TrueOnlyForTrueValue()
		{
			Assert.IsTrue(MetaProviderConfig.IsInstallsOnly(new Dictionary<string, string> { { MetaProviderConfig.InstallsOnlyKey, "true" } }));
			Assert.IsTrue(MetaProviderConfig.IsInstallsOnly(new Dictionary<string, string> { { MetaProviderConfig.InstallsOnlyKey, "True" } }));
			Assert.IsFalse(MetaProviderConfig.IsInstallsOnly(new Dictionary<string, string> { { MetaProviderConfig.InstallsOnlyKey, "false" } }));
			Assert.IsFalse(MetaProviderConfig.IsInstallsOnly(new Dictionary<string, string> { { MetaProviderConfig.InstallsOnlyKey, "yes" } }));
			Assert.IsFalse(MetaProviderConfig.IsInstallsOnly(new Dictionary<string, string> { { "userId", "abc" } }));
			Assert.IsFalse(MetaProviderConfig.IsInstallsOnly(null));
		}
	}
}
