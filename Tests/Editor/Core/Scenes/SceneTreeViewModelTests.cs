using Arch.Core;

using FlickerVoid.Editor.Core.Scenes;
using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class SceneTreeViewModelTests : IDisposable
{
    private const string Room = """
        { "entities": [
          { "name": "Camera", "components": { "transform": {}, "camera": {} } },
          { "name": "Lamp", "folder": "Lights", "components": { "transform": {}, "pointLight": {} } },
          { "name": "Spot", "folder": "Lights/Hall", "components": { "transform": {}, "spotLight": {} } },
          { "name": "Box", "components": { "transform": {} } }
        ] }
        """;

    private readonly EditorSceneTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void ShowsFoldersFirstThenEntitiesInTheFilesOrder()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        tree.Items.Select(item => item.Name).Should().Equal("Lights", "Camera", "Box");
        SceneTreeItem lights = tree.Items[0];
        lights.IsFolder.Should().BeTrue();
        lights.Children.Select(item => item.Name).Should().Equal("Hall", "Lamp");
        lights.Children[0].Children.Select(item => item.Name).Should().Equal("Spot");
    }

    [Fact]
    public void GivesEachItemTheIconOfWhatItIs()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        tree.Items.Select(item => item.Icon).Should().Equal("Folder", "Camera", "Empty");
        tree.Items[0].Children[1].Icon.Should().Be("PointLight");
        tree.Items[0].Children[0].Children[0].Icon.Should().Be("SpotLight");
    }

    [Fact]
    public void SearchingListsTheEntitiesFoundWithoutFoldersAndClearingBringsTheTreeBack()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        tree.SearchText = "p";
        string[] found = [.. tree.Items.Select(item => item.Name)];
        tree.SearchText = "";

        found.Should().Equal("Lamp", "Spot");
        tree.Items.Select(item => item.Name).Should().Equal("Lights", "Camera", "Box");
    }

    [Fact]
    public void AFolderSaysHowManyItemsItHolds()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        tree.Items[0].Count.Should().Be(2);
        tree.Items[0].HasCount.Should().BeTrue();
        tree.Items[1].HasCount.Should().BeFalse();
    }

    [Fact]
    public void ChoosingAFolderSelectsEverythingInIt()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        SceneTreeItem lights = tree.Items[0];

        tree.Choose([lights]);

        _builder.Scene.Selection.Entities.Select(NameOf).Should().BeEquivalentTo("Lamp", "Spot");
        tree.SelectedItems.Should().Contain(lights);
    }

    [Fact]
    public void ChoosingEntitiesSelectsThemWithTheLastForTheInspector()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        tree.Choose([tree.Items[1], tree.Items[2]]);

        _builder.Scene.Selection.Entities.Select(NameOf).Should().Equal("Camera", "Box");
        NameOf(_builder.Scene.Selection.Selected!.Value).Should().Be("Box");
    }

    [Fact]
    public void ShowsASelectionMadeElsewhere()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        Entity camera = _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        _builder.Scene.Selection.Select(camera);

        tree.SelectedItems.Should().ContainSingle().Which.Entity.Should().Be(camera);
    }

    [Fact]
    public void FilesTheSelectionInANewFolder()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        tree.Choose([tree.Items[1], tree.Items[2]]);

        string folder = tree.CreateFolder(null);

        folder.Should().Be("New Folder");
        tree.Items[0].Name.Should().Be("Lights");
        SceneTreeItem created = tree.Items.Single(item => item.Name == "New Folder");
        created.Children.Select(item => item.Name).Should().Equal("Camera", "Box");
        _builder.Scene.IsUnsaved.Should().BeTrue();
    }

    [Fact]
    public void KeepsAnEmptyNewFolderUntilSomethingGoesIn()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");
        _builder.Scene.Selection.Select(null);

        tree.CreateFolder("Lights");
        tree.MoveTo([tree.Items[1].Entity!.Value], "Lights/New Folder");

        tree.Items[0].Children.Select(item => item.Name).Should().Equal("Hall", "New Folder", "Lamp");
        tree.Items[0].Children[1].Children.Select(item => item.Name).Should().Equal("Camera");
    }

    [Fact]
    public void RenamingAFolderMovesEverythingInIt()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        tree.RenameFolder("Lights", "Lamps");

        tree.Items[0].Name.Should().Be("Lamps");
        Entity spot = tree.Items[0].Children[0].Children[0].Entity!.Value;
        _builder.Scene.Game.World.Get<EntityFolder>(spot).Path.Should().Be("Lamps/Hall");
    }

    [Fact]
    public void RenamesAnEntityUnlessAnotherHasTheName()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        bool renamed = tree.RenameEntity(tree.Items[2], "Crate");
        bool taken = tree.RenameEntity(tree.Items[2], "Camera");

        renamed.Should().BeTrue();
        taken.Should().BeFalse();
        tree.Items[2].Name.Should().Be("Crate");
    }

    [Fact]
    public void SavesAnEntityAsATemplateInTheTemplatesFolderUnderAFreeName()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Templates/Box.fventity", """{ "components": {} }""").WithScene("Room.fvscene", Room).Open("Room.fvscene");

        string? path = tree.SaveAsTemplate(tree.Items[2]);

        path.Should().Be("Templates/Box 2.fventity");
        _builder.Scene.GetTemplate(tree.Items[2].Entity!.Value).Should().Be(path);
    }

    [Fact]
    public void DeletingAFolderKeepsItsEntitiesInItsParent()
    {
        SceneTreeViewModel tree = new(_builder.Scene);
        _builder.WithScene("Room.fvscene", Room).Open("Room.fvscene");

        tree.DeleteFolder("Lights/Hall");

        tree.Items[0].Children.Select(item => item.Name).Should().Equal("Lamp", "Spot");
    }

    private string NameOf(Entity entity) => _builder.Scene.Game.World.Get<EntityName>(entity).Value;
}
