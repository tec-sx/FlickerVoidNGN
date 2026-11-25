namespace FlickerVoid.Engine.Creations;

/// <summary>
/// Where a part's joint stands (<see cref="Position"/>, degrees for a hinge and a distance for a slider, from its rest place) and
/// where gameplay asks it to go (<see cref="Target"/>); given to every part with a <see cref="PartJoint"/> as it attaches.
/// </summary>
public struct JointPose
{
    public float Position;
    public float Target;
}
