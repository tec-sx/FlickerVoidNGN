using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Cameras.Directing;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Services;

using Microsoft.Extensions.Logging;

namespace FlickerVoid.Engine.Cameras.Layers;

/// <summary>
/// Runs a shot's camera layers for the director: the layers of the rig it names, plus a default shake and zoom punch when its
/// rig has none, so shakes and punches always show; keeps each layer's state per shot and adds their offsets to the pose.
/// </summary>
internal sealed partial class CameraLayerRunner
{
    private readonly IServiceRegistry _services;
    private readonly ILogger _logger;
    private readonly CameraLayer[] _defaults = [new ShakeLayer(), new ZoomPunchLayer()];
    private readonly Dictionary<(Entity Shot, CameraLayer Layer), object> _states = [];
    private readonly HashSet<string> _failedRigs = [];
    private readonly List<CameraLayer> _layers = [];
    private readonly CameraLayerFrame _frame = new();

    public CameraLayerRunner(IServiceRegistry services, ILogger logger)
    {
        _services = services;
        _logger = logger;
    }

    public List<CameraShakeCommand> Shakes { get; } = [];

    public List<ZoomPunchCommand> Punches { get; } = [];

    /// <summary>
    /// The view moved by the shot's layers for a frame of the given seconds; a frame of no time (a switch between frames) leaves the
    /// requests for the next.
    /// </summary>
    public RenderCamera Apply(Entity shot, string rig, CameraRig? placed, RenderCamera view, Vector3? target, Vector2? pointer, float seconds)
    {
        FindLayers(rig, placed);
        _frame.Seconds = seconds;
        _frame.View = view;
        _frame.Target = target;
        _frame.Pointer = pointer;
        _frame.Shakes = [];
        _frame.Punches = [];
        if (seconds > 0)
        {
            _frame.Shakes = Shakes;
            _frame.Punches = Punches;
        }

        CameraOffset offset = CameraOffset.None;
        foreach (CameraLayer layer in _layers)
        {
            object state = GetState(shot, layer);
            CameraOffset layerOffset = layer.Update(state, _frame);
            offset += layerOffset;
        }

        return Move(view, offset);
    }

    /// <summary>Drops the requests the last frame's layers took.</summary>
    public void EndFrame()
    {
        Shakes.Clear();
        Punches.Clear();
    }

    public void Forget(Entity shot)
    {
        List<(Entity Shot, CameraLayer Layer)> keys = [.. _states.Keys.Where(key => key.Shot == shot)];
        foreach ((Entity Shot, CameraLayer Layer) key in keys)
        {
            _states.Remove(key);
        }
    }

    private static RenderCamera Move(RenderCamera view, in CameraOffset offset)
    {
        if (offset == CameraOffset.None)
        {
            return view;
        }

        Quaternion rotation = view.Transform.Rotation;
        Vector3 moved = Vector3.Transform(offset.Position, rotation);
        Vector3 position = view.Transform.Position + moved;
        Vector3 radians = offset.Rotation * (MathF.PI / 180);
        Quaternion turn = Quaternion.CreateFromYawPitchRoll(radians.Y, radians.X, radians.Z);
        Quaternion turned = Quaternion.Concatenate(turn, rotation);
        Camera lens = view.Camera;
        float zoom = 1 + offset.Zoom;
        lens.OrthographicHeight *= zoom;
        lens.FieldOfView *= zoom;
        Transform pose = new() { Position = position, Rotation = turned };
        return new RenderCamera(lens, pose);
    }

    /// <summary>Fills the layers to run: the placed shot's or the rig's, then each default whose kind they lack.</summary>
    private void FindLayers(string rig, CameraRig? placed)
    {
        _layers.Clear();
        if (placed is not null)
        {
            _layers.AddRange(placed.Layers);
        }
        else if (!string.IsNullOrEmpty(rig) && LoadRig(rig) is { } loaded)
        {
            _layers.AddRange(loaded.Layers);
        }

        foreach (CameraLayer fallback in _defaults)
        {
            Type kind = fallback.GetType();
            if (!_layers.Any(layer => layer.GetType() == kind))
            {
                _layers.Add(fallback);
            }
        }
    }

    private CameraRig? LoadRig(string path)
    {
        DataAssets? data = _services.GetService<DataAssets>();
        if (data is null)
        {
            return null;
        }

        try
        {
            CameraRig rig = data.Load<CameraRig>(path);
            _failedRigs.Remove(path);
            return rig;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            if (_failedRigs.Add(path))
            {
                LogRigFailed(path, exception.Message);
            }

            return null;
        }
    }

    private object GetState(Entity shot, CameraLayer layer)
    {
        if (!_states.TryGetValue((shot, layer), out object? state))
        {
            state = layer.CreateState();
            _states.Add((shot, layer), state);
        }

        return state;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The camera rig {Path} can't be read, so its shot has no layers: {Reason}")]
    private partial void LogRigFailed(string path, string reason);
}
