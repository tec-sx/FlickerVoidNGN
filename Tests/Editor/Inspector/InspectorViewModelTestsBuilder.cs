using Arch.Core;

using FlickerVoid.Engine.Editor.Inspector;
using FlickerVoid.Engine.Editor.Scenes;
using FlickerVoid.Engine.Runtime.Components;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Editor.Tests.Inspector;

/// <summary>Builds the inspector over an empty scene in a temporary content folder.</summary>
internal sealed class InspectorViewModelTestsBuilder : IDisposable
{
    private readonly string _contentFolder;
    private readonly EditorScene _scene;

    public InspectorViewModelTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
        _scene = new EditorScene(NullLoggerFactory.Instance, _contentFolder);
        ViewModel = new InspectorViewModel(_scene);
    }

    public InspectorViewModel ViewModel { get; }

    public World World => _scene.Game.World;

    /// <summary>Creates a named entity with the components and selects it.</summary>
    public Entity Select<T1, T2>(string name, T1 first, T2 second)
    {
        EntityName entityName = new(name);
        Entity entity = World.Create(entityName, first, second);
        _scene.Selection.Select(entity);
        return entity;
    }

    public Entity Select<T>(T component)
    {
        Entity entity = World.Create(component);
        _scene.Selection.Select(entity);
        return entity;
    }

    public void SelectNothing()
    {
        _scene.Selection.Select(null);
    }

    public ComponentViewModel Component(string title)
    {
        return ViewModel.Components.Single(component => component.Title == title);
    }

    public TField Field<TField>(string component, string label)
        where TField : FieldViewModel
    {
        ComponentViewModel found = Component(component);
        FieldViewModel field = found.Fields.Single(candidate => candidate.Label == label);
        return field.Should().BeOfType<TField>().Subject;
    }

    public void Dispose()
    {
        _scene.Dispose();
        Directory.Delete(_contentFolder, recursive: true);
    }
}
