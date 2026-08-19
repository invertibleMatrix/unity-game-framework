namespace Utilities.ModelPreview
{
	/// <summary>
	/// App-lifetime factory for dialog-scoped live 3D model preview sessions.
	/// </summary>
	public interface IModelPreviewService
	{
		ModelPreviewSession CreateSession(ModelPreviewSessionOptions options = null);
	}
}
