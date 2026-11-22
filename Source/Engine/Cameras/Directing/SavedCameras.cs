using System.Numerics;

namespace FlickerVoid.Engine.Cameras.Directing;

/// <summary>What saves keep of the director, by shot names: the live shot, the stack, where each shot is, and the fade.</summary>
public sealed class SavedCameras
{
    public string Live { get; set; } = string.Empty;

    public List<string> Stack { get; set; } = [];

    public List<SavedShot> Shots { get; set; } = [];

    public float Fade { get; set; }

    public Vector3 FadeColor { get; set; }
}
