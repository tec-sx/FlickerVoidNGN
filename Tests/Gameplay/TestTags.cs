using FlickerVoid.Engine.Gameplay.Tags;

namespace FlickerVoid.Engine.Gameplay.Tests;

/// <summary>Tags shared by the gameplay tests.</summary>
internal static class TestTags
{
    public static readonly Tag A = new("Test.FVFramework.A");

    public static readonly Tag B = new("Test.FVFramework.B");

    public static readonly Tag Group = new("Test.FVFramework.Group");

    public static readonly Tag GroupOne = new("Test.FVFramework.Group.One");

    public static readonly Tag GroupTwo = new("Test.FVFramework.Group.Two");
}
