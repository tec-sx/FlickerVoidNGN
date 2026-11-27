using Arch.Core;

namespace FlickerVoid.Engine.Vehicles;

/// <summary>
/// Marks a placed vehicle as driven, by <see cref="Driver"/>: while it has this, it is a body on wheels that follows the
/// <c>DriveInput</c> beside it; taken off, it brakes, and stands as it did once it has stopped. Game code adds and removes it;
/// it is never saved.
/// </summary>
public struct DrivenVehicle
{
    public Entity Driver;
}
