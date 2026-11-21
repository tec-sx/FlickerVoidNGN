using System.Numerics;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Runtime.Components;

using World = Arch.Core.World;

namespace FlickerVoid.Interface.Hud.Minimap;

/// <summary>
/// The HUD's minimap: a compass ring and a map grid turned so the camera's view points up, scrolled under the protagonist, with
/// the protagonist's view cone turned the way it faces. Headings are degrees clockwise from north, which is -Z.
/// </summary>
public sealed partial class MinimapViewModel(World world) : ObservableObject
{
    /// <summary>How many map pixels a metre of the world takes.</summary>
    public const float PixelsPerMetre = 16;

    /// <summary>The size of one repeat of the map grid image on screen, in pixels.</summary>
    public const float TileSize = 128;

    private static readonly QueryDescription Protagonists = new QueryDescription().WithAll<Gameplay.Protagonist.Protagonist, Transform>();
    private static readonly QueryDescription Cameras = new QueryDescription().WithAll<Camera, Transform>();

    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    /// <summary>The ring's and the map's turn, so the camera's heading points up.</summary>
    [ObservableProperty]
    public partial double RingAngle { get; private set; }

    /// <summary>The view cone's turn on screen: the protagonist's heading against the camera's.</summary>
    [ObservableProperty]
    public partial double FacingAngle { get; private set; }

    [ObservableProperty]
    public partial double MapX { get; private set; }

    [ObservableProperty]
    public partial double MapY { get; private set; }

    public void Update()
    {
        Entity protagonist = FindFirst(Protagonists);
        Entity camera = FindFirst(Cameras);
        IsVisible = protagonist != Entity.Null && camera != Entity.Null;
        if (!IsVisible)
        {
            return;
        }

        Transform cameraPlace = world.Get<Transform>(camera);
        Transform protagonistPlace = world.Get<Transform>(protagonist);
        Vector3 cameraForward = Vector3.Transform(-Vector3.UnitZ, cameraPlace.Rotation);
        Vector3 protagonistForward = Vector3.Transform(Vector3.UnitZ, protagonistPlace.Rotation);
        float cameraHeading = Heading(cameraForward);
        float protagonistHeading = Heading(protagonistForward);
        RingAngle = Normalize(-cameraHeading);
        FacingAngle = Normalize(protagonistHeading - cameraHeading);
        Vector3 position = protagonistPlace.Position;
        MapX = -(position.X * PixelsPerMetre) % TileSize;
        MapY = -(position.Z * PixelsPerMetre) % TileSize;
    }

    private static float Heading(Vector3 direction)
    {
        float radians = MathF.Atan2(direction.X, -direction.Z);
        return float.RadiansToDegrees(radians);
    }

    /// <summary>The same turn between -180 and 180 degrees.</summary>
    private static float Normalize(float degrees)
    {
        float turned = degrees % 360;
        if (turned > 180)
        {
            turned -= 360;
        }
        else if (turned <= -180)
        {
            turned += 360;
        }

        return turned;
    }

    private Entity FindFirst(in QueryDescription query)
    {
        Entity found = Entity.Null;
        world.Query(in query, (Entity entity) =>
        {
            if (found == Entity.Null)
            {
                found = entity;
            }
        });
        return found;
    }
}
