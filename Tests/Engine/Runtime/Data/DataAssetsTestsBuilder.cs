using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Data;
using FlickerVoid.Engine.Runtime.Tests.Content;

namespace FlickerVoid.Engine.Runtime.Tests.Data;

/// <summary>Builds the data assets under test over files in a temporary content folder, with the test item, faction and price types registered.</summary>
internal sealed class DataAssetsTestsBuilder : IDisposable
{
    private readonly ContentManagerTestsBuilder _files = new();
    private ContentManager? _content;

    public DataTypes Types { get; } = CreateTypes();

    public DataAssetsTestsBuilder WithFile(string path, string json)
    {
        _files.WithFile(path, json);
        return this;
    }

    public DataAssets Build()
    {
        _content = new ContentManager(_files.RootDirectory);
        return new DataAssets(_content, Types);
    }

    /// <summary>Rewrites the file and reports it changed, as the content watcher would.</summary>
    public void ChangeFile(string path, string json)
    {
        _files.WithFile(path, json);
        _content!.NotifyChanged(path);
    }

    public void Dispose()
    {
        _files.Dispose();
    }

    private static DataTypes CreateTypes()
    {
        DataTypes types = new();
        types.AddResolver(TestDataJsonContext.Default);
        types.Register<TestItem>("item");
        types.Register<TestFaction>("faction");
        types.RegisterDerived<TestItemFragment, PickupFragment>("pickup");
        types.RegisterDerived<TestItemFragment, UsableFragment>("usable");
        types.RegisterRow<PriceRow>("price");
        return types;
    }
}
