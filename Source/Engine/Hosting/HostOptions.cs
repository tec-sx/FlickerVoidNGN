using System.Globalization;

namespace FlickerVoid.Engine.Hosting;

/// <summary>What a <see cref="SceneHost"/> runs: the scene, the lighting mood, the content folder, a frame limit and Vulkan validation.</summary>
public sealed class HostOptions
{
    /// <summary>The scene file to spawn, by content path or full path; none spawns nothing.</summary>
    public string? Scene { get; set; }

    /// <summary>The lighting mood file to draw with; none uses the default mood.</summary>
    public string? Mood { get; set; }

    public string ContentFolder { get; set; } = Path.Combine(AppContext.BaseDirectory, "Content");

    /// <summary>Closes the window after this many frames when above zero.</summary>
    public int FrameLimit { get; set; }

    public bool Validation { get; set; }

    /// <summary>The folder save slots go to; none uses <c>FlickerVoid/&lt;title&gt;/Saves</c> in the user's local application data.</summary>
    public string? SaveFolder { get; set; }

    /// <summary>
    /// Reads <c>--scene &lt;file&gt;</c>, <c>--mood &lt;file&gt;</c>, <c>--frames &lt;count&gt;</c>, <c>--saves &lt;folder&gt;</c> and <c>--validation</c>, starting from
    /// <paramref name="startupScene"/>; throws <see cref="ArgumentException"/> for anything else.
    /// </summary>
    public static HostOptions Parse(IReadOnlyList<string> arguments, string? startupScene = null)
    {
        HostOptions options = new() { Scene = startupScene };
        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            switch (argument)
            {
                case "--scene":
                    options.Scene = ReadValue(arguments, ref index);
                    break;
                case "--mood":
                    options.Mood = ReadValue(arguments, ref index);
                    break;
                case "--frames":
                    string frames = ReadValue(arguments, ref index);
                    options.FrameLimit = int.Parse(frames, CultureInfo.InvariantCulture);
                    break;
                case "--saves":
                    options.SaveFolder = ReadValue(arguments, ref index);
                    break;
                case "--validation":
                    options.Validation = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option {argument}.", nameof(arguments));
            }
        }

        return options;
    }

    private static string ReadValue(IReadOnlyList<string> arguments, ref int index)
    {
        string option = arguments[index];
        index++;
        if (index >= arguments.Count)
        {
            throw new ArgumentException($"{option} needs a value.", nameof(arguments));
        }

        return arguments[index];
    }
}
