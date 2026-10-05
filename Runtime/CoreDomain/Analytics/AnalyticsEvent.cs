using System;
using System.Collections.Generic;
using System.Globalization;

namespace AK.CoreDomain.Analytics
{
	/// <summary>
	/// Runtime envelope sent through <c>IAnalyticsService</c>.
	/// Definitions may overlay mapping/sampling; they are not required to dispatch.
	/// </summary>
	public sealed class AnalyticsEvent
	{
		public string Id;
		public AnalyticsEventKind Kind = AnalyticsEventKind.Unspecified;
		public Dictionary<string, object> Parameters;
		public float? Value;

		public AnalyticsProgressionStatus? ProgressionStatus;
		public string Progression01;
		public string Progression02;
		public string Progression03;
		public int? Score;

		public AnalyticsAdAction? AdAction;
		public string AdType;
		public string AdSdkName;
		public string AdPlacement;
		public long? AdDurationMs;
		public string AdFailReason;
		public double? AdRevenue;

		public AnalyticsResourceFlow? ResourceFlow;
		public string ResourceCurrency;
		public float? ResourceAmount;
		public string ResourceItemType;
		public string ResourceItemId;

		public AnalyticsErrorSeverity? ErrorSeverity;
		public string ErrorMessage;

		public string ItemId;
		public double? Price;
		public string Currency;
		public string CartType;

		public static AnalyticsEvent Design(string id, float? value = null, Dictionary<string, object> parameters = null)
		{
			return new AnalyticsEvent
			{
				Id = id,
				Kind = AnalyticsEventKind.Design,
				Value = value,
				Parameters = parameters
			};
		}

		public static AnalyticsEvent Progression(
			AnalyticsProgressionStatus status,
			string progression01,
			string progression02 = null,
			string progression03 = null,
			int? score = null,
			Dictionary<string, object> parameters = null)
		{
			return new AnalyticsEvent
			{
				Id = progression01,
				Kind = AnalyticsEventKind.Progression,
				ProgressionStatus = status,
				Progression01 = progression01,
				Progression02 = progression02,
				Progression03 = progression03,
				Score = score,
				Parameters = parameters
			};
		}

		public static AnalyticsEvent Ad(
			AnalyticsAdAction action,
			string adType,
			string placement,
			string sdkName = "applovin",
			long? durationMs = null,
			string failReason = null,
			double? revenue = null,
			Dictionary<string, object> parameters = null)
		{
			return new AnalyticsEvent
			{
				Id = "ad",
				Kind = AnalyticsEventKind.Ad,
				AdAction = action,
				AdType = adType,
				AdSdkName = sdkName,
				AdPlacement = placement,
				AdDurationMs = durationMs,
				AdFailReason = failReason,
				AdRevenue = revenue,
				Parameters = parameters
			};
		}

		public Dictionary<string, object> EnsureParameters()
		{
			return Parameters ??= new Dictionary<string, object>();
		}

		public AnalyticsEvent With(string key, object value)
		{
			EnsureParameters()[key] = value;
			return this;
		}

		/// <summary>
		/// The parameter under <paramref name="key"/> as text, or null when it is missing, null or
		/// empty. Numbers format with the invariant culture.
		/// </summary>
		public string ParameterText(string key)
		{
			if (Parameters == null || key == null || !Parameters.TryGetValue(key, out object raw) || raw == null)
			{
				return null;
			}

			string text = raw is IFormattable formattable
				? formattable.ToString(null, CultureInfo.InvariantCulture)
				: raw.ToString();
			return string.IsNullOrEmpty(text) ? null : text;
		}

		public AnalyticsEvent Clone()
		{
			return new AnalyticsEvent
			{
				Id = Id,
				Kind = Kind,
				Parameters = Parameters == null ? null : new Dictionary<string, object>(Parameters),
				Value = Value,
				ProgressionStatus = ProgressionStatus,
				Progression01 = Progression01,
				Progression02 = Progression02,
				Progression03 = Progression03,
				Score = Score,
				AdAction = AdAction,
				AdType = AdType,
				AdSdkName = AdSdkName,
				AdPlacement = AdPlacement,
				AdDurationMs = AdDurationMs,
				AdFailReason = AdFailReason,
				AdRevenue = AdRevenue,
				ResourceFlow = ResourceFlow,
				ResourceCurrency = ResourceCurrency,
				ResourceAmount = ResourceAmount,
				ResourceItemType = ResourceItemType,
				ResourceItemId = ResourceItemId,
				ErrorSeverity = ErrorSeverity,
				ErrorMessage = ErrorMessage,
				ItemId = ItemId,
				Price = Price,
				Currency = Currency,
				CartType = CartType
			};
		}
	}
}
