using FlickerVoid.Engine.Rendering.Materials;

namespace FlickerVoid.Editor.Assets.Materials;

/// <summary>A master or material in the material browser's tree, with the instances of it nested under it.</summary>
public sealed record MaterialTreeNode(string Path, IReadOnlyList<MaterialTreeNode> Children)
{
    public bool IsMaster => Path.EndsWith(MasterFile.Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>The file's name without its folder or extension.</summary>
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
}
