using System.Text.Json.Serialization.Metadata;

using FlickerVoid.Editor.Core.Data;
using FlickerVoid.Engine.Runtime.Data;

namespace FlickerVoid.Editor.Core.Tests.Data;

/// <summary>Builds the form of a crate, with locked and loot fragments registered, counting the edits it reports.</summary>
internal sealed class DataObjectViewModelTestsBuilder
{
    private readonly DataTypes _types = new();

    public DataObjectViewModelTestsBuilder()
    {
        _types.AddResolver(CrateJsonContext.Default);
        _types.Register<Crate>("crate");
        _types.RegisterDerived<CrateFragment, LockedFragment>("locked");
        _types.RegisterDerived<CrateFragment, LootFragment>("loot");
    }

    public Crate Crate { get; } = new() { Fragments = [new LockedFragment { Key = "Items/Key.fvdata" }] };

    public int Edits { get; private set; }

    public DataObjectViewModel Build()
    {
        JsonTypeInfo typeInfo = _types.Options.GetTypeInfo(typeof(Crate));
        return new DataObjectViewModel("Crate", Crate, typeInfo, () => Edits++);
    }
}
