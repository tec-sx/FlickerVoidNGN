namespace FlickerVoid.Engine.Creations;

/// <summary>A creation's file as the creators' shared tools see it: its parts by id, and its doors, which the door tool cuts out of them.</summary>
public interface ICreationFile
{
    /// <summary>Every part with its id, in the order the kind lists them.</summary>
    IReadOnlyList<(string Id, SolidPart Part)> GetParts();

    /// <summary>The doors, numbered from 1 in order: <c>door.1</c>, <c>door.2</c>; the door tool adds and replaces them.</summary>
    List<SolidPart> Doors { get; }
}
