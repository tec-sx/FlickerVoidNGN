# FlickerVoidNGN

A C# engine built for fast prototyping and easy development.

The target game is a third-person narrative RPG/adventure with an orthographic 2.5D look: 3D scenes with hand-drawn textures and a 2D feel. It is neither 2D-first nor photoreal. Lighting is the most important part of the look. The game is player-paced: no decay timers or forced timings.

Plan, decisions and roadmap: `Docs/Plan.md`.

## Stack

- .NET 10 LTS; NativeAOT for release builds, so runtime libraries set `IsAotCompatible` and avoid reflection (use source generators, such as System.Text.Json source generation).
- Silk.NET for windowing, input and graphics bindings. Vulkan 1.3 only, on Windows and Linux (Direct3D 12 was dropped: Vulkan has the RTX features, and one API halves the graphics code and its tests).
- HLSL shaders, compiled by DXC to SPIR-V. The `Shaders` module wraps DXC for the builder and for tests and depends on nothing; games never compile shaders, they load the builder's `.spv` files through `ShaderLibrary`.
- Arch for entities and components, BepuPhysics for physics, SharpGLTF for glTF.
`Source/Editor/Core/Icons`
- One codebase for Windows and Linux. When something is OS-specific, build it for Windows first and leave a `// TODO(Linux): ...` note on the Linux side.

## Layout

