using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Rendering.Cameras;

/// <summary>Marks the scene's main camera, which names the lens every view draws with. One per scene.</summary>
public struct MainCamera
{
    private static readonly QueryDescription Mains = new QueryDescription().WithAll<MainCamera>();

    public MainCamera()
    {
    }

    /// <summary>The lens (a <c>.fvlens</c> file) every view draws with, under the scene's mood.</summary>
    [ContentFile(".fvlens")]
    public string Lens { get; set; } = string.Empty;

    /// <summary>The lens the world's main camera names, or null without a main camera or a lens named.</summary>
    public static string? FindLens(World world)
    {
        string? found = null;
        world.Query(in Mains, (ref MainCamera main) =>
        {
            if (found is null && !string.IsNullOrEmpty(main.Lens))
            {
                found = main.Lens;
            }
        });
        return found;
    }
}
