using System.Collections.Frozen;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FlickerVoid.Editor;

/// <summary>
/// Path data of the editor's icons on a 24 by 24 grid: one outline family, Lucide (ISC), drawn as strokes in the text colour by
/// the theme's <c>PathIcon</c>. The Visual Studio logo is Simple Icons' (CC0) and is filled (<c>PathIcon.filled</c>).
/// </summary>
public static class LucideIcons
{
    public static Geometry Extrude { get; } = Parse("M18 9l-6-6-6 6 M12 3v14 M5 21h14");
    public static Geometry Inset { get; } = Parse("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z M9 8h6a1 1 0 0 1 1 1v6a1 1 0 0 1 -1 1h-6a1 1 0 0 1 -1 -1v-6a1 1 0 0 1 1 -1z");
    public static Geometry Bevel { get; } = Parse("M20.34 17.52a10 10 0 1 0-2.82 2.82 M17 19a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z M13.41 13.41L17.59 17.59 M10 12a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z");
    public static Geometry LoopCut { get; } = Parse("M12 2v20 M16 3h3a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-3 M8 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3");
    public static Geometry Cut { get; } = Parse("M11 16.586V19a1 1 0 0 1-1 1H2L18.37 3.63a1 1 0 1 1 3 3l-9.663 9.663a1 1 0 0 1-1.414 0L8 14");
    public static Geometry Mirror { get; } = Parse("M10 12H8 M16 12h-2 M22 12h-2 M4 12H2 M7.298 20.288A1 1 0 0 0 8 22h8a1 1 0 0 0 .703-1.712l-3.991-3.99a1 1 0 0 0-1.424-.001z M7.298 3.712A1 1 0 0 1 8 2h8a1 1 0 0 1 .703 1.712l-3.991 3.99a1 1 0 0 1-1.424.001z");
    public static Geometry Select { get; } = Parse("M4.037 4.688a.495.495 0 0 1 .651-.651l16 6.5a.5.5 0 0 1-.063.947l-6.124 1.58a2 2 0 0 0-1.438 1.435l-1.579 6.126a.5.5 0 0 1-.947.063z");
    public static Geometry Move { get; } = Parse("M12 2v20 M15 19l-3 3-3-3 M19 9l3 3-3 3 M2 12h20 M5 9l-3 3 3 3 M9 5l3-3 3 3");
    public static Geometry Rotate { get; } = Parse("M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8 M21 3v5h-5");
    public static Geometry Scale { get; } = Parse("M12 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7 M14 15H9v-5 M16 3h5v5 M21 3 9 15");
    public static Geometry WorldSpace { get; } = Parse("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0z M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20 M2 12h20");
    public static Geometry LocalSpace { get; } = Parse("M5 3v16h16 M5 19l6-6 M2 6l3-3 3 3 M18 16l3 3-3 3");
    public static Geometry Perspective { get; } = Parse("M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z M3.3 7l8.7 5 8.7-5 M12 22V12");
    public static Geometry Grid { get; } = Parse("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z M3 9h18 M3 15h18 M9 3v18 M15 3v18");
    public static Geometry DepthFade { get; } = Parse("M8 9a7 7 0 1 0 14 0a7 7 0 1 0 -14 0z M2 15a7 7 0 1 0 14 0a7 7 0 1 0 -14 0z");
    public static Geometry Play { get; } = Parse("M5 5a2 2 0 0 1 3.008-1.728l11.997 6.998a2 2 0 0 1 .003 3.458l-12 7A2 2 0 0 1 5 19z");
    public static Geometry Duplicate { get; } = Parse("M10 8h10a2 2 0 0 1 2 2v10a2 2 0 0 1 -2 2h-10a2 2 0 0 1 -2 -2v-10a2 2 0 0 1 2 -2z M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2");
    public static Geometry Delete { get; } = Parse("M10 11v6 M14 11v6 M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6 M3 6h18 M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2");
    public static Geometry Save { get; } = Parse("M15.2 3a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M17 21v-7a1 1 0 0 0-1-1H8a1 1 0 0 0-1 1v7 M7 3v4a1 1 0 0 0 1 1h7");
    public static Geometry SaveAssets { get; } = Parse("M10 2v3a1 1 0 0 0 1 1h5 M18 18v-6a1 1 0 0 0-1-1h-6a1 1 0 0 0-1 1v6 M18 22H4a2 2 0 0 1-2-2V6 M8 18a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9.172a2 2 0 0 1 1.414.586l2.828 2.828A2 2 0 0 1 22 6.828V16a2 2 0 0 1-2.01 2z");
    public static Geometry Discard { get; } = Parse("M21 17a9 9 0 0 0-15-6.7L3 13 M3 7v6h6 M11 17a1 1 0 1 0 2 0a1 1 0 1 0 -2 0z");
    public static Geometry Bake { get; } = Parse("M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5 M9 18h6 M10 22h4");
    public static Geometry Settings { get; } = Parse("M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915 M9 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z");
    public static Geometry Materials { get; } = Parse("M12 22a1 1 0 0 1 0-20 10 9 0 0 1 10 9 5 5 0 0 1-5 5h-2.25a1.75 1.75 0 0 0-1.4 2.8l.3.4a1.75 1.75 0 0 1-1.4 2.8z M13 6.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0z M17 10.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0z M6 12.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0z M8 7.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0z");
    public static Geometry GameView { get; } = Parse("M6 11L10 11 M8 9L8 13 M15 12L15.01 12 M18 10L18.01 10 M17.32 5H6.68a4 4 0 0 0-3.978 3.59c-.006.052-.01.101-.017.152C2.604 9.416 2 14.456 2 16a3 3 0 0 0 3 3c1 0 1.5-.5 2-1l1.414-1.414A2 2 0 0 1 9.828 16h4.344a2 2 0 0 1 1.414.586L17 18c.5.5 1 1 2 1a3 3 0 0 0 3-3c0-1.545-.604-6.584-.685-7.258-.007-.05-.011-.1-.017-.151A4 4 0 0 0 17.32 5z");
    public static Geometry Add { get; } = Parse("M5 12h14 M12 5v14");
    public static Geometry Remove { get; } = Parse("M5 12h14");
    public static Geometry Swap { get; } = Parse("M21 16l-4 4-4-4 M17 20V4 M3 8l4-4 4 4 M7 4v16");
    public static Geometry Close { get; } = Parse("M18 6 6 18 M6 6l12 12");
    public static Geometry Refresh { get; } = Parse("M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8 M21 3v5h-5 M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16 M8 16H3v5");
    public static Geometry New { get; } = Parse("M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z M14 2v5a1 1 0 0 0 1 1h5 M9 15h6 M12 18v-6");
    public static Geometry Rename { get; } = Parse("M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z M15 5l4 4");
    public static Geometry Expand { get; } = Parse("M6 9l6 6 6-6");
    public static Geometry Collapse { get; } = Parse("M18 15l-6-6-6 6");
    public static Geometry Folder { get; } = Parse("M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z");
    public static Geometry Scene { get; } = Parse("M8 3l4 8 5-5 5 15H2L8 3z");
    public static Geometry Model { get; } = Parse("M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z M3.3 7l8.7 5 8.7-5 M12 22V12");
    public static Geometry Texture { get; } = Parse("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z M7 9a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z M21 15l-3.086-3.086a2 2 0 0 0-2.828 0L6 21");
    public static Geometry Canvas { get; } = Parse("M11 10l3 3 M6.5 21A3.5 3.5 0 1 0 3 17.5a2.62 2.62 0 0 1-.708 1.792A1 1 0 0 0 3 21z M9.969 17.031 21.378 5.624a1 1 0 0 0-3.002-3.002L6.967 14.031");
    public static Geometry Mood { get; } = Parse("M8 12a4 4 0 1 0 8 0a4 4 0 1 0 -8 0z M12 4h.01 M20 12h.01 M12 20h.01 M4 12h.01 M17.657 6.343h.01 M17.657 17.657h.01 M6.343 17.657h.01 M6.343 6.343h.01");
    public static Geometry Shader { get; } = Parse("M18 16l4-4-4-4 M6 8l-4 4 4 4 M14.5 4l-5 16");
    public static Geometry Data { get; } = Parse("M15 12h-5 M15 8h-5 M19 17V5a2 2 0 0 0-2-2H4 M8 21h12a2 2 0 0 0 2-2v-1a1 1 0 0 0-1-1H11a1 1 0 0 0-1 1v1a2 2 0 1 1-4 0V5a2 2 0 1 0-4 0v2a1 1 0 0 0 1 1h3");
    public static Geometry Table { get; } = Parse("M12 3v18 M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z M3 9h18 M3 15h18");
    public static Geometry Flow { get; } = Parse("M5 3h4a2 2 0 0 1 2 2v4a2 2 0 0 1 -2 2h-4a2 2 0 0 1 -2 -2v-4a2 2 0 0 1 2 -2z M7 11v4a2 2 0 0 0 2 2h4 M15 13h4a2 2 0 0 1 2 2v4a2 2 0 0 1 -2 2h-4a2 2 0 0 1 -2 -2v-4a2 2 0 0 1 2 -2z");
    public static Geometry File { get; } = Parse("M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z M14 2v5a1 1 0 0 0 1 1h5");
    public static Geometry Logo { get; } = Parse("M12 3q1 4 4 6.5t3 5.5a1 1 0 0 1-14 0 5 5 0 0 1 1-3 1 1 0 0 0 5 0c0-2-1.5-3-1.5-5q0-2 2.5-4");
    public static Geometry Search { get; } = Parse("M21 21l-4.34-4.34 M3 11a8 8 0 1 0 16 0a8 8 0 1 0 -16 0z");
    public static Geometry Camera { get; } = Parse("M16 13l5.223 3.482a.5.5 0 0 0 .777-.416V7.87a.5.5 0 0 0-.752-.432L16 10.5 M4 6h10a2 2 0 0 1 2 2v8a2 2 0 0 1 -2 2h-10a2 2 0 0 1 -2 -2v-8a2 2 0 0 1 2 -2z");
    public static Geometry PointLight { get; } = Parse("M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5 M9 18h6 M10 22h4");
    public static Geometry SpotLight { get; } = Parse("M12 2v5 M14.829 15.998a3 3 0 1 1-5.658 0 M20.92 14.606A1 1 0 0 1 20 16H4a1 1 0 0 1-.92-1.394l3-7A1 1 0 0 1 7 7h10a1 1 0 0 1 .92.606z");
    public static Geometry Empty { get; } = Parse("M3 7V5a2 2 0 0 1 2-2h2 M17 3h2a2 2 0 0 1 2 2v2 M21 17v2a2 2 0 0 1-2 2h-2 M7 21H5a2 2 0 0 1-2-2v-2");
    public static Geometry Entity { get; } = Parse("M8.3 10a.7.7 0 0 1-.626-1.079L11.4 3a.7.7 0 0 1 1.198-.043L16.3 8.9a.7.7 0 0 1-.572 1.1Z M4 14h5a1 1 0 0 1 1 1v5a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-5a1 1 0 0 1 1 -1z M14 17.5a3.5 3.5 0 1 0 7 0a3.5 3.5 0 1 0 -7 0z");
    public static Geometry Character { get; } = Parse("M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2 M8 7a4 4 0 1 0 8 0a4 4 0 1 0 -8 0z");
    public static Geometry FolderOpen { get; } = Parse("M6 14l1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2");
    public static Geometry NewFolder { get; } = Parse("M12 10v6 M9 13h6 M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z");
    public static Geometry Open { get; } = Parse("M21 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h6 M21 3l-9 9 M15 3h6v6");
    public static Geometry Up { get; } = Parse("M5 12l7-7 7 7 M12 19V5");
    public static Geometry Back { get; } = Parse("M15 18l-6-6 6-6");
    public static Geometry Next { get; } = Parse("M9 18l6-6-6-6");
    public static Geometry Code { get; } = Parse("M8 3H7a2 2 0 0 0-2 2v5a2 2 0 0 1-2 2 2 2 0 0 1 2 2v5c0 1.1.9 2 2 2h1 M16 21h1a2 2 0 0 0 2-2v-5c0-1.1.9-2 2-2a2 2 0 0 1-2-2V5a2 2 0 0 0-2-2h-1");
    public static Geometry Import { get; } = Parse("M12 15V3 M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5");
    public static Geometry Reimport { get; } = Parse("M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8 M3 3v5h5 M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16 M16 16h5v5");
    public static Geometry Undo { get; } = Parse("M9 14 4 9l5-5 M4 9h10.5a5.5 5.5 0 0 1 5.5 5.5a5.5 5.5 0 0 1-5.5 5.5H11");
    public static Geometry Redo { get; } = Parse("M15 14l5-5-5-5 M20 9H9.5A5.5 5.5 0 0 0 4 14.5A5.5 5.5 0 0 0 9.5 20H13");
    public static Geometry Visible { get; } = Parse("M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0 M9 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z");
    public static Geometry Invisible { get; } = Parse("M10.733 5.076a10.744 10.744 0 0 1 11.205 6.575 1 1 0 0 1 0 .696 10.747 10.747 0 0 1-1.444 2.49 M14.084 14.158a3 3 0 0 1-4.242-4.242 M17.479 17.499a10.75 10.75 0 0 1-15.417-5.151 1 1 0 0 1 0-.696 10.75 10.75 0 0 1 4.446-5.143 M2 2l20 20");
    public static Geometry Nested { get; } = Parse("M8 5h13 M13 12h8 M13 19h8 M3 10a2 2 0 0 0 2 2h3 M3 5v12a2 2 0 0 0 2 2h3");
    public static Geometry Vehicle { get; } = Parse("M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2 M5 17a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z M9 17h6 M15 17a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z");
    public static Geometry More { get; } = Parse("M11 12a1 1 0 1 0 2 0a1 1 0 1 0 -2 0z M18 12a1 1 0 1 0 2 0a1 1 0 1 0 -2 0z M4 12a1 1 0 1 0 2 0a1 1 0 1 0 -2 0z");
    public static Geometry Check { get; } = Parse("M20 6 9 17l-5-5");
    public static Geometry Snap { get; } = Parse("M12 15l4 4 M2.352 10.648a1.205 1.205 0 0 0 0 1.704l2.296 2.296a1.205 1.205 0 0 0 1.704 0l6.029-6.029a1 1 0 1 1 3 3l-6.029 6.029a1.205 1.205 0 0 0 0 1.704l2.296 2.296a1.205 1.205 0 0 0 1.704 0l6.365-6.367A1 1 0 0 0 8.716 4.282z M5 8l4 4");
    public static Geometry Sun { get; } = Parse("M8 12a4 4 0 1 0 8 0a4 4 0 1 0 -8 0z M12 2v2 M12 20v2 M4.93 4.93l1.41 1.41 M17.66 17.66l1.41 1.41 M2 12h2 M20 12h2 M6.34 17.66l-1.41 1.41 M19.07 4.93l-1.41 1.41");
    public static Geometry Sky { get; } = Parse("M17.5 19H9a7 7 0 1 1 6.71-9h1.79a4.5 4.5 0 1 1 0 9Z");
    public static Geometry Fog { get; } = Parse("M4 14.899A7 7 0 1 1 15.71 8h1.79a4.5 4.5 0 0 1 2.5 8.242 M16 17H7 M17 21H9");
    public static Geometry Grade { get; } = Parse("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0z M12 18a6 6 0 0 0 0-12v12z");
    public static Geometry Maximize { get; } = Parse("M8 3H5a2 2 0 0 0-2 2v3 M21 8V5a2 2 0 0 0-2-2h-3 M3 16v3a2 2 0 0 0 2 2h3 M16 21h3a2 2 0 0 0 2-2v-3");
    public static Geometry Layers { get; } = Parse("M12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83z M2 12a1 1 0 0 0 .58.91l8.6 3.91a2 2 0 0 0 1.65 0l8.58-3.9A1 1 0 0 0 22 12 M2 17a1 1 0 0 0 .58.91l8.6 3.91a2 2 0 0 0 1.65 0l8.58-3.9A1 1 0 0 0 22 17");
    public static Geometry Transform { get; } = Parse("M5 3v16h16 M5 19l6-6 M2 6l3-3 3 3 M18 16l3 3-3 3");
    public static Geometry Look { get; } = Parse("M14.622 17.897l-10.68-2.913 M18.376 2.622a1 1 0 1 1 3.002 3.002L17.36 9.643a.5.5 0 0 0 0 .707l.944.944a2.41 2.41 0 0 1 0 3.408l-.944.944a.5.5 0 0 1-.707 0L8.354 7.348a.5.5 0 0 1 0-.707l.944-.944a2.41 2.41 0 0 1 3.408 0l.944.944a.5.5 0 0 0 .707 0z M9 8c-1.804 2.71-3.97 3.46-6.583 3.948a.507.507 0 0 0-.302.819l7.32 8.883a1 1 0 0 0 1.185.204C12.735 20.405 16 16.792 16 15");
    public static Geometry Physics { get; } = Parse("M8.3 10a.7.7 0 0 1-.626-1.079L11.4 3a.7.7 0 0 1 1.198-.043L16.3 8.9a.7.7 0 0 1-.572 1.1Z M4 14h5a1 1 0 0 1 1 1v5a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-5a1 1 0 0 1 1 -1z M14 17.5a3.5 3.5 0 1 0 7 0a3.5 3.5 0 1 0 -7 0z");
    public static Geometry Behaviour { get; } = Parse("M12 18V5 M15 13a4.17 4.17 0 01-3-4 4.17 4.17 0 01-3 4 M17.707 6.294A3 3 0 1012 5a3 3 0 10-5.707 1.294 M17.996 5.125a4 4 0 012.526 5.77 M18 18a4 4 0 002-7.465 M19.967 17.482a4 4 0 11-7.967.517 4 4 0 11-7.967-.517 M6 18a4 4 0 01-2-7.465 M6.004 5.125a4 4 0 00-2.526 5.77");
    public static Geometry Story { get; } = Parse("M12 5v16 M20.001 19A2 2 0 0022 17V5a2 2 0 00-1.999-2L16 3.002A5 5 0 0012 5a5 5 0 00-4-2H4a2 2 0 00-2 2v12a2 2 0 001.999 2H8a5 5 0 014 2 5 5 0 014-2z");
    public static Geometry Marker { get; } = Parse("M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0 M9 10a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z");
    public static Geometry Log { get; } = Parse("M12 19h8 M4 17l6-6-6-6");
    public static Geometry Inspector { get; } = Parse("M10 5H3 M12 19H3 M14 3v4 M16 17v4 M21 12h-9 M21 19h-5 M21 5h-7 M8 10v4 M8 12H3");
    public static Geometry Filter { get; } = Parse("M2 5h20 M6 12h12 M9 19h6");
    public static Geometry Lens { get; } = Parse("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0z M14.31 8l5.74 9.94 M9.69 8h11.48 M7.38 12l5.74-9.94 M9.69 16 3.95 6.06 M14.31 16H2.83 M16.62 12l-5.74 9.94");
    public static Geometry Unsaved { get; } = Parse("M11 12a1 1 0 1 0 2 0a1 1 0 1 0 -2 0z M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0z");
    public static Geometry Minimize { get; } = Parse("M5 12h14");
    public static Geometry Restore { get; } = Parse("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z");
    public static Geometry Tree { get; } = Parse("M8 19a4 4 0 0 1-2.24-7.32A3.5 3.5 0 0 1 9 6.03V6a3 3 0 1 1 6 0v.04a3.5 3.5 0 0 1 3.24 5.65A4 4 0 0 1 16 19Z M12 19v3");