- `Source/Engine/<Module>/FlickerVoid.Engine.<Module>.csproj`, `Source/Editor/<Module>/FlickerVoid.Editor.<Module>.csproj` and `Source/Tools/<Module>/FlickerVoid.Tools.<Module>.csproj`, tests in the same folders under `Tests/` (`FlickerVoid.Engine.<Module>.Tests.csproj` and so on). Namespaces follow folders.
- Inside a module, folders group by what code is about: Graphics and Graphics.Vulkan by resource kind (`Devices`, `Buffers`, `Textures`, `Samplers`, `Pipelines`, `Commands`, `Presentation`, `Resources`/`Descriptors`); Rendering by feature (`Cameras`, `Meshes`, `Materials`, `Lighting`, `Shadows`, `GlobalIllumination`, `PostProcessing`, `Extraction`, `Pipeline`); Runtime and Platform likewise (`Loop`, `Windowing`, `Native`).
- Rendering never reads the Arch world while drawing: an `IRenderExtractor` (default `WorldExtractor`) copies it into a plain `RenderScene`, then `SceneRenderer` runs its `RenderPipeline` of `IRenderPass`es (Prepare all, write the frame data, Execute all) over a shared `RenderContext`. New rendering features are new passes added, inserted or replaced in the pipeline, not branches in `SceneRenderer`.
- Engine file types carry `fv` extensions: scenes `.fvscene`, materials `.fvmat`, lighting moods `.fvmood`, shader descriptions `.fvshader` (built shader parameters `.fvparams`), baked models `.fvmodel`, baked probes `.fvprobes`, projects `.fvproject`. Text ones are JSON with a `$schema`; standard formats keep theirs (`.glb`, `.png`, `.hlsl`, `.dds`, `.spv`).
`Source/Tools/Builder/FlickerVoid.Tools.Builder.targets`(`Source/Engine/<Module>/Shaders`)
- Content hot-reloads: `ContentWatcher` (a frame system) reports files that changed on disk to `IContentManager.NotifyChanged`, which forgets the cached JSON and raises `Changed` with the full path. Anything that caches what it loaded from a file (the mesh, texture and probe caches, `SceneLoader`, `LightingMoodDirector`) listens to `Changed` and drops or patches what it holds; the scene loader and mood director keep the last good version when an edited text file can't be read. In a development build the targets also write `Content/.content-build` (the builder and the source folders) and copy the build caches beside it; `ContentWatcher` then starts the builder in watch mode (`ContentBuildProcess`, `--watch`), so saving a source file rebuilds it into the game's content folder and the reload follows. Published games have neither file. Text content has JSON Schemas generated from the same source-generated metadata that reads it (`JsonSchemas`): modules add theirs to `Game.Schemas` (`scene` from every registered component, `mood` from Rendering), and a development run writes them into each source content folder's `.schemas` (`ContentWatcher`), which the builder skips. Content files name theirs with `"$schema": "../.schemas/scene.schema.json"`; root file types carry a `Schema` property for it.
- Modules arrive with their roadmap stage (`Docs/Plan.md`). Don't create empty projects, folders or classes ahead of their stage.
- Samples that show a stage's gate: `Samples/<Name>/FlickerVoid.Samples.<Name>.csproj`, in their own solution `Samples/FlickerVoid.Samples.slnx`.
- Dependencies only point down: Editor -> Rendering -> Graphics -> Runtime -> Gameplay; Physics and Platform (window and input) -> Runtime; `Hosting` -> Platform, Rendering and Graphics.Vulkan runs a game in a window (`SceneHost`: device, game with rendering, the built content, the `--scene`/`--mood`/`--frames`/`--validation` options read by `HostOptions`, and `SceneRenderSystem` drawing last with an optional `IFrameOverlay`), which the template's launcher and the samples use. `Gameplay` holds the engine-neutral rules (definitions, fragments, tags, facts, conditions, effects, scales) and depends on no other engine module; conditions read entities through `IEntityView`, which Runtime implements over Arch (`WorldEntityView`). Game builds never contain editor code. `Debug` (Dear ImGui panels for development: engine stats and rendering switches, and `DebugWindow` for a project's own panels, as in Cog) depends on Hosting, and `DebugUi` is a frame overlay; samples and projects reference it and show it with `--debug` or F1, and release builds leave it out.
- Users' projects live in `Documents/FlickerVoid/<Project>`. The demo project is FlickerVoid, in this repo at `Projects/FlickerVoid`: `Source/<Module>/FlickerVoid.<Module>.csproj`, tests in `Tests/<Module>/FlickerVoid.<Module>.Tests.csproj`, folders following the ontology tree (`Gameplay/Protagonist/Identity/Social`). Game-specific rules (social, quests) live there, not in the engine.
- The engine solution `FlickerVoidNGN.slnx` holds only engine modules and their tests. Each project is its own solution (`<Project>.slnx`) with an Engine solution folder listing the engine's module projects (references, not copies), so engine classes can be seen and edited from the project and the changes land in the engine repository. A project finds the engine through `$(FlickerVoidEngine)`, set by its `Directory.Build.props` from the `FLICKERVOID_ENGINE` environment variable (falling back to the repository the project sits in), and imports the engine's `Directory.Build.props` and `Directory.Packages.props`.
- A project is opened in the editor through its `<Name>.fvproject` file (JSON: `game`, the launcher's C# project; `startupScene`), like a `.uproject`: the editor (`FlickerVoid.Engine.Editor <file>.fvproject`) builds the game first (`EditorProject`, `DotnetProjectBuilder`), which builds its content, then opens the built content and the startup scene; the build manifest beside the content names the source folders, which the asset browser lists and every save writes, while `ContentWatcher` runs the builder over them in watch mode (`EditorScene.Poll`). The template's launcher lists the project's `Content` folder. Play (F5, `ScenePlayer`) runs the project's built game (`EditorProject.Program`) on the open scene as the world is now, without saving it: the world is written to `.play/<Scene>.fvscene` in the built content folder and the game gets `--scene` (`ProcessGameLauncher`); the game's watcher sees the editor's builder already holds the manifest (`ContentWatchLock`), so only one builder runs. Started without a project or content folder, the editor shows the project picker (`ProjectPickerWindow`): the projects in `Documents/FlickerVoid`, Browse for a `.fvproject`, or Create a new one from the template (`TemplateProjectCreator`, which installs it from `FLICKERVOID_ENGINE`); the build runs off the UI thread and the editor window replaces the picker.
- New projects come from the `dotnet new` template in `Templates/Project` (`dotnet new install ./Templates/Project`, then `dotnet new flickervoid -n <Name> --engine <engine folder>`); the editor creates projects through the same template. `--engine` only fills the solution's Engine folder; builds find the engine through `FLICKERVOID_ENGINE`, with no fallback in the project's build files; the editor (`DotnetCli`) uses the variable or else the engine folder it runs from, and sets it on every `dotnet` it starts. Keep the template in step with `Projects/FlickerVoid`'s build files and the engine's module list.

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
- CI runs both on Windows and Linux for every pull request, creates and runs a project from the template, then publishes the samples with NativeAOT and runs them (the window sample on SDL's dummy video driver, the textured mesh and scene viewer samples on lavapipe, under Xvfb on Linux). Arch components must be registered with `ArrayRegistry.Add<T>()` (scene components are, through `SceneSpawner.Register`), or NativeAOT builds fail when the first entity gets them. Cloud sessions can build and test on Linux. The editor viewport (Avalonia GPU interop) needs a real GPU: lavapipe exports no semaphores, so it is tested on the owner's PC.
- Graphics tests render on whatever GPU is present and skip when none is. In CI they run on lavapipe, the software Vulkan driver: on Linux from `mesa-vulkan-drivers` plus `vulkan-validationlayers`, on Windows through `jakoch/install-vulkan-sdk-action`, with `LP_PERF=no_rast_linear`, because llvmpipe's linear rasterizer reads push constants past 16 bytes as zero, and on Linux under `xvfb-run` so the swapchain tests get a window.
- Unit tests use xUnit v3, NSubstitute for mocking and AwesomeAssertions (the Apache-2.0 fork of FluentAssertions) for assertions; test projects named `*.Tests` get all three from `Directory.Build.props`. The class under test is created by a builder in a separate `<Class>TestsBuilder.cs` file in the same folder (`GameTestsBuilder.cs` for `GameTests.cs`).
- Work is done TDD. Tests are written first, and serve as the Design Spec for the code.

## Versions

- The engine's version is `Version` in `Source/Directory.Build.props`: `0.<roadmap stage>.<patch>`. Bump it in the pull request that completes a stage (minor) or fixes a released one (patch). When main gets a version without a tag, CI tags the commit `v<Version>` and packs the engine modules as NuGet packages (a workflow artifact).
- Projects reference the engine's projects, not its packages, so every project build compiles the current engine. Packages are for an installed engine without source.

## Git

- Branches: `feature/<title>`, `improvement/<title>` or `fix/<title>`.
- PRs open ready for review; the owner reviews and may push to the branch.
