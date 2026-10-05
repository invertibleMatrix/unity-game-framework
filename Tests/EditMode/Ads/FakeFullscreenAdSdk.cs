using System;
using System.Collections.Generic;
using AK.CoreDomain.Ads;
using AK.Services.Ads;

namespace AK.Tests.Ads
{
	/// <summary>
	/// A fullscreen ad SDK for <see cref="FullscreenAdDriver"/> tests. It records the calls it
	/// gets, and answers only through the hooks the test sets. A show uses its ad up.
	/// </summary>
	internal sealed class FakeFullscreenAdSdk : IFullscreenAdSdk
	{
		/// <summary>Units with an ad loaded.</summary>
		public readonly HashSet<string> Loaded = new();

		/// <summary>The units asked to load, in order.</summary>
		public readonly List<string> Loads = new();

		/// <summary>The units asked to show, in order.</summary>
		public readonly List<string> Shows = new();

		/// <summary>Runs inside <see cref="Load"/>, for an SDK that answers before returning.</summary>
		public Action<AdType, string> OnLoad;

		/// <summary>Runs inside <see cref="Show"/>, for an SDK that answers before returning.</summary>
		public Action<AdType, string> OnShow;

		public bool IsLoaded(AdType adType, string adUnitId) => Loaded.Contains(adUnitId);

		public void Load(AdType adType, string adUnitId)
		{
			Loads.Add(adUnitId);
			OnLoad?.Invoke(adType, adUnitId);
		}

		public void Show(AdType adType, string adUnitId, string placementId)
		{
			Shows.Add(adUnitId);
			Loaded.Remove(adUnitId);
			OnShow?.Invoke(adType, adUnitId);
		}
	}
}
