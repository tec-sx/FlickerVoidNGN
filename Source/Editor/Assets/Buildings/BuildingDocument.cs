using FlickerVoid.Editor.Assets.Creators;
using FlickerVoid.Editor.Core.Buildings;
using FlickerVoid.Engine.Buildings;
using FlickerVoid.Engine.Buildings.Serialization;

namespace FlickerVoid.Editor.Assets.Buildings;

/// <summary>
/// A building file being edited in the creator: each change to its plans makes its parts again and is held unsaved, one undo step in
/// the file's history. A part the mesh tools reshape is kept as it is from then on.
/// </summary>
public sealed class BuildingDocument(UnsavedContent content, string path)
    : CreationDocument<BuildingFile>(content, path, BuildingsJsonContext.Default.BuildingFile)
{
    public BuildingFile Building => Value;

    /// <summary>Edits the building, makes its parts again from its plans and holds it.</summary>
    public void Change(Action<BuildingFile> edit)
    {
        edit(Building);
        BuildingTools.Rebuild(Building);
        Hold();
    }

    /// <summary>Gives the building that many storeys on the ground and a cellar or none; throws for a count it doesn't take.</summary>
    public void SetStoreys(int above, bool cellar) => Change(building => BuildingTools.SetStoreys(building, above, cellar));

    /// <summary>A storey's part reshaped by hand is one its plan no longer makes.</summary>
    protected override void Reshaped(string id)
    {
        foreach (BuildingStorey storey in Building.Storeys)
        {
            string prefix = $"{BuildingFile.GetStoreyId(storey.Level)}.";
            if (!id.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string local = id[prefix.Length..];
            storey.Free ??= [];
            if (!storey.Free.Contains(local))
            {
                storey.Free.Add(local);
            }
        }
    }
}
