using FlickerVoid.Editor.Core.Scenes;

namespace FlickerVoid.Editor.Core.Tests.Scenes;

public sealed class EntityPresetGroupTests
{
    [Fact]
    public void GroupsPresetsInTheOrderTheirGroupsFirstAppear()
    {
        EntityPreset point = new("Point Light", ["transform"], "Light");
        EntityPreset mesh = new("Mesh", ["transform"], "Model");
        EntityPreset spot = new("Spot Light", ["transform"], "Light");

        IReadOnlyList<EntityPresetGroup> groups = EntityPresetGroup.From([point, mesh, spot]);

        groups.Select(group => group.Name).Should().Equal("Light", "Model");
        groups[0].Presets.Should().Equal(point, spot);
        groups[1].Presets.Should().Equal(mesh);
    }

    [Fact]
    public void PutsPresetsWithoutAGroupUnderCustom()
    {
        EntityPreset door = new("Door", ["transform"]);

        IReadOnlyList<EntityPresetGroup> groups = EntityPresetGroup.From([door]);

        groups.Should().ContainSingle();
        groups[0].Name.Should().Be(EntityPresetGroup.Custom);
        groups[0].Presets.Should().Equal(door);
    }

    [Fact]
    public void EveryBuiltInPresetHasAGroup()
    {
        EntityPreset.All.Should().OnlyContain(preset => preset.Group != null);
    }
}
