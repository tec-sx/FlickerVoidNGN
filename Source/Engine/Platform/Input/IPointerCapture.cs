namespace FlickerVoid.Engine.Platform.Input;

/// <summary>Something drawn over the game, such as its interface, that takes the mouse's buttons while the pointer is over it.</summary>
public interface IPointerCapture
{
    bool CapturesPointer { get; }
}
