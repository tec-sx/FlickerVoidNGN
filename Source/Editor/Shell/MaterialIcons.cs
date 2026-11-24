using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FlickerVoid.Editor;

/// <summary>
/// Path data of the editor's toolbar icons on a 24 by 24 grid, from Google's Material Icons and, for the cube and the cursor,
/// Pictogrammers' Material Design Icons; both are Apache-2.0. The Visual Studio logo is Simple Icons' (CC0).
/// </summary>
public static class MaterialIcons
{
    public static Avalonia.Media.Geometry Select { get; } = Parse("M13.64,21.97C13.14,22.21 12.54,22 12.31,21.5L10.13,16.76L7.62,18.78C7.45,18.92 7.24,19 7,19A1,1 0 0,1 6,18V3A1,1 0 0,1 7,2C7.24,2 7.47,2.09 7.64,2.23L7.65,2.22L19.14,11.86C19.57,12.22 19.62,12.85 19.27,13.27C19.12,13.45 18.91,13.57 18.7,13.61L15.54,14.23L17.74,18.96C18,19.46 17.76,20.05 17.26,20.28L13.64,21.97Z");

    public static Avalonia.Media.Geometry Move { get; } = Parse("M10 9h4V6h3l-5-5-5 5h3v3zm-1 1H6V7l-5 5 5 5v-3h3v-4zm14 2l-5-5v3h-3v4h3v3l5-5zm-9 3h-4v3H7l5 5 5-5h-3v-3z");

    public static Avalonia.Media.Geometry Rotate { get; } = Parse("M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z");

    public static Avalonia.Media.Geometry Scale { get; } = Parse("M21 11V3h-8l3.29 3.29-10 10L3 13v8h8l-3.29-3.29 10-10z");

    public static Avalonia.Media.Geometry WorldSpace { get; } = Parse("M17.9,17.39C17.64,16.59 16.89,16 16,16H15V13A1,1 0 0,0 14,12H8V10H10A1,1 0 0,0 11,9V7H13A2,2 0 0,0 15,5V4.59C17.93,5.77 20,8.64 20,12C20,14.08 19.2,15.97 17.9,17.39M11,19.93C7.05,19.44 4,16.08 4,12C4,11.38 4.08,10.78 4.21,10.21L9,15V16A2,2 0 0,0 11,18M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z");

    public static Avalonia.Media.Geometry LocalSpace { get; } = Parse("M12,2L16,6H13V13.85L19.53,17.61L21,15.03L22.5,20.5L17,21.96L18.53,19.35L12,15.58L5.47,19.35L7,21.96L1.5,20.5L3,15.03L4.47,17.61L11,13.85V6H8L12,2Z");

    public static Avalonia.Media.Geometry Perspective { get; } = Parse("M21,16.5C21,16.88 20.79,17.21 20.47,17.38L12.57,21.82C12.41,21.94 12.21,22 12,22C11.79,22 11.59,21.94 11.43,21.82L3.53,17.38C3.21,17.21 3,16.88 3,16.5V7.5C3,7.12 3.21,6.79 3.53,6.62L11.43,2.18C11.59,2.06 11.79,2 12,2C12.21,2 12.41,2.06 12.57,2.18L20.47,6.62C20.79,6.79 21,7.12 21,7.5V16.5M12,4.15L6.04,7.5L12,10.85L17.96,7.5L12,4.15M5,15.91L11,19.29V12.58L5,9.21V15.91M19,15.91V9.21L13,12.58V19.29L19,15.91Z");

    public static Avalonia.Media.Geometry Grid { get; } = Parse("M20 2H4c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zM8 20H4v-4h4v4zm0-6H4v-4h4v4zm0-6H4V4h4v4zm6 12h-4v-4h4v4zm0-6h-4v-4h4v4zm0-6h-4V4h4v4zm6 12h-4v-4h4v4zm0-6h-4v-4h4v4zm0-6h-4V4h4v4z");

    public static Avalonia.Media.Geometry DepthFade { get; } = Parse("M11 9h2v2h-2zm-2 2h2v2H9zm4 0h2v2h-2zm2-2h2v2h-2zM7 9h2v2H7zm12-6H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zM9 18H7v-2h2v2zm4 0h-2v-2h2v2zm4 0h-2v-2h2v2zm2-7h-2v2h2v2h-2v-2h-2v2h-2v-2h-2v2H9v-2H7v2H5v-2h2v-2H5V5h14v6z");

