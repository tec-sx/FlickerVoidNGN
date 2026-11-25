# FlickerVoidNGN

A C# engine built for fast prototyping and easy development.

The target game is a third-person narrative RPG/adventure with an orthographic 2.5D look: 3D scenes with hand-drawn textures and a 2D feel. It is neither 2D-first nor photoreal. Lighting is the most important part of the look. The game is player-paced: no decay timers or forced timings.

Plan, decisions and roadmap: `Docs/Plan.md`.

## Stack

- .NET 10 LTS; NativeAOT for release builds, so runtime libraries set `IsAotCompatible` and avoid reflection (use source generators, such as System.Text.Json source generation).
- Silk.NET for windowing, input and graphics bindings. Vulkan 1.3 only, on Windows and Linux (Direct3D 12 was dropped: Vulkan has the RTX features, and one API halves the graphics code and its tests).
- HLSL shaders, compiled by DXC to SPIR-V. The `ShaderCompiler` tool (`Source/Tools/ShaderCompiler`) wraps DXC for the builder and for tests and depends on nothing; games never compile shaders, they load the builder's `.spv` files through `ShaderLibrary`.
- Arch for entities and components, BepuPhysics for physics, SharpGLTF for glTF.
- Avalonia for the editor, with its GPU interop for the viewport: four projects in `Source/Editor` (`Shell`, the program and the only one using Avalonia; `Core`, `Assets` and `Persistence`, which run headless in tests), MVVM on CommunityToolkit.Mvvm (`Docs/Modules/Editor.md`).
- One codebase for Windows and Linux. When something is OS-specific, build it for Windows first and leave a `// TODO(Linux): ...` note on the Linux side.

## Layout

- `Source/Engine/<Module>/FlickerVoid.Engine.<Module>.csproj`, `Source/Editor/<Module>/FlickerVoid.Editor.<Module>.csproj` and `Source/Tools/<Module>/FlickerVoid.Tools.<Module>.csproj`, tests in the same folders under `Tests/` (`FlickerVoid.Engine.<Module>.Tests.csproj` and so on). Namespaces follow folders.
- Inside a module, folders group by what code is about: Graphics and Graphics.Vulkan by resource kind (`Devices`, `Buffers`, `Textures`, `Samplers`, `Pipelines`, `Commands`, `Presentation`, `Resources`/`Descriptors`); Rendering by feature (`Cameras`, `Meshes`, `Materials`, `Lighting`, `Shadows`, `GlobalIllumination`, `PostProcessing`, `Sky`, `Outlines`, `Extraction`, `Pipeline`); Runtime and Platform likewise (`Loop`, `Windowing`, `Native`).
- Engine file types carry `fv` extensions: scenes `.fvscene`, materials `.fvmat`, lighting moods `.fvmood`, canvases `.fvcanvas`, texture settings `.fvtexture`, shader descriptions `.fvshader` (built shader parameters `.fvparams`), baked models `.fvmodel`, baked probes `.fvprobes`, entity templates `.fventity`, data assets `.fvdata`, data tables `.fvtable`, flow graphs `.fvflow`, projects `.fvproject`. Text ones are JSON with a `$schema`; standard formats keep theirs (`.glb`, `.png`, `.hlsl`, `.dds`, `.spv`).
- Modules arrive with their roadmap stage (`Docs/Plan.md`). Don't create empty projects, folders or classes ahead of their stage.
- Samples that show a stage's gate: `Samples/<Name>/FlickerVoid.Samples.<Name>.csproj`, in their own solution `Samples/FlickerVoid.Samples.slnx`.
- Dependencies only point down: Editor -> Rendering -> Graphics -> Runtime -> Gameplay; inside the editor, Shell -> Assets, Persistence and Core, Assets -> Core, and Core -> Rendering, Cameras and Vehicles, while Persistence references nothing; Tools' Builder -> ShaderCompiler and Rendering, and Generators references nothing of the engine (netstandard2.0, as Roslyn needs). Engine source projects never reference Tools or Editor; engine tests may (`Tests/Engine/Graphics` compiles shaders with the ShaderCompiler tool); Physics, Creations and Platform (window and input) -> Runtime; Vehicles -> Creations and Rendering; `Hosting` -> Platform, Rendering and Graphics.Vulkan runs a game in a window (`SceneHost`: device, game with rendering, the built content (also its `IContentManager` service), the `--scene`/`--mood`/`--frames`/`--saves`/`--validation`/`--debug` options read by `HostOptions`, and `SceneRenderSystem` drawing last with an optional `IFrameOverlay` and closing the window on Escape unless the game turns `CloseOnEscape` off; without `--mood` it draws with the scene's `SceneMood`), which the template's launcher and the samples use. `Gameplay` holds the engine-neutral rules (definitions, fragments, tags, facts, conditions, effects, scales) and depends on no other engine module; conditions read entities through `IEntityView`, which Runtime implements over Arch (`WorldEntityView`). Game builds never contain editor code. `Debug` (Dear ImGui panels for development: engine stats and rendering switches, and `DebugWindow` for a project's own panels, as in Cog) depends on Hosting, and `DebugUi` is a frame overlay; samples and projects reference it and show it with `--debug` or F1, and release builds leave it out.
- Benchmarks of the engine's hot code paths (BenchmarkDotNet) are in `Benchmarks/FlickerVoid.Engine.Benchmarks.csproj`, one class per path, grouped by module (`Rendering/WorldExtractorBenchmarks.cs`). Run them with `dotnet run --project Benchmarks -c Release -- --filter '*'`; `--profiler EP` adds a CPU trace of each benchmark (`.speedscope.json`, opened in speedscope.app) showing where the time goes. The Benchmarks workflow runs them on every push and pull request: a results table, a comparison with main and charts (`.github/scripts/BenchmarkCharts.cs`, Mermaid: this run per benchmark, and main's history) in the run's summary, the traces as an artifact, and a commit comment when a benchmark gets 50% slower than main.

