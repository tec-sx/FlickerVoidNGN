namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>The camera layers a shot uses (data asset type <c>cameraRig</c>), shared by every shot that names the file.</summary>
public sealed class CameraRig
{
    public List<CameraLayer> Layers { get; set; } = [];
}
