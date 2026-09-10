using System.Diagnostics;
using Unity.Profiling;

namespace AK.Core
{
	/// <summary>
	/// Marks a region in the Unity Profiler for the lifetime of a scope, on every exit path.
	/// Zero cost in release: the whole type body is stripped by <c>[Conditional]</c>, the
	/// <c>using</c> compiles to an empty dispose, and no marker is created.
	///
	/// <code>
	/// using (ProfilerScope.Begin(Markers.ShopRefresh))
	/// {
	///     RebuildShopList();
	/// }
	/// </code>
	///
	/// Declare markers once as <c>static readonly ProfilerMarker</c> fields — creating one per
	/// call allocates the name string in the profiler and defeats the point. The overload that
	/// takes a <c>string</c> exists for quick investigation only.
	/// </summary>
	public readonly ref struct ProfilerScope
	{
		private readonly ProfilerMarker _marker;

		private ProfilerScope(ProfilerMarker marker)
		{
			_marker = marker;
			BeginSample(_marker);
		}

		public static ProfilerScope Begin(ProfilerMarker marker) => new(marker);

		/// <summary>Ad-hoc marker by name. Fine while investigating; promote to a static marker before shipping.</summary>
		public static ProfilerScope Begin(string name) => new(new ProfilerMarker(name));

		public void Dispose()
		{
			EndSample(_marker);
		}

		[Conditional("ENABLE_PROFILER")]
		private static void BeginSample(ProfilerMarker marker) => marker.Begin();

		[Conditional("ENABLE_PROFILER")]
		private static void EndSample(ProfilerMarker marker) => marker.End();
	}
}
