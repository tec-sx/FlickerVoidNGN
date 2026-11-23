using System.ComponentModel.DataAnnotations;
using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Cameras;

/// <summary>
/// Marks the scene's main camera, which holds the look every camera starts from: its <see cref="DepthFade"/>, which other cameras
/// change with a <see cref="DepthFadeOverride"/>. One per scene; without one nothing fades.
/// </summary>
public struct MainCamera
{
    private static readonly QueryDescription Mains = new QueryDescription().WithAll<MainCamera>();

    public MainCamera()
    {
    }

    /// <summary>How far in front of the focus things start to darken.</summary>
    public float NearStart { get; set; } = 1;

    /// <summary>How far in front of the focus things are darkened by the whole <see cref="NearAmount"/>.</summary>
    public float NearEnd { get; set; } = 6;

    /// <summary>How much the nearest things darken, from 0 (not at all) to 1 (black).</summary>
    [Range(0, 1)]
    public float NearAmount { get; set; } = 0.35f;

    /// <summary>How far behind the focus things start to melt into <see cref="FarColor"/>.</summary>
    public float FarStart { get; set; } = 2;

    /// <summary>How far behind the focus things melt into <see cref="FarColor"/> by the whole <see cref="FarAmount"/>.</summary>
    public float FarEnd { get; set; } = 20;

    /// <summary>How much the farthest things melt into <see cref="FarColor"/>, from 0 (not at all) to 1 (all colour).</summary>
    [Range(0, 1)]
    public float FarAmount { get; set; } = 0.5f;

    /// <summary>The haze far things melt into, as graded colour.</summary>
    [Color]
    public Vector3 FarColor { get; set; } = new(0.82f, 0.8f, 0.76f);

    public readonly DepthFade ToDepthFade() => new(NearStart, NearEnd, NearAmount, FarStart, FarEnd, FarAmount, FarColor);

    /// <summary>The main camera's fade, or none when the world has no main camera.</summary>
    public static DepthFade Find(World world)
    {
        DepthFade found = default;
        bool any = false;
        world.Query(in Mains, (ref MainCamera main) =>
        {
            if (!any)
            {
                found = main.ToDepthFade();
                any = true;
            }
        });
        return found;
    }

    /// <summary>The fade a camera draws with: the main camera's, changed by the camera's own override when it has one.</summary>
    public static DepthFade FindFor(World world, Entity camera)
    {
        DepthFade main = Find(world);
        if (world.IsAlive(camera) && !world.Has<MainCamera>(camera) && world.TryGet(camera, out DepthFadeOverride change))
        {
            return change.Apply(main);
        }

        return main;
    }
}
