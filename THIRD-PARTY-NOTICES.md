# Third-party notices

FlickerVoid Engine includes code adapted from the open-source projects below. Each adapted file keeps its original copyright header.

## Stride

https://github.com/stride3d/stride

Used in: `Source/Runtime/GameLoop.cs` (stepping rules) and `Source/Runtime/Services/ServiceRegistry.cs`

```
The MIT License (MIT)

Copyright (c) 2021 .NET Foundation and Contributors (https://dotnetfoundation.org/)
Copyright (c) 2018-2021 Stride contributors (https://stride3d.net)
Copyright (c) 2011-2018 Silicon Studio Corp. (https://www.siliconstudio.co.jp)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Stride's `ServiceRegistry` carries a further notice from SharpDX (Copyright (c) 2010-2013 SharpDX - Alexandre Mutel, MIT), kept in the adapted file.

## Fox (glTF Sample Assets)

https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/Fox

Used in: `Samples/SceneViewer/Content/Fox.glb`, unchanged.

- Model by PixelMannen, [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/legalcode).
- Rigging and animation by tomkranis on Sketchfab, [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/legalcode).
- glTF conversion by AsoboStudio and scurest, [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/legalcode).

## Poly Haven HDRIs

https://polyhaven.com (`quarry_01`, `moonless_golf` and `venice_sunset`, the 1k HDRIs as mirrored in three.js's examples)

Used in: `Source/Engine/Rendering/Shaders/Reflections/*.jpg` (Quarry, Moonless Golf, Venice Sunset), tonemapped to 1024 by 512 JPEGs, which moods name as the world glossy surfaces reflect (`LightingMood.Reflection`).

Released under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/legalcode) (public domain).

## Material Symbols (Google Material Design Icons)

https://github.com/google/material-design-icons

Used in: `Source/Editor/Icons/*.png`, the Rounded filled glyphs `lightbulb`, `highlight`, `videocam` and `blur_on`, rasterised to 64 px white PNGs.

Licensed under the [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0), Copyright Google LLC.

## Lucide

https://lucide.dev (v1.81.0)

Used in: `Source/Editor/Shell/LucideIcons.cs` (the editor's icons, their paths copied by `icons.py` from `lucide-static`). Lucide is licensed under the [ISC License](https://github.com/lucide-icons/lucide/blob/main/LICENSE), Copyright Lucide Contributors; the icons it took from Feather are MIT, Copyright Cole Bemis.

## Avalonia

https://github.com/AvaloniaUI/Avalonia (12.1.6)

Used in: `Source/Editor/Shell/Themes/EditorTheme.axaml` (the window decorations' control theme, adapted from the Fluent theme's so no title is drawn over the top bar). Avalonia is licensed under the [MIT License](https://github.com/AvaloniaUI/Avalonia/blob/master/licence.md), Copyright The Avalonia Project.

## Inter

https://rsms.me/inter (through the `Avalonia.Fonts.Inter` package, MIT)

Used in: the editor's text (`Source/Editor/Shell/Program.cs`, `WithInterFont`). Inter is licensed under the [SIL Open Font License 1.1](https://openfontlicense.org), Copyright the Inter Project Authors.

## Simple Icons

https://simpleicons.org (v9.21.0)

Used in: `Source/Editor/Shell/LucideIcons.cs` (the Visual Studio logo of the editor's Open in Visual Studio button). Simple Icons are released under CC0 1.0 Universal; the logo is a trademark of Microsoft, used only to name the program the button opens.

## NVIDIA DLSS SDK

https://github.com/NVIDIA/DLSS (v310.9.16)

Used in: `Source/Native/Dlss` (the engine's DLSS library links the SDK's NGX static library, and NVIDIA's DLSS and Ray Reconstruction runtimes are copied beside programs). Nothing of the SDK is in this repository: the library's build scripts fetch it under the NVIDIA RTX SDKs license (the SDK's `LICENSE.txt`). A game that ships with DLSS must attribute it and show the NVIDIA marks on its splash screen, in its about box and in its credits (the license's exhibit, section 7.1 (b)).

## Manifold

https://github.com/elalish/manifold (v3.5.5)

Used in: `Source/Native/Manifold` and `Source/Editor/Core/Meshes/Booleans` (the editor's mesh Booleans call its C binding). Nothing of it is in this repository: the build scripts fetch and build it. Licensed under the [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0), Copyright The Manifold Authors.

## NVorbis

`Source/Engine/Audio` decodes Ogg Vorbis files through the NVorbis package.

- Source: https://github.com/NVorbis/NVorbis
- Licence: MIT
- Copyright (c) Andrew Ward

## Steam Audio

https://github.com/ValveSoftware/steam-audio (v4.6.1), Copyright (c) Valve Corporation

Used in: `Source/Engine/Audio/Spatial` (a thin binding to its C API for head-related 3D sound) and `Source/Native/SteamAudio` (the build script fetches Valve's release and copies `phonon` beside programs). Nothing of it is in this repository. Licence: Apache 2.0.