    public static Avalonia.Media.Geometry Pan { get; } = Parse("M23 5.5V20c0 2.2-1.8 4-4 4h-7.3c-1.08 0-2.1-.43-2.85-1.19L1 14.83s1.26-1.23 1.3-1.25c.22-.19.49-.29.79-.29.22 0 .42.06.6.16.04.01 4.31 2.46 4.31 2.46V4c0-.83.67-1.5 1.5-1.5S11 3.17 11 4v7h1V1.5c0-.83.67-1.5 1.5-1.5S15 .67 15 1.5V11h1V2.5c0-.83.67-1.5 1.5-1.5s1.5.67 1.5 1.5V11h1V5.5c0-.83.67-1.5 1.5-1.5s1.5.67 1.5 1.5z");

    public static Avalonia.Media.Geometry Zoom { get; } = Parse("M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z");

    public static Avalonia.Media.Geometry Play { get; } = Parse("M8 5v14l11-7z");

    public static Avalonia.Media.Geometry VisualStudio { get; } = Parse("M 17.583 0.063 a 1.5 1.5 0 0 0 -1.032 0.392 a 1.5 1.5 0 0 0 -0.001 0 A 0.88 0.88 0 0 0 16.5 0.5 L 8.528 9.316 L 3.875 5.5 l -0.407 -0.35 a 1 1 0 0 0 -1.024 -0.154 a 1 1 0 0 0 -0.012 0.005 l -1.817 0.75 a 1 1 0 0 0 -0.077 0.036 a 1 1 0 0 0 -0.047 0.028 a 1 1 0 0 0 -0.038 0.022 a 1 1 0 0 0 -0.048 0.034 a 1 1 0 0 0 -0.03 0.024 a 1 1 0 0 0 -0.044 0.036 a 1 1 0 0 0 -0.036 0.033 a 1 1 0 0 0 -0.032 0.035 a 1 1 0 0 0 -0.033 0.038 a 1 1 0 0 0 -0.035 0.044 a 1 1 0 0 0 -0.024 0.034 a 1 1 0 0 0 -0.032 0.05 a 1 1 0 0 0 -0.02 0.035 a 1 1 0 0 0 -0.024 0.05 a 1 1 0 0 0 -0.02 0.045 a 1 1 0 0 0 -0.016 0.044 a 1 1 0 0 0 -0.016 0.047 a 1 1 0 0 0 -0.015 0.055 a 1 1 0 0 0 -0.01 0.04 a 1 1 0 0 0 -0.008 0.054 a 1 1 0 0 0 -0.006 0.05 A 1 1 0 0 0 0 6.668 v 10.666 a 1 1 0 0 0 0.615 0.917 l 1.817 0.764 a 1 1 0 0 0 1.035 -0.164 l 0.408 -0.35 l 4.653 -3.815 l 7.973 8.815 a 1.5 1.5 0 0 0 0.072 0.065 a 1.5 1.5 0 0 0 0.057 0.05 a 1.5 1.5 0 0 0 0.058 0.042 a 1.5 1.5 0 0 0 0.063 0.044 a 1.5 1.5 0 0 0 0.065 0.038 a 1.5 1.5 0 0 0 0.065 0.036 a 1.5 1.5 0 0 0 0.068 0.031 a 1.5 1.5 0 0 0 0.07 0.03 a 1.5 1.5 0 0 0 0.073 0.025 a 1.5 1.5 0 0 0 0.066 0.02 a 1.5 1.5 0 0 0 0.08 0.02 a 1.5 1.5 0 0 0 0.068 0.014 a 1.5 1.5 0 0 0 0.075 0.01 a 1.5 1.5 0 0 0 0.075 0.008 a 1.5 1.5 0 0 0 0.073 0.003 a 1.5 1.5 0 0 0 0.077 0 a 1.5 1.5 0 0 0 0.078 -0.005 a 1.5 1.5 0 0 0 0.067 -0.007 a 1.5 1.5 0 0 0 0.087 -0.015 a 1.5 1.5 0 0 0 0.06 -0.012 a 1.5 1.5 0 0 0 0.08 -0.022 a 1.5 1.5 0 0 0 0.068 -0.02 a 1.5 1.5 0 0 0 0.07 -0.028 a 1.5 1.5 0 0 0 0.09 -0.037 l 4.944 -2.377 a 1.5 1.5 0 0 0 0.476 -0.362 a 1.5 1.5 0 0 0 0.09 -0.112 a 1.5 1.5 0 0 0 0.004 -0.007 a 1.5 1.5 0 0 0 0.08 -0.125 a 1.5 1.5 0 0 0 0.062 -0.12 a 1.5 1.5 0 0 0 0.009 -0.017 a 1.5 1.5 0 0 0 0.04 -0.108 a 1.5 1.5 0 0 0 0.015 -0.037 a 1.5 1.5 0 0 0 0.03 -0.107 a 1.5 1.5 0 0 0 0.009 -0.037 a 1.5 1.5 0 0 0 0.017 -0.1 a 1.5 1.5 0 0 0 0.008 -0.05 a 1.5 1.5 0 0 0 0.006 -0.09 a 1.5 1.5 0 0 0 0.004 -0.08 V 3.942 a 1.5 1.5 0 0 0 0 -0.003 a 1.5 1.5 0 0 0 0 -0.032 a 1.5 1.5 0 0 0 -0.01 -0.15 a 1.5 1.5 0 0 0 -0.84 -1.17 L 18.206 0.21 a 1.5 1.5 0 0 0 -0.622 -0.146 z M 18 6.92 v 10.163 l -6.198 -5.08 z M 3 8.574 l 3.099 3.427 l -3.1 3.426 z");

