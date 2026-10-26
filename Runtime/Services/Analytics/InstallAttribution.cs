using System;
using UnityEngine;

namespace AK.Services.Analytics
{
	/// <summary>
	/// Where the install came from, as the attribution provider reported it.
	/// Empty strings for what it didn't say; organic installs have no campaign.
	/// </summary>
	public readonly struct InstallSource
	{
		public readonly bool Organic;
		public readonly string MediaSource;
		public readonly string Campaign;
		public readonly string AdSet;
		public readonly string Ad;

		public InstallSource(bool organic, string mediaSource, string campaign, string adSet, string ad)
		{
			Organic = organic;
			MediaSource = mediaSource ?? string.Empty;
			Campaign = campaign ?? string.Empty;
			AdSet = adSet ?? string.Empty;
			Ad = ad ?? string.Empty;
		}
	}

	/// <summary>
	/// Shared install attribution. The attribution provider (AppsFlyer's conversion data)
	/// reports here so a game can put the campaign on its analytics without referencing the
	/// provider's assembly. Providers report on every launch; the latest report wins.
	/// </summary>
	public static class InstallAttribution
	{
		/// <summary>Null until the provider reports this session.</summary>
		public static InstallSource? Latest { get; private set; }

		public static event Action<InstallSource> Received;

		public static void Report(InstallSource source)
		{
			Latest = source;
			Received?.Invoke(source);
		}

		// Statics outlive Play sessions when domain reload is off.
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetForPlaySession()
		{
			Latest = null;
			Received = null;
		}
	}
}
