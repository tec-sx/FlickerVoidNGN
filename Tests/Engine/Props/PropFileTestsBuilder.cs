using System.Numerics;

using FlickerVoid.Engine.Creations;
using FlickerVoid.Engine.Runtime.Solids;

namespace FlickerVoid.Engine.Props.Tests;

/// <summary>Makes prop files for the tests.</summary>
internal static class PropFileTestsBuilder
{
    /// <summary>Two boxes, the second a wall with a swinging door placed on it.</summary>
    public static PropFile WithDoor()
    {
        PropFile prop = PropFile.Create();
        string wall = prop.AddBox(new Vector3(3, 2.5f, 0.2f), new Vector3(0, 0, 2));
        SolidMesh leaf = SolidMesh.Box(new Vector3(0.9f, 2, 0.05f));
        PartJoint hinge = new() { Kind = JointKind.Hinge, Axis = Vector3.UnitY, Min = 0, Max = 1.5f, Speed = 2 };
        DoorFit fit = new() { Wall = wall };
        SolidPart door = new() { Mesh = leaf, Joint = hinge, Toggle = new("Interaction.Action.Toggle"), Fit = fit };
        prop.Doors.Add(door);
        return prop;
    }
}
