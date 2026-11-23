using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

namespace FlickerVoid.Engine.Runtime.Tests.Entities;

public sealed class TransformHierarchyTests : IDisposable
{
    private const float Precision = 0.0001f;

    private readonly TransformHierarchyTestsBuilder _builder = new();

    public void Dispose() => _builder.Dispose();

    [Fact]
    public void KeepsChildrenWhereTheyStandWhenTheyAttach()
    {
        _builder.Add("Trigger", new Vector3(1, 0, 0));
        Entity enter = _builder.Add("Enter", new Vector3(3, 0, 0), parent: "Trigger");

        _builder.Hierarchy.Update(_builder.World);

        _builder.PositionOf(enter).Should().Be(new Vector3(3, 0, 0));
    }

    [Fact]
    public void MovesChildrenWithTheirParent()
    {
        Entity trigger = _builder.Add("Trigger", new Vector3(1, 0, 0));
        Entity enter = _builder.Add("Enter", new Vector3(3, 0, 0), parent: "Trigger");
        _builder.Hierarchy.Update(_builder.World);

        _builder.Move(trigger, new Vector3(1, 0, 5));
        _builder.Hierarchy.Update(_builder.World);

        _builder.PositionOf(enter).Should().Be(new Vector3(3, 0, 5));
    }

    [Fact]
    public void TurnsAndScalesChildrenAboutTheirParent()
    {
        Entity trigger = _builder.Add("Trigger", Vector3.Zero);
        Entity enter = _builder.Add("Enter", new Vector3(1, 0, 0), parent: "Trigger");
        _builder.Hierarchy.Update(_builder.World);

        Quaternion quarterTurn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        _builder.Move(trigger, Vector3.Zero, quarterTurn, new Vector3(2, 2, 2));
        _builder.Hierarchy.Update(_builder.World);

        Transform child = _builder.World.Get<Transform>(enter);
        Vector3 expected = Vector3.Transform(new Vector3(2, 0, 0), quarterTurn);
        child.Position.X.Should().BeApproximately(expected.X, Precision);
        child.Position.Z.Should().BeApproximately(expected.Z, Precision);
        child.Scale.Should().Be(new Vector3(2, 2, 2));
        Quaternion.Dot(child.Rotation, quarterTurn).Should().BeApproximately(1, Precision);
    }

    [Fact]
    public void MovesGrandchildrenWithTheTopParent()
    {
        Entity top = _builder.Add("Top", Vector3.Zero);
        Entity grandchild = _builder.Add("Grandchild", new Vector3(2, 0, 0), parent: "Middle");
        _builder.Add("Middle", new Vector3(1, 0, 0), parent: "Top");
        _builder.Hierarchy.Update(_builder.World);

        _builder.Move(top, new Vector3(0, 4, 0));
        _builder.Hierarchy.Update(_builder.World);

        _builder.PositionOf(grandchild).Should().Be(new Vector3(2, 4, 0));
    }

    [Fact]
    public void KeepsAChildWhereItWasMovedToAndFollowsFromThere()
    {
        Entity trigger = _builder.Add("Trigger", Vector3.Zero);
        Entity enter = _builder.Add("Enter", new Vector3(1, 0, 0), parent: "Trigger");
        _builder.Hierarchy.Update(_builder.World);

        _builder.Move(enter, new Vector3(5, 0, 0));
        _builder.Hierarchy.Update(_builder.World);
        _builder.Move(trigger, new Vector3(0, 0, 1));
        _builder.Hierarchy.Update(_builder.World);

        _builder.PositionOf(enter).Should().Be(new Vector3(5, 0, 1));
    }

    [Fact]
    public void LeavesAChildAloneWhenItsParentIsMissing()
    {
        Entity enter = _builder.Add("Enter", new Vector3(1, 0, 0), parent: "Nobody");

        _builder.Hierarchy.Update(_builder.World);

        _builder.PositionOf(enter).Should().Be(new Vector3(1, 0, 0));
    }

    [Fact]
    public void StopsFollowingWhenTheChildIsTakenOffItsParent()
    {
        Entity trigger = _builder.Add("Trigger", Vector3.Zero);
        Entity enter = _builder.Add("Enter", new Vector3(1, 0, 0), parent: "Trigger");
        _builder.Hierarchy.Update(_builder.World);

        _builder.World.Remove<EntityParent>(enter);
        _builder.Move(trigger, new Vector3(0, 0, 3));
        _builder.Hierarchy.Update(_builder.World);

        _builder.PositionOf(enter).Should().Be(new Vector3(1, 0, 0));
    }

    [Fact]
    public void IgnoresParentsThatLoopBackToTheChild()
    {
        Entity first = _builder.Add("First", Vector3.Zero, parent: "Second");
        _builder.Add("Second", new Vector3(1, 0, 0), parent: "First");

        Action update = () => _builder.Hierarchy.Update(_builder.World);

        update.Should().NotThrow();
        _builder.PositionOf(first).Should().Be(Vector3.Zero);
    }
}
