namespace FlickerVoid.Tools.Builder.Paintings;

/// <summary>One layer of a Rebelle painting, as <c>artwork.xml</c> lists it: the file its paint is in, and how it is shown.</summary>
public sealed record PaintingLayer(string FileName, float Opacity, bool Visible);
