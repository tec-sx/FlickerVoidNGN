namespace FlickerVoid.Engine.Graphics.Upscaling;

/// <summary>What an upscaler is made for: its mode and the size of the output it fills; the device picks the size frames are drawn at.</summary>
public readonly record struct UpscalerDescription(UpscaleMode Mode, int OutputWidth, int OutputHeight);
