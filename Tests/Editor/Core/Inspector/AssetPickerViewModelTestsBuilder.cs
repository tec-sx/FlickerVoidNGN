using FlickerVoid.Editor.Core.Inspector;

namespace FlickerVoid.Editor.Core.Tests.Inspector;

/// <summary>Builds an asset picker over a list of content files that tests can add to, recording what it chose.</summary>
internal sealed class AssetPickerViewModelTestsBuilder
{
    private readonly List<string> _assets = [];
    private bool _acceptsNone;

    public List<string?> Chosen { get; } = [];

    public AssetPickerViewModelTestsBuilder WithAssets(params string[] assets)
    {
        _assets.AddRange(assets);
        return this;
    }

    public AssetPickerViewModelTestsBuilder AcceptingNone()
    {
        _acceptsNone = true;
        return this;
    }

    public AssetPickerViewModel Build()
    {
        return new AssetPickerViewModel(() => [.. _assets], _acceptsNone, Chosen.Add);
    }
}
