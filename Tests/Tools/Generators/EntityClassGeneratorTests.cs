using Microsoft.CodeAnalysis;

namespace FlickerVoid.Tools.Generators.Tests;

public sealed class EntityClassGeneratorTests
{
    private const string Components = """
        namespace Game.Gameplay.World;

        public struct Lock { public bool IsLocked; }

        public struct Toggle { public bool IsOpen; }
        """;

    private readonly EntityClassGeneratorTestsBuilder _builder = new();

    [Fact]
    public void WritesAHandleWhosePropertiesAreTheEntitysComponents()
    {
        const string door = """
            using Arch.Core;
            using FlickerVoid.Engine.Runtime.Entities;

            namespace Game.Gameplay.World;

            [EntityClass]
            public readonly partial struct Door
            {
                public partial ref Lock Lock { get; }

                public partial ref Toggle Toggle { get; }
            }

            public static class Doors
            {
                public static bool Open(Arch.Core.World world, Entity entity)
                {
                    if (!Door.TryGet(world, entity, out Door door) || door.Lock.IsLocked)
                    {
                        return false;
                    }

                    door.Toggle.IsOpen = true;
                    return true;
                }

                public static T Spawnable<T>() where T : struct, IEntityClass<T> => default;

                public static Door Check() => Spawnable<Door>();
            }
            """;

        (string generated, IReadOnlyList<Diagnostic> errors) = _builder.WithSource(Components).WithSource(door).Run();

        errors.Should().BeEmpty();
        generated.Should().Contain("partial struct Door : global::FlickerVoid.Engine.Runtime.Entities.IEntityClass<global::Game.Gameplay.World.Door>")
            .And.Contain("public static string? Template => null;")
            .And.Contain("WithAll<global::Game.Gameplay.World.Lock, global::Game.Gameplay.World.Toggle>()");
    }

    [Fact]
    public void NamesTheTemplateTheClassSpawnsFrom()
    {
        const string door = """
            using FlickerVoid.Engine.Runtime.Entities;

            namespace Game.Gameplay.World;

            [EntityClass(Template = "Props/Oak \"Door\".fventity")]
            public partial struct Door
            {
                public partial ref Lock Lock { get; }
            }
            """;

        (string generated, IReadOnlyList<Diagnostic> errors) = _builder.WithSource(Components).WithSource(door).Run();

        errors.Should().BeEmpty();
        generated.Should().Contain("public static string? Template => \"Props/Oak \\\"Door\\\".fventity\";");
    }

    [Fact]
    public void ReportsAPartialPropertyThatIsNotAComponentByRef()
    {
        const string door = """
            using FlickerVoid.Engine.Runtime.Entities;

            namespace Game.Gameplay.World;

            [EntityClass]
            public partial struct Door
            {
                public partial Lock Lock { get; }
            }
            """;

        (_, IReadOnlyList<Diagnostic> errors) = _builder.WithSource(Components).WithSource(door).Run();

        errors.Should().Contain(static error => error.Id == "FVG001");
    }

    [Fact]
    public void ReportsAClassOfComponentsThatAreNotStructs()
    {
        const string door = """
            using FlickerVoid.Engine.Runtime.Entities;

            namespace Game.Gameplay.World;

            [EntityClass]
            public partial struct Door
            {
                public partial ref string Name { get; }
            }
            """;

        (_, IReadOnlyList<Diagnostic> errors) = _builder.WithSource(door).Run();

        errors.Should().Contain(static error => error.Id == "FVG001");
    }

    [Fact]
    public void ReportsAClassWithoutComponents()
    {
        const string door = """
            using FlickerVoid.Engine.Runtime.Entities;

            namespace Game.Gameplay.World;

            [EntityClass]
            public partial struct Door;
            """;

        (_, IReadOnlyList<Diagnostic> errors) = _builder.WithSource(door).Run();

        errors.Should().Contain(static error => error.Id == "FVG002");
    }

    [Fact]
    public void ReportsANestedClass()
    {
        const string door = """
            using FlickerVoid.Engine.Runtime.Entities;

            namespace Game.Gameplay.World;

            public static class Props
            {
                [EntityClass]
                public partial struct Door
                {
                    public partial ref Lock Lock { get; }
                }
            }
            """;

        (_, IReadOnlyList<Diagnostic> errors) = _builder.WithSource(Components).WithSource(door).Run();

        errors.Should().Contain(static error => error.Id == "FVG003");
    }
}
