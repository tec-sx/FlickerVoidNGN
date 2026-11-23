using Arch.Core;

using FlickerVoid.Editor.Assets;
using FlickerVoid.Editor.Core.Inspector;
using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Cameras;
using FlickerVoid.Engine.Physics;
using FlickerVoid.Engine.Runtime.Components;
using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Editor.Core.Tests.Inspector;

/// <summary>Builds the inspector over an empty scene in a temporary content folder, its game knowing cameras and physics as a project's would.</summary>
internal sealed class InspectorViewModelTestsBuilder : IDisposable
{
    private readonly string _contentFolder;
    private readonly EditorScene _scene;

    public InspectorViewModelTestsBuilder()
    {
        string folderName = Path.GetRandomFileName();
        _contentFolder = Path.Combine(Path.GetTempPath(), folderName);
        Directory.CreateDirectory(_contentFolder);
        _scene = new EditorScene(NullLoggerFactory.Instance, new ContentManager(_contentFolder), [new ProjectModule()]);
        ContentFileFinder files = new(_scene.Content);
        ViewModel = new InspectorViewModel(_scene, files.Find);
    }

    public InspectorViewModel ViewModel { get; }

    public EditorScene Scene => _scene;

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

    public InspectorViewModelTestsBuilder WithContentFile(string path)
    {
        string fullPath = Path.Combine(_contentFolder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "{}");
        return this;
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

    /// <summary>Adds cameras and physics to the editor's game, as Bleeders' gameplay module does.</summary>
    private sealed class ProjectModule : IGameModule
    {
        public void Add(Game game)
        {
            game.AddPhysics();
            game.AddCameras();
        }
    }
}