    public static Avalonia.Media.Geometry Duplicate { get; } = Parse("M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z");

    public static Avalonia.Media.Geometry Delete { get; } = Parse("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z");

    public static Avalonia.Media.Geometry Save { get; } = Parse("M17 3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z");

    public static Avalonia.Media.Geometry SaveAssets { get; } = Parse("M21,12.4V7l-4-4H5C3.89,3,3,3.9,3,5v14c0,1.1,0.89,2,2,2h7.4L21,12.4z M15,15c0,1.66-1.34,3-3,3s-3-1.34-3-3s1.34-3,3-3 S15,13.34,15,15z M6,6h9v4H6V6z M19.99,16.25l1.77,1.77L16.77,23H15v-1.77L19.99,16.25z M23.25,16.51l-0.85,0.85l-1.77-1.77 l0.85-0.85c0.2-0.2,0.51-0.2,0.71,0l1.06,1.06C23.45,16,23.45,16.32,23.25,16.51z");

    public static Avalonia.Media.Geometry Discard { get; } = Parse("M19 6.41 17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z");

    public static Avalonia.Media.Geometry Bake { get; } = Parse("M9 21c0 .5.4 1 1 1h4c.6 0 1-.5 1-1v-1H9v1zm3-19C8.1 2 5 5.1 5 9c0 2.4 1.2 4.5 3 5.7V17c0 .5.4 1 1 1h6c.6 0 1-.5 1-1v-2.3c1.8-1.3 3-3.4 3-5.7 0-3.9-3.1-7-7-7z");

    public static Avalonia.Media.Geometry Materials { get; } = Parse("M12,2C6.49,2,2,6.49,2,12s4.49,10,10,10c1.38,0,2.5-1.12,2.5-2.5c0-0.61-0.23-1.2-0.64-1.67c-0.08-0.1-0.13-0.21-0.13-0.33 c0-0.28,0.22-0.5,0.5-0.5H16c3.31,0,6-2.69,6-6C22,6.04,17.51,2,12,2z M17.5,13c-0.83,0-1.5-0.67-1.5-1.5c0-0.83,0.67-1.5,1.5-1.5 s1.5,0.67,1.5,1.5C19,12.33,18.33,13,17.5,13z M14.5,9C13.67,9,13,8.33,13,7.5C13,6.67,13.67,6,14.5,6S16,6.67,16,7.5 C16,8.33,15.33,9,14.5,9z M5,11.5C5,10.67,5.67,10,6.5,10S8,10.67,8,11.5C8,12.33,7.33,13,6.5,13S5,12.33,5,11.5z M11,7.5 C11,8.33,10.33,9,9.5,9S8,8.33,8,7.5C8,6.67,8.67,6,9.5,6S11,6.67,11,7.5z");

    public static Avalonia.Media.Geometry GameView { get; } = Parse("M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z");

    public static Avalonia.Media.Geometry Add { get; } = Parse("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z");

    public static Avalonia.Media.Geometry Remove { get; } = Parse("M19 13H5v-2h14v2z");

    public static Avalonia.Media.Geometry Close { get; } = Parse("M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z");

    public static Avalonia.Media.Geometry Refresh => Rotate;

