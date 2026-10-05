#if UGFW_FIREBASE_ANALYTICS
using System;
using AK.Services.Analytics.Providers;

namespace AK.Services
{
	/// <summary>Adds Firebase Analytics to an <see cref="AnalyticsServiceBuilder"/>.</summary>
	public static class FirebaseAnalyticsBuilderExtensions
	{
		/// <summary>
		/// Adds a <see cref="FirebaseAnalyticsProvider"/>, which sends events once
		/// <paramref name="firebase"/> reports Firebase available.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="firebase"/> is null.</exception>
		public static AnalyticsServiceBuilder UseFirebase(this AnalyticsServiceBuilder builder, IFirebaseInitializationService firebase)
		{
			if (builder == null)
			{
				throw new ArgumentNullException(nameof(builder));
			}

			return builder.AddProvider(new FirebaseAnalyticsProvider(firebase));
		}
	}
}
#endif
