using System.Text.Json;

using FlickerVoid.Editor.Assets.Props;
using FlickerVoid.Engine.Props;
using FlickerVoid.Engine.Props.Serialization;

namespace FlickerVoid.Editor.Assets.Tests.Props;

/// <summary>Builds the prop creator's panel over Box.fvprop, a new prop, in a temporary content folder with the editor's unsaved content.</summary>
internal sealed class PropPanelViewModelTestsBuilder : IDisposable
{
    public const string Box = "Box.fvprop";

    private readonly UnsavedContentTestsBuilder _files = new();

    public PropPanelViewModelTestsBuilder()
    {
        PropFile box = PropFile.Create();
        string text = JsonSerializer.Serialize(box, PropsJsonContext.Default.PropFile);
        _files.WithFile(Box, text);
        Content = _files.Build();
    }

    public UnsavedContent Content { get; }

    public PropPanelViewModel Build()
    {
        PropDocument document = new(Content, Box);
        return new PropPanelViewModel(document);
    }

    /// <summary>The prop as every load of the file sees it, such as a placed copy's.</summary>
    public PropFile Load() => Content.Load(Box, PropsJsonContext.Default.PropFile);

    public void Dispose() => _files.Dispose();
}