    public static Avalonia.Media.Geometry New { get; } = Parse("M19 3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-2 10h-4v4h-2v-4H7v-2h4V7h2v4h4v2z");

    public static Avalonia.Media.Geometry Rename { get; } = Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z");

    public static Avalonia.Media.Geometry Expand { get; } = Parse("M16.59 8.59L12 13.17 7.41 8.59 6 10l6 6 6-6z");

    public static Avalonia.Media.Geometry Collapse { get; } = Parse("M12 8l-6 6 1.41 1.41L12 10.83l4.59 4.58L18 14z");

    public static Avalonia.Media.Geometry Folder { get; } = Parse("M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z");

    public static Avalonia.Media.Geometry Scene { get; } = Parse("M14 6l-3.75 5 2.85 3.8-1.6 1.2C9.81 13.75 7 10 7 10l-6 8h22L14 6z");

    public static Avalonia.Media.Geometry Model { get; } = Parse("M18.25 7.6l-5.5-3.18c-.46-.27-1.04-.27-1.5 0L5.75 7.6c-.46.27-.75.76-.75 1.3v6.35c0 .54.29 1.03.75 1.3l5.5 3.18c.46.27 1.04.27 1.5 0l5.5-3.18c.46-.27.75-.76.75-1.3V8.9c0-.54-.29-1.03-.75-1.3zM7 14.96v-4.62l4 2.32v4.61l-4-2.31zm5-4.03L8 8.61l4-2.31 4 2.31-4 2.32zm1 6.34v-4.61l4-2.32v4.62l-4 2.31zM7 2H3.5C2.67 2 2 2.67 2 3.5V7h2V4h3V2zm10 0h3.5c.83 0 1.5.67 1.5 1.5V7h-2V4h-3V2zM7 22H3.5c-.83 0-1.5-.67-1.5-1.5V17h2v3h3v2zm10 0h3.5c.83 0 1.5-.67 1.5-1.5V17h-2v3h-3v2z");

    public static Avalonia.Media.Geometry Texture { get; } = Parse("M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z");

    public static Avalonia.Media.Geometry Canvas { get; } = Parse("M12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9c.83 0 1.5-.67 1.5-1.5 0-.39-.15-.74-.39-1.01-.23-.26-.38-.61-.38-.99 0-.83.67-1.5 1.5-1.5H16c2.76 0 5-2.24 5-5 0-4.42-4.03-8-9-8zm-5.5 9c-.83 0-1.5-.67-1.5-1.5S5.67 9 6.5 9 8 9.67 8 10.5 7.33 12 6.5 12zm3-4C8.67 8 8 7.33 8 6.5S8.67 5 9.5 5s1.5.67 1.5 1.5S10.33 8 9.5 8zm5 0c-.83 0-1.5-.67-1.5-1.5S13.67 5 14.5 5s1.5.67 1.5 1.5S15.33 8 14.5 8zm3 4c-.83 0-1.5-.67-1.5-1.5S16.67 9 17.5 9s1.5.67 1.5 1.5-.67 1.5-1.5 1.5z");

    public static Avalonia.Media.Geometry Mood { get; } = Parse("M6.76 4.84l-1.8-1.79-1.41 1.41 1.79 1.79 1.42-1.41zM4 10.5H1v2h3v-2zm9-9.95h-2V3.5h2V.55zm7.45 3.91l-1.41-1.41-1.79 1.79 1.41 1.41 1.79-1.79zm-3.21 13.7l1.79 1.8 1.41-1.41-1.8-1.79-1.4 1.4zM20 10.5v2h3v-2h-3zm-8-5c-3.31 0-6 2.69-6 6s2.69 6 6 6 6-2.69 6-6-2.69-6-6-6zm-1 16.95h2V19.5h-2v2.95zm-7.45-3.91l1.41 1.41 1.79-1.8-1.41-1.41-1.79 1.8z");

    public static Avalonia.Media.Geometry Shader { get; } = Parse("M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z");

    public static Avalonia.Media.Geometry Data { get; } = Parse("M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z");

    public static Avalonia.Media.Geometry Table { get; } = Parse("M10 10.02h5V21h-5zM17 21h3c1.1 0 2-.9 2-2v-9h-5v11zm3-18H5c-1.1 0-2 .9-2 2v3h19V5c0-1.1-.9-2-2-2zM3 19c0 1.1.9 2 2 2h3V10H3v9z");

    public static Avalonia.Media.Geometry Flow { get; } = Parse("M22 11V3h-7v3H9V3H2v8h7V8h2v10h4v3h7v-8h-7v3h-2V8h2v3z");

