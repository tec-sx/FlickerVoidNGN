# FlickerVoidNGN

FlickerVoid Engine: a C# game engine for narrative games with an orthographic 2.5D look, built on .NET 10, Direct3D 12 and Vulkan, with an Avalonia editor for Windows and Linux.

It grows alongside the Unreal project [FlickerVoid](https://github.com/tec-sx/FlickerVoid), whose gameplay it ports. See [Docs/Plan.md](Docs/Plan.md) for the decisions and the roadmap.

## Build

Requires the .NET 10 SDK.

```
dotnet build FlickerVoidNGN.slnx
dotnet test --solution FlickerVoidNGN.slnx
```

Projects and samples are solutions of their own that reference the engine:

```
dotnet build Projects/FlickerVoid/FlickerVoid.slnx
dotnet build Samples/FlickerVoid.Samples.slnx
```

A project outside this repository finds the engine through the `FLICKERVOID_ENGINE` environment variable. On Windows, `setx FLICKERVOID_ENGINE C:\path\to\FlickerVoidNGN` sets it for new terminals and IDEs.

## New projects

Set `FLICKERVOID_ENGINE` to the engine repository's folder, then create a project from the template (project names are C# identifiers, without spaces):

```
dotnet new install ./Templates/Project
dotnet new flickervoid -n MyGame -o ~/Documents/FlickerVoid/MyGame
dotnet build ~/Documents/FlickerVoid/MyGame/MyGame.slnx
```

The project's solution holds only the project's own modules. It builds the engine modules it references from the folder `FLICKERVOID_ENGINE` names.
