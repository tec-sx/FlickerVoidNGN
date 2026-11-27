using System.ComponentModel;
using System.Diagnostics;

namespace FlickerVoid.Editor.Assets.Blender;

/// <summary>Starts Blender as a process of its own.</summary>
public sealed class BlenderLauncher : IBlenderLauncher
{
    public bool Launch(string blender, IReadOnlyList<string> arguments)
    {
        ProcessStartInfo start = new(blender) { UseShellExecute = false };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using Process? process = Process.Start(start);
            return process is not null;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }
}
