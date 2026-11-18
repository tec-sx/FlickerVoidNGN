# FlickerVoidNGN

A C# engine built for fast prototyping and easy development.

The target game is a third-person narrative RPG/adventure with an orthographic 2.5D look: 3D scenes with hand-drawn textures and a 2D feel. It is neither 2D-first nor photoreal. Lighting is the most important part of the look. The game is player-paced: no decay timers or forced timings.

Plan, decisions and roadmap: `Docs/Plan.md`.

## Stack

- .NET 10 LTS; NativeAOT for release builds, so runtime libraries set `IsAotCompatible` and avoid reflection (use source generators, such as System.Text.Json source generation).
- Silk.NET for windowing, input and graphics bindings. Direct3D 12 and Vulkan only. Direct3D 12 runs on the Agility SDK (copied to `D3D12\` next to the app) with enhanced barriers.
- HLSL shaders, compiled by DXC to DXIL and SPIR-V. The `Shaders` module compiles them for tools, tests and samples and depends on nothing; games load compiled bytecode.
- Arch for entities and components, BepuPhysics for physics, SharpGLTF for glTF.
- Avalonia for the editor, with its GPU interop for the viewport.
- One codebase for Windows and Linux. When something is OS-specific, build it for Windows first and leave a `// TODO(Linux): ...` note on the Linux side.

## Layout

- `Source/<Module>/FlickerVoid.Engine.<Module>.csproj`, tests in `Tests/<Module>/FlickerVoid.Engine.<Module>.Tests.csproj`. Namespaces follow folders.
- Modules arrive with their roadmap stage (`Docs/Plan.md`). Don't create empty projects, folders or classes ahead of their stage.
- Samples that show a stage's gate: `Samples/<Name>/FlickerVoid.Samples.<Name>.csproj`, in their own solution `Samples/FlickerVoid.Samples.slnx`.
- Dependencies only point down: Editor -> Rendering -> Graphics -> Runtime; Physics and Platform (window and input) -> Runtime. `Gameplay` holds the engine-neutral rules (definitions, fragments, tags, facts, conditions, effects, scales) and depends on no other engine module. Game builds never contain editor code.
- Users' projects live in `Documents/FlickerVoid/<Project>`. The demo project is FlickerVoid, in this repo at `Projects/FlickerVoid`: `Source/<Module>/FlickerVoid.<Module>.csproj`, tests in `Tests/<Module>/FlickerVoid.<Module>.Tests.csproj`, folders following the ontology tree (`Gameplay/Protagonist/Identity/Social`). Game-specific rules (social, quests) live there, not in the engine.
- The engine solution `FlickerVoidNGN.slnx` holds only engine modules and their tests. Each project is its own solution (`<Project>.slnx`) with an Engine solution folder listing the engine's module projects (references, not copies), so engine classes can be seen and edited from the project and the changes land in the engine repository. A project finds the engine through `$(FlickerVoidEngine)`, set by its `Directory.Build.props` from the `FLICKERVOID_ENGINE` environment variable (falling back to the repository the project sits in), and imports the engine's `Directory.Build.props` and `Directory.Packages.props`.
- New projects come from the `dotnet new` template in `Templates/Project` (`dotnet new install ./Templates/Project`, then `dotnet new flickervoid -n <Name> --engine <engine folder>`); the editor will create projects through the same template. `--engine` only fills the solution's Engine folder; builds find the engine through `FLICKERVOID_ENGINE`, with no fallback. Keep the template in step with `Projects/FlickerVoid`'s build files and the engine's module list.

## Porting from Unreal

- Read the Unreal source (FVFramework, FVGameplay and the plugins) before porting, and port its behaviour and its tests. Never write an API from memory.
- C# types drop the Unreal prefixes (`EFVCompare` -> `Compare`, `UFVCondition` -> `Condition`). Unreal's instanced UObjects (fragments, conditions, effects) become plain C# classes serialized polymorphically.
- The ontology tree (Game -> Interface, Gameplay, Presentation, three children per node) and the rule of three carry over: at most 3 of anything the player sees (HUD sections, hub pages, menu entries, dialogue choices plus cancel).

## Reuse and licences

- Find the root cause, design and plan before adding code. Adding code is not always the answer; prefer removing and simplifying. No helper for every inconvenience.
- Reuse .NET, the libraries above and open-source projects before writing our own. Stride (MIT) is the main source: copy and adapt its code where it fits, keep its copyright header in copied files and list it in `THIRD-PARTY-NOTICES.md`.

## Conventions

- Idiomatic C#: file-scoped namespaces, one main type per file named after it.
- Components talk through C# events on components found on the entity.  no string-addressed messages except across worlds or for deferred calls.
- Data-driven, composition over inheritance, SOLID/KISS/YAGNI, open/closed.
- Interface what is worth interfacing: services and the dependencies tests substitute (`IFactDatabase`).
- Don't nest calls: store a call's result in a variable before passing it to another call. Only simple getters and properties may be passed inline.
- Don't chain ternaries; use `if`/`else` or a `switch`.
- Minimal comments: a one-sentence `<summary>` on a type; on a member only when its name or logic is unclear.
- Warnings are errors.

## Build and test

- `dotnet build FlickerVoidNGN.slnx` and `dotnet test --solution FlickerVoidNGN.slnx` (Microsoft Testing Platform, xUnit v3); the same for `Projects/FlickerVoid/FlickerVoid.slnx` (run from its folder, which has its own `global.json`) and `Samples/FlickerVoid.Samples.slnx`.
- CI runs both on Windows and Linux for every pull request, creates and runs a project from the template, then publishes the samples with NativeAOT and runs them (the window sample on SDL's dummy video driver, the textured mesh sample on WARP and on lavapipe under Xvfb). Arch components must be registered with `ArrayRegistry.Add<T>()` (scene components are, through `SceneSpawner.Register`), or NativeAOT builds fail when the first entity gets them. Cloud sessions can build and test on Linux; Direct3D 12 work is tested on the owner's Windows PC.
- Graphics tests render on whatever GPU is present and skip when none is. On Windows CI the Direct3D 12 tests run on WARP, the software rasterizer. On Linux they run on lavapipe (`mesa-vulkan-drivers`, plus `vulkan-validationlayers`) with `LP_PERF=no_rast_linear`, because llvmpipe's linear rasterizer reads push constants past 16 bytes as zero, and under `xvfb-run` so the swapchain tests get a window.
- Unit tests use xUnit v3, NSubstitute for mocking and AwesomeAssertions (the Apache-2.0 fork of FluentAssertions) for assertions; test projects named `*.Tests` get all three from `Directory.Build.props`. The class under test is created by a builder in a separate `<Class>TestsBuilder.cs` file in the same folder (`GameTestsBuilder.cs` for `GameTests.cs`).
- Work is done TDD. Tests are written first, and serve as the Design Spec for the code.

## Versions

- The engine's version is `Version` in `Source/Directory.Build.props`: `0.<roadmap stage>.<patch>`. Bump it in the pull request that completes a stage (minor) or fixes a released one (patch). When main gets a version without a tag, CI tags the commit `v<Version>` and packs the engine modules as NuGet packages (a workflow artifact).
- Projects reference the engine's projects, not its packages, so every project build compiles the current engine. Packages are for an installed engine without source.

## Git

- Branches: `feature/<title>`, `improvement/<title>` or `fix/<title>`.
- PRs open ready for review; the owner reviews and may push to the branch.