## Modules

Each module's behaviour, API and decisions are in `Docs/Modules`; read the one you work on first, and keep it in step with the code in the same pull request.

- `Editor.md`: the editor's projects, MVVM, viewport, outliner, inspector, undo, unsaved content, asset browser and the material, texture and flow editors.
- `Rendering.md`: extraction, the pass pipeline, culling, the sky, outlines and outline requests, indoors, cutaways and doorways.
- `Content.md`: the builder and its importers, textures, canvases, models and shaders, hot reload, schemas and the content check.
- `Scenes.md`: greyboxes and solids, entity templates and classes, data assets and tables, scene travel, names and references, the game loop.
- `Input.md`: input maps, actions, contexts and captures.
- `Characters.md`: point and click movement, bodies, paths and perception.
- `Cameras.md`: shots, the director, layers, triggers, cues, scripting, the depth fade and cutaways.
- `Interaction.md`, `Maps.md`, `Inventory.md`, `Attributes.md` (attributes and abilities), `Flow.md`, `Story.md` (quests, knowledge and dialogue), `Saves.md` and `Ui.md` (the game's interface and stages).
- `Creations.md`: the parts of creations: kinds, spawning under a placed entity and patching by part id.
- `Projects.md`: users' projects, the solutions, `.fvproject` files, the editor's project picker and splash, Compile and Play, and the template.
- `Vehicles.md`: vehicle files, their parts and the wheel rules, and vehicles placed in levels.

## Porting from Unreal

- Read the Unreal source (FVFramework, FVGameplay and the plugins) before porting, and port its behaviour and its tests. Never write an API from memory.
- C# types drop the Unreal prefixes (`EFVCompare` -> `Compare`, `UFVCondition` -> `Condition`). Unreal's instanced UObjects (fragments, conditions, effects) become plain C# classes serialized polymorphically.
- The ontology tree (Game -> Interface, Gameplay, Presentation, three children per node) and the rule of three carry over: at most 3 of anything the player sees (HUD sections, hub pages, menu entries, dialogue choices plus cancel).

## Reuse and licences

- Find the root cause, design and plan before adding code. Adding code is not always the answer; prefer removing and simplifying. No helper for every inconvenience.
- Reuse .NET, the libraries above and open-source projects before writing our own. Stride (MIT) is the main source: copy and adapt its code where it fits, keep its copyright header in copied files and list it in `THIRD-PARTY-NOTICES.md`.

## Conventions

- Idiomatic C#: file-scoped namespaces, one main type per file named after it.
- Entities are Arch-first (owner, 2026-11-20): components are plain data structs, behaviour lives in systems, one per feature. Systems add lifecycle hooks for their components to `EntityLifecycle` (`Game.Lifecycle`: attach runs in the editor and the game, for looks from data; activate only while playing; deactivate and detach in reverse), and talk through events, plain structs sent to and handled on `Game.Events` (`EventBus`, dispatched after each group of systems). No string-addressed messages except across worlds or for deferred calls.
- Data-driven, composition over inheritance, SOLID/KISS/YAGNI, open/closed.
- Interface what is worth interfacing: services and the dependencies tests substitute (`IFactDatabase`).
- Don't nest calls: store a call's result in a variable before passing it to another call. Only simple getters and properties may be passed inline.
- Don't chain ternaries; use `if`/`else` or a `switch`.
- Minimal comments: a one-sentence `<summary>` on a type; on a member only when its name or logic is unclear.
- Warnings are errors.

## Build and test

- `dotnet build FlickerVoidNGN.slnx` and `dotnet test --solution FlickerVoidNGN.slnx` (Microsoft Testing Platform, xUnit v3); the same for `Samples/FlickerVoid.Samples.slnx`.
- CI runs both on Windows and Linux for every pull request, creates and runs a project from the template, then publishes the samples with NativeAOT and runs them (the window sample on SDL's dummy video driver, the textured mesh and scene viewer samples on lavapipe, under Xvfb on Linux). Its `game` job builds and tests the game (`tec-sx/Bleeders`) against the engine being tested: the game's branch of the same name when it has one, so a change spanning both is tested together, otherwise its main. Arch components must be registered with `ArrayRegistry.Add<T>()` (scene components are, through `SceneSpawner.Register`), or NativeAOT builds fail when the first entity gets them. Cloud sessions can build and test on Linux. The editor viewport (Avalonia GPU interop) needs a real GPU: lavapipe exports no semaphores, so it is tested on the owner's PC.
- Graphics tests render on whatever GPU is present and skip when none is. In CI they run on lavapipe, the software Vulkan driver: on Linux from `mesa-vulkan-drivers` plus `vulkan-validationlayers`, on Windows through `jakoch/install-vulkan-sdk-action`, with `LP_PERF=no_rast_linear`, because llvmpipe's linear rasterizer reads push constants past 16 bytes as zero, and on Linux under `xvfb-run` so the swapchain tests get a window.
- Unit tests use xUnit v3, NSubstitute for mocking and AwesomeAssertions (the Apache-2.0 fork of FluentAssertions) for assertions; test projects named `*.Tests` get all three from `Directory.Build.props`. The class under test is created by a builder in a separate `<Class>TestsBuilder.cs` file in the same folder (`GameTestsBuilder.cs` for `GameTests.cs`).
- Work is done TDD. Tests are written first, and serve as the Design Spec for the code.

## Versions

- The engine's version is `Version` in `Source/Directory.Build.props`: `0.<roadmap stage>.<patch>`. Bump it in the pull request that completes a stage (minor) or fixes a released one (patch). When main gets a version without a tag, CI tags the commit `v<Version>` and packs the engine modules as NuGet packages (a workflow artifact).
- Projects reference the engine's projects, not its packages, so every project build compiles the current engine. Packages are for an installed engine without source.

## Git

- Branches: `feature/<title>`, `improvement/<title>` or `fix/<title>`.
- PRs open ready for review; the owner reviews and may push to the branch.
