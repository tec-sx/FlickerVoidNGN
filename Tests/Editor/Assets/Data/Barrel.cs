using System.Text.Json.Serialization;

using FlickerVoid.Engine.Runtime.Data;

namespace FlickerVoid.Editor.Assets.Tests.Data;

/// <summary>A data asset type as a project would register one, and a table row type.</summary>
public sealed class Barrel
{
    public int Litres { get; set; } = 50;
}

public sealed class WineRow
{
    public int Year { get; set; }
}

[JsonSerializable(typeof(Barrel))]
[JsonSerializable(typeof(WineRow))]
internal sealed partial class BarrelJsonContext : JsonSerializerContext
{
    public static DataTypes CreateTypes()
    {
        DataTypes types = new();
        types.AddResolver(Default);
        types.Register<Barrel>("barrel");
        types.RegisterRow<WineRow>("wine");
        return types;
    }
}
