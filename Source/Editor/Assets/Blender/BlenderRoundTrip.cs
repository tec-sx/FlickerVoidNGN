using System.Reflection;

using FlickerVoid.Editor.Assets.Vehicles;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Vehicles;

namespace FlickerVoid.Editor.Assets.Blender;

/// <summary>
/// Opens a creation in Blender (milestone 3 of the creation tools). The first time, its greyboxes are written as its look file
/// (<c>Van.glb</c> beside <c>Van.fvvehicle</c>) and Blender starts on them through the round trip script, which saves
/// <c>Van.blend</c>; later Blender opens that file. The script exports the look again on every save, the content build bakes it and
/// the creation is drawn with it.
/// </summary>
public sealed class BlenderRoundTrip(IContentManager content, IBlenderLauncher launcher, Func<string?> findBlender)
{
    private const string ScriptName = "flickervoid_round_trip.py";

    /// <summary>Opens the vehicle at the content path in Blender, and says what happened.</summary>
    public string Open(string vehiclePath, VehicleFile vehicle)
    {
        string? blender = findBlender();
        if (blender is null)
        {
            return $"Blender was not found. Install it, or name its program in the {BlenderLocator.Variable} environment variable.";
        }

        string lookPath = VehicleLook.GetPath(vehiclePath);
        string look = content.GetSourcePath(lookPath);
        string blend = Path.ChangeExtension(look, ".blend");
        List<string> arguments = [];
        if (File.Exists(blend))
        {
            arguments.Add(blend);
        }
        else if (!File.Exists(look))
        {
            string name = Path.GetFileNameWithoutExtension(vehiclePath);
            byte[] exported = VehicleExport.ToGlb(vehicle, name);
            content.Write(lookPath, exported);
        }

        string script = WriteScript();
        arguments.AddRange(["--python", script, "--", look, blend]);
        if (!launcher.Launch(blender, arguments))
        {
            return $"Blender could not be started from {blender}.";
        }

        return $"Opened in Blender. Saving {Path.GetFileName(blend)} there brings the look back here.";
    }

    /// <summary>Writes the round trip script, kept in the editor, where Blender can read it.</summary>
    private static string WriteScript()
    {
        string folder = Path.Combine(Path.GetTempPath(), "FlickerVoid");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, ScriptName);
        Assembly assembly = typeof(BlenderRoundTrip).Assembly;
        using Stream source = assembly.GetManifestResourceStream(ScriptName) ?? throw new InvalidOperationException($"The editor was built without {ScriptName}.");
        using FileStream target = File.Create(path);
        source.CopyTo(target);
        return path;
    }
}