    public static Avalonia.Media.Geometry File { get; } = Parse("M6 2c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6H6zm7 7V3.5L18.5 9H13z");

    /// <summary>A four-pointed star, the editor's mark until it has a logo.</summary>
    public static Avalonia.Media.Geometry Logo { get; } = Parse("M12,1 C12.9,8 16,11.1 23,12 C16,12.9 12.9,16 12,23 C11.1,16 8,12.9 1,12 C8,11.1 11.1,8 12,1 Z");

    public static Avalonia.Media.Geometry Search => Zoom;

    public static Avalonia.Media.Geometry Camera { get; } = Parse("M17 10.5V7c0-.55-.45-1-1-1H4c-.55 0-1 .45-1 1v10c0 .55.45 1 1 1h12c.55 0 1-.45 1-1v-3.5l4 4v-11l-4 4z");

    public static Avalonia.Media.Geometry PointLight => Bake;

    public static Avalonia.Media.Geometry SpotLight { get; } = Parse("M6 14l3 3v5h6v-5l3-3V9H6zm5-12h2v3h-2zM3.5 5.88l1.41-1.41 2.12 2.12L5.62 8 3.5 5.88zm13.46.71l2.12-2.12 1.41 1.41L18.38 8l-1.42-1.41z");

    public static Avalonia.Media.Geometry Empty { get; } = Parse("M3 5v4h2V5h4V3H5c-1.1 0-2 .9-2 2zm2 10H3v4c0 1.1.9 2 2 2h4v-2H5v-4zm14 4h-4v2h4c1.1 0 2-.9 2-2v-4h-2v4zm0-16h-4v2h4v4h2V5c0-1.1-.9-2-2-2z");

    public static Avalonia.Media.Geometry Entity { get; } = Parse("M12 2l-5.5 9h11L12 2zm0 3.84L13.93 9h-3.87L12 5.84zM17.5 13c-2.49 0-4.5 2.01-4.5 4.5s2.01 4.5 4.5 4.5 4.5-2.01 4.5-4.5-2.01-4.5-4.5-4.5zm0 7c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5zM3 21.5h8v-8H3v8zm2-6h4v4H5v-4z");

    public static Avalonia.Media.Geometry Character { get; } = Parse("M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z");

    public static Avalonia.Media.Geometry FolderOpen { get; } = Parse("M20 6h-8l-2-2H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 12H4V8h16v10z");

    public static Avalonia.Media.Geometry NewFolder { get; } = Parse("M20 6h-8l-2-2H4c-1.11 0-1.99.89-1.99 2L2 18c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V8c0-1.11-.89-2-2-2zm-1 8h-3v3h-2v-3h-3v-2h3V9h2v3h3v2z");

    public static Avalonia.Media.Geometry Open { get; } = Parse("M19 19H5V5h7V3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2v-7h-2v7zM14 3v2h3.59l-9.83 9.83 1.41 1.41L19 6.41V10h2V3h-7z");

    public static Avalonia.Media.Geometry Up { get; } = Parse("M4 12l1.41 1.41L11 7.83V20h2V7.83l5.58 5.59L20 12l-8-8-8 8z");

    public static Avalonia.Media.Geometry Back { get; } = Parse("M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z");

    public static Avalonia.Media.Geometry Next { get; } = Parse("M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z");

    public static Avalonia.Media.Geometry Code { get; } = Parse("M4 7v2c0 .55-.45 1-1 1H2v4h1c.55 0 1 .45 1 1v2c0 1.65 1.35 3 3 3h3v-2H7c-.55 0-1-.45-1-1v-2c0-1.3-.84-2.42-2-2.83v-.34C5.16 11.42 6 10.3 6 9V7c0-.55.45-1 1-1h3V4H7C5.35 4 4 5.35 4 7zM21 10c-.55 0-1-.45-1-1V7c0-1.65-1.35-3-3-3h-3v2h3c.55 0 1 .45 1 1v2c0 1.3.84 2.42 2 2.83v.34c-1.16.41-2 1.52-2 2.83v2c0 .55-.45 1-1 1h-3v2h3c1.65 0 3-1.35 3-3v-2c0-.55.45-1 1-1h1v-4h-1z");

    public static Avalonia.Media.Geometry Import { get; } = Parse("M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z");

    public static Avalonia.Media.Geometry Reimport { get; } = Parse("M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z");