    public static Geometry Building { get; } = Parse("M10 12h4 M10 8h4 M14 21v-3a2 2 0 0 0-4 0v3 M6 10H4a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-2 M6 21V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v16");
    public static Geometry Roof { get; } = Parse("M13.73 4a2 2 0 0 0-3.46 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z");
    public static Geometry Item { get; } = Parse("M11 21.73a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73z M12 22V12 M3.29 7L12 12L20.71 7 M7.5 4.27l9 5.15");
    public static Geometry Key { get; } = Parse("M2.586 17.414A2 2 0 0 0 2 18.828V21a1 1 0 0 0 1 1h3a1 1 0 0 0 1-1v-1a1 1 0 0 1 1-1h1a1 1 0 0 0 1-1v-1a1 1 0 0 1 1-1h.172a2 2 0 0 0 1.414-.586l.814-.814a6.5 6.5 0 1 0-4-4z M16 7.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0z");
    public static Geometry Talk { get; } = Parse("M22 17a2 2 0 0 1-2 2H6.828a2 2 0 0 0-1.414.586l-2.202 2.202A.71.71 0 0 1 2 21.286V5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2z");
    public static Geometry Network { get; } = Parse("M17 16h4a1 1 0 0 1 1 1v4a1 1 0 0 1 -1 1h-4a1 1 0 0 1 -1 -1v-4a1 1 0 0 1 1 -1z M3 16h4a1 1 0 0 1 1 1v4a1 1 0 0 1 -1 1h-4a1 1 0 0 1 -1 -1v-4a1 1 0 0 1 1 -1z M10 2h4a1 1 0 0 1 1 1v4a1 1 0 0 1 -1 1h-4a1 1 0 0 1 -1 -1v-4a1 1 0 0 1 1 -1z M5 16v-3a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v3 M12 12V8");
    public static Geometry Tiles { get; } = Parse("M4 3h5a1 1 0 0 1 1 1v5a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-5a1 1 0 0 1 1 -1z M15 3h5a1 1 0 0 1 1 1v5a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-5a1 1 0 0 1 1 -1z M15 14h5a1 1 0 0 1 1 1v5a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-5a1 1 0 0 1 1 -1z M4 14h5a1 1 0 0 1 1 1v5a1 1 0 0 1 -1 1h-5a1 1 0 0 1 -1 -1v-5a1 1 0 0 1 1 -1z");
    public static Geometry Moon { get; } = Parse("M20.985 12.486a9 9 0 1 1-9.473-9.472c.405-.022.617.46.402.803a6 6 0 0 0 8.268 8.268c.344-.215.825-.004.803.401");
    public static Geometry Environment { get; } = Parse("M8 12a4 4 0 1 0 8 0a4 4 0 1 0 -8 0z M12 2v2 M12 20v2 M4.93 4.93l1.41 1.41 M17.66 17.66l1.41 1.41 M2 12h2 M20 12h2 M6.34 17.66l-1.41 1.41 M19.07 4.93l-1.41 1.41");
    public static Geometry Door { get; } = Parse("M10 21H2 M10 3H7a2 2 0 00-2 2v16 M14 12h.01 M19 21V5a2 2 0 00-1.675-1.974l-6.163-1.013A1 1 0 0010 3v18a1 1 0 001.124.992z M22 21h-3");
    public static Geometry Zone { get; } = Parse("M5 3a2 2 0 0 0-2 2 M19 3a2 2 0 0 1 2 2 M21 19a2 2 0 0 1-2 2 M5 21a2 2 0 0 1-2-2 M9 3h1 M9 21h1 M14 3h1 M14 21h1 M3 9v1 M21 9v1 M3 14v1 M21 14v1");
    public static Geometry Pin { get; } = Parse("M12 17v5 M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z");
    public static Geometry Cube { get; } = Parse("M10 22v-8 M2.336 8.89 10 14l11.715-7.029 M22 14a2 2 0 0 1-.971 1.715l-10 6a2 2 0 0 1-2.138-.05l-6-4A2 2 0 0 1 2 16v-6a2 2 0 0 1 .971-1.715l10-6a2 2 0 0 1 2.138.05l6 4A2 2 0 0 1 22 8z");
    public static Geometry Sphere { get; } = Parse("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0z");
    public static Geometry Interact { get; } = Parse("M18 11V6a2 2 0 0 0-2-2a2 2 0 0 0-2 2 M14 10V4a2 2 0 0 0-2-2a2 2 0 0 0-2 2v2 M10 10.5V6a2 2 0 0 0-2-2a2 2 0 0 0-2 2v8 M18 8a2 2 0 1 1 4 0v6a8 8 0 0 1-8 8h-2c-2.8 0-4.5-.86-5.99-2.34l-3.6-3.6a2 2 0 0 1 2.83-2.82L7 15");
    public static Geometry Scan { get; } = Parse("M3 7V5a2 2 0 0 1 2-2h2 M17 3h2a2 2 0 0 1 2 2v2 M21 17v2a2 2 0 0 1-2 2h-2 M7 21H5a2 2 0 0 1-2-2v-2 M11 12a1 1 0 1 0 2 0a1 1 0 1 0 -2 0z M18.944 12.33a1 1 0 0 0 0-.66 7.5 7.5 0 0 0-13.888 0 1 1 0 0 0 0 .66 7.5 7.5 0 0 0 13.888 0");
    public static Geometry Inventory { get; } = Parse("M4 10a4 4 0 0 1 4-4h8a4 4 0 0 1 4 4v10a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2z M8 10h8 M8 18h8 M8 22v-6a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v6 M9 6V4a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2");
    public static Geometry Attributes { get; } = Parse("M12 14l4-4 M3.34 19a10 10 0 1 1 17.32 0");
    public static Geometry Trigger { get; } = Parse("M15.914 4a1.5 1.5 0 00-2.474-1.561l-9 9A1.5 1.5 0 005.5 14h4.002a.5.5 0 01.471.666L8.086 20a1.5 1.5 0 002.475 1.56l9-9A1.5 1.5 0 0018.5 10h-3.997a.5.5 0 01-.472-.667z");
    public static Geometry Timeline { get; } = Parse("M12.296 3.464l3.02 3.956 M20.2 6 3 11l-.9-2.4c-.3-1.1.3-2.2 1.3-2.5l13.5-4c1.1-.3 2.2.3 2.5 1.3z M3 11h18v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M6.18 5.276l3.1 3.899");
    public static Geometry Stairs { get; } = Parse("M4 16v-2.38C4 11.5 2.97 10.5 3 8c.03-2.72 1.49-6 4.5-6C9.37 2 10 3.8 10 5.5c0 3.11-2 5.66-2 8.68V16a2 2 0 1 1-4 0Z M20 20v-2.38c0-2.12 1.03-3.12 1-5.62-.03-2.72-1.49-6-4.5-6C14.63 6 14 7.8 14 9.5c0 3.11 2 5.66 2 8.68V20a2 2 0 1 0 4 0Z M16 17h4 M4 13h4");
    public static Geometry Lock { get; } = Parse("M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-7a2 2 0 0 1 2 -2z M7 11V7a5 5 0 0 1 10 0v4");
    public static Geometry Probe { get; } = Parse("M11.017 2.814a1 1 0 0 1 1.966 0l1.051 5.558a2 2 0 0 0 1.594 1.594l5.558 1.051a1 1 0 0 1 0 1.966l-5.558 1.051a2 2 0 0 0-1.594 1.594l-1.051 5.558a1 1 0 0 1-1.966 0l-1.051-5.558a2 2 0 0 0-1.594-1.594l-5.558-1.051a1 1 0 0 1 0-1.966l5.558-1.051a2 2 0 0 0 1.594-1.594z M20 2v4 M22 4h-4 M2 20a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z");
    public static Geometry Outline { get; } = Parse("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0z");
    public static Geometry Path { get; } = Parse("M9 5h8.5a1 1 0 010 7h-11a1 1 0 000 7H15 M15 19a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z M3 5a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z");
    public static Geometry Stop { get; } = Parse("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z");
    public static Geometry Clock { get; } = Parse("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0z M12 6v6l4 2");
    public static Geometry Project { get; } = Parse("M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z M8 10v4 M12 10v2 M16 10v6");

