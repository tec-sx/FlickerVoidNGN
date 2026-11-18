using System.Numerics;

using Arch.Core;
using Arch.System;

using FlickerVoid.Engine.Platform;
using FlickerVoid.Engine.Platform.Input;
using FlickerVoid.Engine.Rendering;
using FlickerVoid.Engine.Runtime;
using FlickerVoid.Engine.Runtime.Components;

using Silk.NET.Input;

namespace FlickerVoid.Samples.SceneViewer;

/// <summary>
/// Moves the camera entity round a point: dragging with the left mouse button turns it, the wheel zooms, WASD moves the
/// point across the ground and P switches between orthographic and perspective. Both projections frame the same height at the point.
/// </summary>
public sealed class OrbitCameraSystem : ISystem<GameTime>
{
    private const float RadiansPerPixel = 0.008f;
    private const float ZoomPerNotch = 0.9f;
    private const float MinPitch = -1.5f;
    private const float MaxPitch = -0.05f;

    private readonly IGameWindow _window;
    private readonly World _world;
    private readonly Entity _camera;
    private Vector3 _target;
    private float _yaw = MathF.PI / 4;
    private float _pitch = -0.6f;
    private float _distance;

    public OrbitCameraSystem(IGameWindow window, World world, Entity camera, Vector3 target, float distance)
    {
        _window = window;
        _world = world;
        _camera = camera;
        _target = target;
        _distance = distance;
    }

    public void Initialize()
    {
        Apply();
    }

    public void BeforeUpdate(in GameTime t)
    {
    }

    public void Update(in GameTime t)
    {
        IInputState input = _window.Input;
        if (input.IsDown(MouseButton.Left))
        {
            _yaw -= input.MouseDelta.X * RadiansPerPixel;
            _pitch = Math.Clamp(_pitch - input.MouseDelta.Y * RadiansPerPixel, MinPitch, MaxPitch);
        }

        _distance *= MathF.Pow(ZoomPerNotch, input.ScrollDelta);

        Vector3 move = Vector3.Zero;
        move += Axis(input, Key.D, Key.A) * Vector3.UnitX;
        move += Axis(input, Key.S, Key.W) * Vector3.UnitZ;
        Quaternion turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, _yaw);
        Vector3 groundMove = Vector3.Transform(move, turn);
        _target += groundMove * _distance * (float)t.Elapsed.TotalSeconds;

        if (input.WasPressed(Key.P))
        {
            ref Camera camera = ref _world.Get<Camera>(_camera);
            if (camera.Projection == CameraProjection.Orthographic)
            {
                camera.Projection = CameraProjection.Perspective;
            }
            else
            {
                camera.Projection = CameraProjection.Orthographic;
            }
        }

        Apply();
    }

    public void AfterUpdate(in GameTime t)
    {
    }

    public void Dispose()
    {
    }

    private static float Axis(IInputState input, Key positive, Key negative)
    {
        float value = 0;
        if (input.IsDown(positive))
        {
            value += 1;
        }

        if (input.IsDown(negative))
        {
            value -= 1;
        }

        return value;
    }

    private void Apply()
    {
        Quaternion rotation = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, 0);
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        ref Transform transform = ref _world.Get<Transform>(_camera);
        transform.Position = _target - forward * _distance;
        transform.Rotation = rotation;

        ref Camera camera = ref _world.Get<Camera>(_camera);
        float halfAngle = float.DegreesToRadians(camera.FieldOfView) / 2;
        camera.OrthographicHeight = 2 * _distance * MathF.Tan(halfAngle);
        camera.FarPlane = _distance * 4;
    }
}
