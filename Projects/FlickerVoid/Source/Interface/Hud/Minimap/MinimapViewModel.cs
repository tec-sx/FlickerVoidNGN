using System.Collections.ObjectModel;
using System.Numerics;

using Arch.Core;

using CommunityToolkit.Mvvm.ComponentModel;

using FlickerVoid.Engine.Maps.Navigation;
using FlickerVoid.Engine.Rendering.Cameras;
using FlickerVoid.Engine.Rendering.Extraction;
using FlickerVoid.Engine.Runtime.Components;

using MinimapData = FlickerVoid.Engine.Maps.Navigation.MinimapView;
using World = Arch.Core.World;

namespace FlickerVoid.Interface.Hud.Minimap;

/// <summary>
/// The HUD's minimap: a compass ring and a map grid turned so the camera's view points up, scrolled under the protagonist, with
/// the protagonist's view cone turned the way it faces, and the map system's markers around the protagonist (its navigator) at the
/// grid's scale. Headings are degrees clockwise from north, which is -Z.
/// </summary>
public sealed partial class MinimapViewModel(World world, MapSystem? maps = null) : ObservableObject
{
    /// <summary>Pixels from the minimap's centre to its edge.</summary>
    public const float Radius = 88;

    /// <summary>How many map pixels a metre of the world takes.</summary>
    public const float PixelsPerMetre = 16;

    /// <summary>The size of one repeat of the map grid image on screen, in pixels.</summary>
    public const float TileSize = 128;

    private static readonly QueryDescription Protagonists = new QueryDescription().WithAll<Gameplay.Protagonist.Protagonist, Transform>();

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

    public ObservableCollection<MinimapMarkerViewModel> Markers { get; } = [];

    public void Update()
    {
        Entity protagonist = FindFirst(Protagonists);
        RenderCamera? camera = ActiveCamera.Find(world);
        IsVisible = protagonist != Entity.Null && camera is not null;
        if (camera is not { } drawn || protagonist == Entity.Null)
        {
            return;
        }

        Transform cameraPlace = drawn.Transform;
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
        UpdateMarkers(cameraHeading);
    }

    private void UpdateMarkers(float cameraHeading)
    {
        List<MarkerView> shown = [];
        if (maps is not null)
        {
            MinimapData view = maps.BuildMinimapView(cameraHeading, Radius / PixelsPerMetre);
            shown = view.Markers;
        }

        while (Markers.Count > shown.Count)
        {
            Markers.RemoveAt(Markers.Count - 1);
        }

        while (Markers.Count < shown.Count)
        {
            Markers.Add(new MinimapMarkerViewModel());
        }

        for (int index = 0; index < shown.Count; index++)
        {
            MarkerView marker = shown[index];
            MinimapMarkerViewModel item = Markers[index];
            item.X = marker.Position.X * Radius;
            item.Y = marker.Position.Y * Radius;
            item.IsTracked = marker.Tracked;
            item.IsClamped = marker.Clamped;
        }
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