    /// <summary>The Visual Studio logo, filled.</summary>
    public static Geometry VisualStudio { get; } = Parse("M 17.583 0.063 a 1.5 1.5 0 0 0 -1.032 0.392 a 1.5 1.5 0 0 0 -0.001 0 A 0.88 0.88 0 0 0 16.5 0.5 L 8.528 9.316 L 3.875 5.5 l -0.407 -0.35 a 1 1 0 0 0 -1.024 -0.154 a 1 1 0 0 0 -0.012 0.005 l -1.817 0.75 a 1 1 0 0 0 -0.077 0.036 a 1 1 0 0 0 -0.047 0.028 a 1 1 0 0 0 -0.038 0.022 a 1 1 0 0 0 -0.048 0.034 a 1 1 0 0 0 -0.03 0.024 a 1 1 0 0 0 -0.044 0.036 a 1 1 0 0 0 -0.036 0.033 a 1 1 0 0 0 -0.032 0.035 a 1 1 0 0 0 -0.033 0.038 a 1 1 0 0 0 -0.035 0.044 a 1 1 0 0 0 -0.024 0.034 a 1 1 0 0 0 -0.032 0.05 a 1 1 0 0 0 -0.02 0.035 a 1 1 0 0 0 -0.024 0.05 a 1 1 0 0 0 -0.02 0.045 a 1 1 0 0 0 -0.016 0.044 a 1 1 0 0 0 -0.016 0.047 a 1 1 0 0 0 -0.015 0.055 a 1 1 0 0 0 -0.01 0.04 a 1 1 0 0 0 -0.008 0.054 a 1 1 0 0 0 -0.006 0.05 A 1 1 0 0 0 0 6.668 v 10.666 a 1 1 0 0 0 0.615 0.917 l 1.817 0.764 a 1 1 0 0 0 1.035 -0.164 l 0.408 -0.35 l 4.653 -3.815 l 7.973 8.815 a 1.5 1.5 0 0 0 0.072 0.065 a 1.5 1.5 0 0 0 0.057 0.05 a 1.5 1.5 0 0 0 0.058 0.042 a 1.5 1.5 0 0 0 0.063 0.044 a 1.5 1.5 0 0 0 0.065 0.038 a 1.5 1.5 0 0 0 0.065 0.036 a 1.5 1.5 0 0 0 0.068 0.031 a 1.5 1.5 0 0 0 0.07 0.03 a 1.5 1.5 0 0 0 0.073 0.025 a 1.5 1.5 0 0 0 0.066 0.02 a 1.5 1.5 0 0 0 0.08 0.02 a 1.5 1.5 0 0 0 0.068 0.014 a 1.5 1.5 0 0 0 0.075 0.01 a 1.5 1.5 0 0 0 0.075 0.008 a 1.5 1.5 0 0 0 0.073 0.003 a 1.5 1.5 0 0 0 0.077 0 a 1.5 1.5 0 0 0 0.078 -0.005 a 1.5 1.5 0 0 0 0.067 -0.007 a 1.5 1.5 0 0 0 0.087 -0.015 a 1.5 1.5 0 0 0 0.06 -0.012 a 1.5 1.5 0 0 0 0.08 -0.022 a 1.5 1.5 0 0 0 0.068 -0.02 a 1.5 1.5 0 0 0 0.07 -0.028 a 1.5 1.5 0 0 0 0.09 -0.037 l 4.944 -2.377 a 1.5 1.5 0 0 0 0.476 -0.362 a 1.5 1.5 0 0 0 0.09 -0.112 a 1.5 1.5 0 0 0 0.004 -0.007 a 1.5 1.5 0 0 0 0.08 -0.125 a 1.5 1.5 0 0 0 0.062 -0.12 a 1.5 1.5 0 0 0 0.009 -0.017 a 1.5 1.5 0 0 0 0.04 -0.108 a 1.5 1.5 0 0 0 0.015 -0.037 a 1.5 1.5 0 0 0 0.03 -0.107 a 1.5 1.5 0 0 0 0.009 -0.037 a 1.5 1.5 0 0 0 0.017 -0.1 a 1.5 1.5 0 0 0 0.008 -0.05 a 1.5 1.5 0 0 0 0.006 -0.09 a 1.5 1.5 0 0 0 0.004 -0.08 V 3.942 a 1.5 1.5 0 0 0 0 -0.003 a 1.5 1.5 0 0 0 0 -0.032 a 1.5 1.5 0 0 0 -0.01 -0.15 a 1.5 1.5 0 0 0 -0.84 -1.17 L 18.206 0.21 a 1.5 1.5 0 0 0 -0.622 -0.146 z M 18 6.92 v 10.163 l -6.198 -5.08 z M 3 8.574 l 3.099 3.427 l -3.1 3.426 z");

