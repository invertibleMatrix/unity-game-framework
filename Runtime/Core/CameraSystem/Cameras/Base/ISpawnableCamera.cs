namespace AK.Systems
{
	/// <summary>
	/// A camera the <see cref="ICameraSystem"/> can spawn from a <see cref="CameraDefinition"/>:
	/// it takes its configuration from the definition, and binds to the system that spawned it
	/// before the spawn returns. A spawned camera that isn't one is bound as it comes.
	/// </summary>
	public interface ISpawnableCamera : IGameCamera
	{
		/// <summary>Takes the definition's configuration in place of the prefab's.</summary>
		void ApplyDefinition(CameraDefinition definition);

		/// <summary>Binds to <paramref name="cameraSystem"/>. Nothing happens once bound.</summary>
		void BindToSystem(ICameraSystem cameraSystem);
	}
}
