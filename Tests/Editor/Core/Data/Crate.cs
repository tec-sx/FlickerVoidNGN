using System.Text.Json.Serialization;

using FlickerVoid.Engine.Gameplay.Definitions;

namespace FlickerVoid.Editor.Core.Tests.Data;

/// <summary>A data asset type with fragments, as a project would declare one.</summary>
public sealed class Crate : Definition
{
    public int Slots { get; set; } = 4;

    public List<CrateFragment> Fragments { get; set; } = [];
}

public abstract class CrateFragment : Fragment;

public sealed class LockedFragment : CrateFragment
{
    public string Key { get; set; } = "";
}

public sealed class LootFragment : CrateFragment
{
    public int Count { get; set; } = 1;
}

[JsonSerializable(typeof(Crate))]
[JsonSerializable(typeof(LockedFragment))]
[JsonSerializable(typeof(LootFragment))]
internal sealed partial class CrateJsonContext : JsonSerializerContext;
