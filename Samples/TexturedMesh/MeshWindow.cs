using FlickerVoid.Engine.Debug;

using Hexa.NET.ImGui;

namespace FlickerVoid.Samples.TexturedMesh;

/// <summary>The cube sample's debug panel: how fast the cube turns, or whether it stands still.</summary>
public sealed class MeshWindow : DebugWindow
{
    private float _speed = 1;
    private bool _paused;

    public MeshWindow()
        : base("Cube", "TexturedMesh")
    {
    }

    public float Speed
    {
        get
        {
            if (_paused)
            {
                return 0;
            }

            return _speed;
        }
    }

    public override void Render()
    {
        ImGui.Checkbox("Paused", ref _paused);
        ImGui.SliderFloat("Turning speed", ref _speed, 0, 5);
    }
}
