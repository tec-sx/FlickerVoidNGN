using FlickerVoid.Engine.Runtime.Data;

namespace FlickerVoid.Editor.Assets.Data;

/// <summary>
/// A data asset file being edited: the editor edits <see cref="Asset"/> in place, and <see cref="ContentDocument{T}.Hold"/> keeps its
/// values unsaved in the content until they are saved or discarded. Throws <see cref="InvalidDataException"/> when the file holds no
/// registered type or can't be read.
/// </summary>
public sealed class DataDocument(UnsavedContent content, DataTypes types, string path)
    : ContentDocument<object>(content, path, file => types.ReadAsset(path, file), types.WriteAsset)
{
    public object Asset => Value;
}
