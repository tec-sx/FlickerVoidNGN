using Jolt = JoltPhysicsSharp;

namespace FlickerVoid.Engine.Physics;

/// <summary>Lets queries pass through one body, such as the one a guard looks out from.</summary>
internal sealed class IgnoredBodyFilter : Jolt.BodyFilter
{
    public Jolt.BodyID Ignored { get; set; } = Jolt.BodyID.Invalid;

    protected override bool ShouldCollide(Jolt.BodyID bodyID) => bodyID != Ignored;

    protected override bool ShouldCollideLocked(Jolt.Body body) => body.ID != Ignored;
}
