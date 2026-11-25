namespace FlickerVoid.Engine.Graphics.Upscaling;

/// <summary>How frames are drawn for an upscaler: at the output's size with none, or for DLSS to fill in.</summary>
public enum UpscaleMode
{
    /// <summary>Drawn at the output's size, with nothing upscaling them.</summary>
    Off,

    /// <summary>Drawn at about two thirds of the output's width and height, jittered, and upscaled by DLSS.</summary>
    Quality,

    /// <summary>Drawn at the output's size, jittered, and smoothed by DLSS (DLAA).</summary>
    Dlaa,
}
