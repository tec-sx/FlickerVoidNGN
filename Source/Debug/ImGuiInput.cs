using FlickerVoid.Engine.Platform.Input;

using Hexa.NET.ImGui;

using Silk.NET.Input;

namespace FlickerVoid.Engine.Debug;

/// <summary>Hands a frame's keyboard and mouse input to Dear ImGui.</summary>
internal static class ImGuiInput
{
    public static void Feed(ImGuiIOPtr io, IInputState input)
    {
        io.AddMousePosEvent(input.MousePosition.X, input.MousePosition.Y);
        FeedButton(io, input, MouseButton.Left, 0);
        FeedButton(io, input, MouseButton.Right, 1);
        FeedButton(io, input, MouseButton.Middle, 2);
        if (input.ScrollDelta != 0)
        {
            io.AddMouseWheelEvent(0, input.ScrollDelta);
        }

        foreach ((Key key, ImGuiKey imGuiKey) in ImGuiKeys.All)
        {
            if (input.WasPressed(key))
            {
                io.AddKeyEvent(imGuiKey, true);
            }

            if (input.WasReleased(key))
            {
                io.AddKeyEvent(imGuiKey, false);
            }
        }

        foreach (char character in input.TypedText)
        {
            io.AddInputCharacter(character);
        }
    }

    private static void FeedButton(ImGuiIOPtr io, IInputState input, MouseButton button, int imGuiButton)
    {
        if (input.WasPressed(button))
        {
            io.AddMouseButtonEvent(imGuiButton, true);
        }

        if (input.WasReleased(button))
        {
            io.AddMouseButtonEvent(imGuiButton, false);
        }
    }
}