    public static Avalonia.Media.Geometry Undo { get; } = Parse("M12.5 8c-2.65 0-5.05.99-6.9 2.6L2 7v9h9l-3.62-3.62c1.39-1.16 3.16-1.88 5.12-1.88 3.54 0 6.55 2.31 7.6 5.5l2.37-.78C21.08 11.03 17.15 8 12.5 8z");

    public static Avalonia.Media.Geometry Redo { get; } = Parse("M18.4 10.6C16.55 8.99 14.15 8 11.5 8c-4.65 0-8.58 3.03-9.96 7.22L3.9 16c1.05-3.19 4.05-5.5 7.6-5.5 1.95 0 3.73.72 5.12 1.88L13 16h9V7l-3.6 3.6z");

    /// <summary>Shown in the editor's views: an open eye.</summary>
    public static Avalonia.Media.Geometry Visible { get; } = Parse("M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z");

    /// <summary>Hidden in the editor's views: a struck-through eye.</summary>
    public static Avalonia.Media.Geometry Invisible { get; } = Parse("M12 7c2.76 0 5 2.24 5 5 0 .65-.13 1.26-.36 1.83l2.92 2.92c1.51-1.26 2.7-2.89 3.43-4.75-1.73-4.39-6-7.5-11-7.5-1.4 0-2.74.25-3.98.7l2.16 2.16C10.74 7.13 11.35 7 12 7zM2 4.27l2.28 2.28.46.46C3.08 8.3 1.78 10.02 1 12c1.73 4.39 6 7.5 11 7.5 1.55 0 3.03-.3 4.38-.84l.42.42L19.73 22 21 20.73 3.27 3 2 4.27zM7.53 9.8l1.55 1.55c-.05.21-.08.43-.08.65 0 1.66 1.34 3 3 3 .22 0 .44-.03.65-.08l1.55 1.55c-.67.33-1.41.53-2.2.53-2.76 0-5-2.24-5-5 0-.79.2-1.53.53-2.2zm4.31-.78l3.15 3.15.02-.16c0-1.66-1.34-3-3-3l-.17.01z");

    /// <summary>Entities nested under another: a small tree.</summary>
    public static Avalonia.Media.Geometry Nested { get; } = Parse("M22 11V3h-7v3H9V3H2v8h7V8h2v10h4v3h7v-8h-7v3h-2V8h2v3z");

    /// <summary>Icons by name, for view models that name theirs without seeing Avalonia (menu choices, scene items).</summary>
    public static FuncValueConverter<string?, Avalonia.Media.Geometry?> ByName { get; } = new(Find);

    public static Avalonia.Media.Geometry? Find(string? name)
    {
        return name switch
        {
            nameof(Add) => Add,
            nameof(Visible) => Visible,
            nameof(Invisible) => Invisible,
            nameof(Nested) => Nested,
            nameof(Undo) => Undo,
            nameof(Redo) => Redo,
            nameof(Back) => Back,
            nameof(Bake) => Bake,
            nameof(Camera) => Camera,
            nameof(Character) => Character,
            nameof(Close) => Close,
            nameof(Code) => Code,
            nameof(Data) => Data,
            nameof(Delete) => Delete,
            nameof(Discard) => Discard,
            nameof(Duplicate) => Duplicate,
            nameof(Empty) => Empty,
            nameof(Entity) => Entity,
            nameof(File) => File,
            nameof(Flow) => Flow,
            nameof(Folder) => Folder,
            nameof(FolderOpen) => FolderOpen,
            nameof(Grid) => Grid,
            nameof(Import) => Import,
            nameof(Materials) => Materials,
            nameof(Model) => Model,
            nameof(Mood) => Mood,
            nameof(Canvas) => Canvas,
            nameof(New) => New,
            nameof(NewFolder) => NewFolder,
            nameof(Next) => Next,
            nameof(Open) => Open,
            nameof(Play) => Play,
            nameof(PointLight) => PointLight,
            nameof(Refresh) => Refresh,
            nameof(Reimport) => Reimport,
            nameof(Remove) => Remove,
            nameof(Rename) => Rename,
            nameof(Save) => Save,
            nameof(Scene) => Scene,
            nameof(Search) => Search,
            nameof(Shader) => Shader,
            nameof(SpotLight) => SpotLight,
            nameof(Table) => Table,
            nameof(Texture) => Texture,
            nameof(Up) => Up,
            _ => null,
        };
    }

    private static Avalonia.Media.Geometry Parse(string data) => StreamGeometry.Parse(data);
}
