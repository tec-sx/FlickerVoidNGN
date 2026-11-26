using System.ComponentModel.DataAnnotations;

using Arch.Core;

using FlickerVoid.Engine.Rendering.Sky;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Moods;

/// <summary>Names the environment file a scene's light follows through the day, and sets the world clock when the scene plays.</summary>
public struct SceneEnvironment
{
    private static readonly QueryDescription Environments = new QueryDescription().WithAll<SceneEnvironment>();

    public SceneEnvironment()
    {
    }

    [ContentFile(".fvenv")]
    public string? Environment { get; set; }

    /// <summary>The game hours that pass in a real minute while the scene plays; 0 holds the clock at its hour.</summary>
    public float Speed { get; set; }

    /// <summary>The hour a new game starts at, when this is the first scene with an environment it plays.</summary>
    public float StartHour { get; set; } = 12;

    /// <summary>How far north of the equator the place lies, in degrees (south below 0), which sets how high the sun climbs.</summary>
    [Range(-90, 90)]
    public float Latitude { get; set; } = 45;

    /// <summary>Which way north lies in the world, in degrees turned clockwise seen from above, from -Z.</summary>
    public float North { get; set; }

    /// <summary>The sun's declination, in degrees: 0 at an equinox, about 23.44 at the northern midsummer and -23.44 at midwinter.</summary>
    [Range(-23.44, 23.44)]
    public float Declination { get; set; }

    /// <summary>
    /// The painted sky: a 4:1 panorama, north at both edges and east a quarter across, the horizon at the bottom and the zenith at
    /// the top, imported as a mask. Red is the position in the gradient (black the horizon's colour, white the zenith's), green
    /// the band's share, blue how much of the sun's glow it holds back, alpha where stars show. None paints the plain gradient.
    /// </summary>
    [ContentFile(".png", ".tga")]
    public string? SkyMask { get; set; }

    /// <summary>The way the sun crosses the scene's sky.</summary>
    public readonly SunPath GetSunPath() => new(Latitude, North, Declination);

    /// <summary>The environment file an entity of the world names, or null when none does; a scene names one.</summary>
    public static string? Find(World world)
    {
        string? found = null;
        world.Query(in Environments, (ref SceneEnvironment environment) =>
        {
            found ??= environment.Environment;
        });
        return found;
    }

    /// <summary>The sun's path of the first entity of the world with an environment, or null when none has one.</summary>
    public static SunPath? FindSunPath(World world)
    {
        SunPath? found = null;
        world.Query(in Environments, (ref SceneEnvironment environment) =>
        {
            found ??= environment.GetSunPath();
        });
        return found;
    }
}
