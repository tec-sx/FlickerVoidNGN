# Third-party notices

FlickerVoid Engine includes code adapted from the open-source projects below. Each adapted file keeps its original copyright header.

## Stride

https://github.com/stride3d/stride

Used in: `Source/Runtime/GameLoop.cs` (stepping rules), `Source/Runtime/Services/ServiceRegistry.cs`, `Source/Physics/PoseIntegratorCallbacks.cs` and `Source/Physics/NarrowPhaseCallbacks.cs` (from Stride.BepuPhysics), `Source/Graphics.Direct3D12/D3D12Runtime.cs` (Agility SDK loading).

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

## DirectX Agility SDK

https://www.nuget.org/packages/Microsoft.Direct3D.D3D12

`D3D12Core.dll` and `d3d12SDKLayers.dll` are copied from the package to `D3D12\` next to apps that use the Direct3D 12 backend. Both are listed as distributable in the package's Microsoft Software License Terms (`LICENSE.txt` in the package).