    private static readonly FrozenDictionary<string, Geometry> s_byName = new Dictionary<string, Geometry>
    {
        [nameof(Select)] = Select,
        [nameof(Move)] = Move,
        [nameof(Rotate)] = Rotate,
        [nameof(Scale)] = Scale,
        [nameof(WorldSpace)] = WorldSpace,
        [nameof(LocalSpace)] = LocalSpace,
        [nameof(Perspective)] = Perspective,
        [nameof(Grid)] = Grid,
        [nameof(DepthFade)] = DepthFade,
        [nameof(Play)] = Play,
        [nameof(Duplicate)] = Duplicate,
        [nameof(Delete)] = Delete,
        [nameof(Save)] = Save,
        [nameof(SaveAssets)] = SaveAssets,
        [nameof(Discard)] = Discard,
        [nameof(Bake)] = Bake,
        [nameof(Settings)] = Settings,
        [nameof(Materials)] = Materials,
        [nameof(GameView)] = GameView,
        [nameof(Add)] = Add,
        [nameof(Remove)] = Remove,
        [nameof(Swap)] = Swap,
        [nameof(Close)] = Close,
        [nameof(Refresh)] = Refresh,
        [nameof(New)] = New,
        [nameof(Rename)] = Rename,
        [nameof(Expand)] = Expand,
        [nameof(Collapse)] = Collapse,
        [nameof(Folder)] = Folder,
        [nameof(Scene)] = Scene,
        [nameof(Model)] = Model,
        [nameof(Texture)] = Texture,
        [nameof(Canvas)] = Canvas,
        [nameof(Mood)] = Mood,
        [nameof(Shader)] = Shader,
        [nameof(Data)] = Data,
        [nameof(Table)] = Table,
        [nameof(Flow)] = Flow,
        [nameof(File)] = File,
        [nameof(Logo)] = Logo,
        [nameof(Search)] = Search,
        [nameof(Camera)] = Camera,
        [nameof(PointLight)] = PointLight,
        [nameof(SpotLight)] = SpotLight,
        [nameof(Empty)] = Empty,
        [nameof(Entity)] = Entity,
        [nameof(Character)] = Character,
        [nameof(FolderOpen)] = FolderOpen,
        [nameof(NewFolder)] = NewFolder,
        [nameof(Open)] = Open,
        [nameof(Up)] = Up,
        [nameof(Back)] = Back,
        [nameof(Next)] = Next,
        [nameof(Code)] = Code,
        [nameof(Import)] = Import,
        [nameof(Reimport)] = Reimport,
        [nameof(Undo)] = Undo,
        [nameof(Redo)] = Redo,
        [nameof(Visible)] = Visible,
        [nameof(Invisible)] = Invisible,
        [nameof(Nested)] = Nested,
        [nameof(Vehicle)] = Vehicle,
        [nameof(More)] = More,
        [nameof(Check)] = Check,
        [nameof(Snap)] = Snap,
        [nameof(Sun)] = Sun,
        [nameof(Sky)] = Sky,
        [nameof(Fog)] = Fog,
        [nameof(Grade)] = Grade,
        [nameof(Maximize)] = Maximize,
        [nameof(Layers)] = Layers,
        [nameof(Transform)] = Transform,
        [nameof(Look)] = Look,
        [nameof(Physics)] = Physics,
        [nameof(Behaviour)] = Behaviour,
        [nameof(Story)] = Story,
        [nameof(Marker)] = Marker,
        [nameof(Log)] = Log,
        [nameof(Inspector)] = Inspector,
        [nameof(Filter)] = Filter,
        [nameof(Lens)] = Lens,
        [nameof(Unsaved)] = Unsaved,
        [nameof(Minimize)] = Minimize,
        [nameof(Restore)] = Restore,
        [nameof(Building)] = Building,
        [nameof(Tree)] = Tree,
        [nameof(Roof)] = Roof,
        [nameof(Item)] = Item,
        [nameof(Key)] = Key,
        [nameof(Talk)] = Talk,
        [nameof(Network)] = Network,
        [nameof(Tiles)] = Tiles,
        [nameof(Moon)] = Moon,
        [nameof(Environment)] = Environment,
        [nameof(Door)] = Door,
        [nameof(Extrude)] = Extrude,
        [nameof(Inset)] = Inset,
        [nameof(Bevel)] = Bevel,
        [nameof(LoopCut)] = LoopCut,
        [nameof(Cut)] = Cut,
        [nameof(Mirror)] = Mirror,
        [nameof(Zone)] = Zone,
        [nameof(Pin)] = Pin,
        [nameof(Cube)] = Cube,
        [nameof(Sphere)] = Sphere,
        [nameof(Interact)] = Interact,
        [nameof(Scan)] = Scan,
        [nameof(Inventory)] = Inventory,
        [nameof(Attributes)] = Attributes,
        [nameof(Trigger)] = Trigger,
        [nameof(Timeline)] = Timeline,
        [nameof(Stairs)] = Stairs,
        [nameof(Lock)] = Lock,
        [nameof(Probe)] = Probe,
        [nameof(Outline)] = Outline,
        [nameof(Path)] = Path,
        [nameof(Stop)] = Stop,
        [nameof(Clock)] = Clock,
        [nameof(Project)] = Project,
    }.ToFrozenDictionary();

    /// <summary>Icons by name, for view models that name theirs without seeing Avalonia (menu choices, scene items).</summary>
    public static FuncValueConverter<string?, Geometry?> ByName { get; } = new(Find);

    public static Geometry? Find(string? name)
    {
        if (name is not null && s_byName.TryGetValue(name, out Geometry? icon))
        {
            return icon;
        }

        return null;
    }

    private static Geometry Parse(string data) => StreamGeometry.Parse(data);
}
