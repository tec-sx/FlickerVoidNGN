namespace FlickerVoid.Editor.Assets.Blender;

/// <summary>Starts Blender and lets it run on its own.</summary>
public interface IBlenderLauncher
{
    /// <summary>Starts the program with the arguments; false when it could not be started.</summary>
    bool Launch(string blender, IReadOnlyList<string> arguments);
}
