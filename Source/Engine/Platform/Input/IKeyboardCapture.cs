namespace FlickerVoid.Engine.Platform.Input;

/// <summary>Something drawn over the game, such as a console being typed in, that takes the keyboard while it wants it.</summary>
public interface IKeyboardCapture
{
    bool CapturesKeyboard { get; }
}
