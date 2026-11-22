namespace FlickerVoid.Engine.Cameras.Shots;

/// <summary>Turns a fraction of a move's time into the fraction of the way it has come.</summary>
public static class ShotEaseExtensions
{
    public static float Apply(this ShotEase ease, float fraction)
    {
        float t = Math.Clamp(fraction, 0, 1);
        switch (ease)
        {
            case ShotEase.EaseIn:
                return t * t;
            case ShotEase.EaseOut:
                float left = 1 - t;
                return 1 - (left * left);
            case ShotEase.EaseInOut:
                return t * t * (3 - (2 * t));
            default:
                return t;
        }
    }
}
