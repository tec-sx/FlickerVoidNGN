using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Debug;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime.Components;

using Hexa.NET.ImGui;

namespace FlickerVoid.Samples.SceneViewer;

/// <summary>The scene viewer's own debug panel: the camera projection, the walkers' speed, the story's mood and a ring of lamps to add.</summary>
public sealed class ViewerWindow : DebugWindow
{
    private const int MaxLamps = 64;

    private static readonly Vector3[] Palette = [new(1, 0.3f, 0.2f), new(1, 0.8f, 0.3f), new(0.3f, 1, 0.4f), new(0.3f, 0.8f, 1), new(0.4f, 0.4f, 1), new(1, 0.4f, 0.9f)];

    private readonly World _world;
    private readonly Entity _camera;
    private readonly CircleWalkSystem _walkers;
    private readonly IFactDatabase _facts;
    private readonly string[] _moods;
    private readonly List<Entity> _lamps = [];
    private int _lampCount;
    private float _lampRadius = 3;
    private float _lampIntensity = 3;
    private float _lampRange = 3;
    private bool _lampShadows = true;

    public ViewerWindow(World world, Entity camera, CircleWalkSystem walkers, IFactDatabase facts, string[] moods)
        : base("Viewer", "SceneViewer")
    {
        _world = world;
        _camera = camera;
        _walkers = walkers;
        _facts = facts;
        _moods = moods;
    }

    public override void Render()
    {
        ref Camera camera = ref _world.Get<Camera>(_camera);
        bool perspective = camera.Projection == CameraProjection.Perspective;
        if (ImGui.Checkbox("Perspective camera (P)", ref perspective))
        {
            camera.Projection = perspective ? CameraProjection.Perspective : CameraProjection.Orthographic;
        }

        float speed = _walkers.Speed;
        if (ImGui.SliderFloat("Walking speed", ref speed, 0, 3))
        {
            _walkers.Speed = speed;
        }

        if (_moods.Length > 0)
        {
            DrawMoods();
        }

        ImGui.SeparatorText("Ring of lamps");
        bool changed = ImGui.SliderInt("Lamps", ref _lampCount, 0, MaxLamps);
        changed |= ImGui.SliderFloat("Ring radius", ref _lampRadius, 0.5f, 6);
        changed |= ImGui.SliderFloat("Lamp intensity", ref _lampIntensity, 0, 10);
        changed |= ImGui.SliderFloat("Lamp range", ref _lampRange, 0.5f, 8);
        changed |= ImGui.Checkbox("Lamps cast shadows (the first eight fit)", ref _lampShadows);
        if (changed)
        {
            Rebuild();
        }
    }

    private void DrawMoods()
    {
        ImGui.SeparatorText("Mood");
        int current = _facts.GetFact(MoodSystem.MoodFact);
        foreach (string mood in _moods)
        {
            int index = Array.IndexOf(_moods, mood);
            if (ImGui.RadioButton(Path.GetFileNameWithoutExtension(mood), current == index))
            {
                _facts.SetFact(MoodSystem.MoodFact, index);
            }
        }
    }

    private void Rebuild()
    {
        foreach (Entity lamp in _lamps)
        {
            _world.Destroy(lamp);
        }

        _lamps.Clear();
        for (int index = 0; index < _lampCount; index++)
        {
            float angle = MathF.Tau * index / _lampCount;
            Transform transform = new() { Position = new Vector3(MathF.Cos(angle) * _lampRadius, 1, MathF.Sin(angle) * _lampRadius) };
            PointLight light = new() { Color = Palette[index % Palette.Length], Intensity = _lampIntensity, Range = _lampRange, CastsShadows = _lampShadows };
            Entity lamp = _world.Create(transform, light);
            _lamps.Add(lamp);
        }
    }
}
