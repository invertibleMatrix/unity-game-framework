using System;
using System.Globalization;

namespace AK.Tests.Support
{
	/// <summary>
	/// Runs a block under another culture, as on a device set to it, and restores the previous
	/// one on dispose. Thai (th-TH) uses the Buddhist calendar and Saudi Arabic (ar-SA) the Umm
	/// al-Qura one, so code that formats or parses with the current culture shows up wrong there.
	/// </summary>
	public sealed class CultureScope : IDisposable
	{
		private readonly CultureInfo _previous;
		private readonly CultureInfo _previousUi;

		public CultureScope(string name)
		{
			_previous   = CultureInfo.CurrentCulture;
			_previousUi = CultureInfo.CurrentUICulture;

			var culture = new CultureInfo(name);
			CultureInfo.CurrentCulture   = culture;
			CultureInfo.CurrentUICulture = culture;
		}

		public void Dispose()
		{
			CultureInfo.CurrentCulture   = _previous;
			CultureInfo.CurrentUICulture = _previousUi;
		}
	}
}
