# FlickerVoid Engine plan

Decided with the owner on 2026-11-18. Each roadmap stage ends in something that can be run and judged before the next one starts.

## Decisions

| Topic | Decision |
| --- | --- |
| Foundation | Own engine on open-source libraries: Silk.NET, BepuPhysics, Arch, SharpGLTF. Stride and MonoGame are references. |
| Stride code | Copy and adapt its MIT Vulkan backend and light shaders, keeping the licence notice |
| Look | Orthographic 2.5D: 3D scenes, hand-drawn textures, a 2D feel. Not 2D-first, not photoreal. Lighting first. |
| Indirect light | Baked probes first, real-time later |
| .NET | .NET 10 LTS, NativeAOT for release builds |
| Graphics | Vulkan 1.3 only, on Windows and Linux. Direct3D 12 was dropped after stage 3: every RTX feature (DLSS frame generation and ray reconstruction, Mega Geometry, shader execution reordering, cooperative vectors, RTX IO) is on Vulkan, and what D3D12 adds (work graphs, sampler feedback, Xbox) isn't in the plan |
| Shaders | HLSL, compiled by DXC to SPIR-V |
| Entities | Arch (archetype ECS, NativeAOT friendly) |
| Asset text | JSON through System.Text.Json source generation |
| Editor | Avalonia on Windows and Linux, on Avalonia's Vulkan backend: the viewport shares a Vulkan texture and semaphores with it through Avalonia's GPU interop (`Samples/EditorViewport`) |
| Platforms | One codebase for Windows and Linux; OS-specific work for Windows first, with a TODO on the Linux side |
| Gameplay | The Unreal project is the reference; NGN ports from it |
| Projects | `Documents/FlickerVoid/<Project>`; the demo project is FlickerVoid |

## Architecture

- Runtime, builder and editor are separate programs that share asset schemas. The runtime never references the editor.
- Assets are text on disk (diffable) and binary when built. The builder caches by content hash and runs the same in the editor and in CI.
- Entities and components on Arch; systems update one component type at a time.
- Scenes and spawning go through scene files and factories; scene proxies load and unload whole worlds.
- Game code is plain C#. Hot reload for code and assets in development.
- Pressing Play runs the game in its own process, so a crash in game code never takes the editor down.
- Rendering reads a render world, not the game world: each frame an extractor copies cameras, meshes, lights and volumes into a `RenderScene`, which a later stage can extract on the game thread and draw on a render thread. Drawing is an ordered pipeline of render passes (skinning, light gathering, shadows, depth prepass, screen shadows, opaque, post) sharing a frame context; features are added as passes. Barriers stay explicit until a frame graph earns its place.

## Look and lighting

- Orthographic and perspective cameras (owner, 2026-11-18). The orthographic one snaps to whole pixels so painted textures don't shimmer.
- Simple first (owner, 2026-11-18): a sun and ambient light and a viewer for a painted test room come before the styled shading, so the look can be judged early.
- Scenes are real 3D geometry with painted textures; characters are skinned meshes with painted textures. Painted cards and decals add 2D detail.
- One shading model with artist controls per material (ramp softness, wrap, rim, specular, emissive, shadow tint) on physically based light maths. Stage 4 shipped plain diffuse; the controls come with the editor (owner, 2026-11-19), where they can be tuned live.
- Clustered forward lighting for many point, spot and directional lights.
- Shadows: one orthographic sun shadow map fitted to the view, a shadow atlas for local lights, soft filtering, contact shadows.
- Light cookies: spot lights project painted textures.
- Baked irradiance probes for bounce light.
- Volumetric fog lit by the same lights.
- Post: ambient occlusion, bloom, colour grading per area or mood, paper or grain overlay.
- Lighting moods as data assets the story switches through facts and conditions.

## Roadmap

0. **Skeleton.** Solution, build settings, CI on Windows and Linux, CLAUDE.md, this plan. Gate: green CI on both.
1. **Gameplay core, pure C#.** Definitions and fragments, tags, facts, conditions and effects with compare, scales with bands and links, ranking, ported with their Unreal tests. Gate: the social rules (standing, fame, titles) pass in tests on Linux CI.
2. **Runtime foundation.** Silk.NET window and input, main loop with fixed and variable update, Arch entities, resource loading by path, logging and a console, BepuPhysics. The rules that read entities or world services port with it: attitude, disguises and recognized fame, and the conditions and effects that use the context's instigator, target or services. Gate: a sample spawns entities from a scene file and physics bodies fall.
3. **Graphics layer.** A thin layer over Vulkan adapted from Stride, HLSL through DXC. Ends with a spike: the renderer inside an Avalonia window on Windows and Linux. Gate: a textured mesh renders on Vulkan on Windows and Linux. (Direct3D 12 was built for this stage and dropped after it.)
4. **Lighting renderer.** Everything in Look and lighting, plus basic glTF import. Gate: a painted test room with a character and dozens of lights at 60 fps.
5. **Asset pipeline.** Text assets with schemas, the builder with a content-hash cache, texture compression, glTF import, hot reload. Gate: edit a texture or a light in a file and the running game shows it within a second.
6. **Editor.** Avalonia: project browser for `Documents/FlickerVoid`, scene view, inspector generated from schemas, light editing and probe baking, the per-material shading controls, layered painted textures (an idea to explore), Play in a separate process. Gate: build and light a room without touching a text file.
7. **FlickerVoid systems and the demo project.** Interaction, inventory and outfits, dialogue and story graphs, navigation and map, UI, saves. Gate: the basic story loop from the Unreal milestone (inventory, dialogue, a quest, map, interaction) plays in NGN.

A development-only debug UI (Dear ImGui through Hexa.NET.ImGui, in `Source/Debug`) arrived after stage 4 (owner, 2026-11-19): engine panels for frame, CPU, memory and GPU statistics, the console, the renderer's switches and moods, plus a public `DebugWindow` API for per-project panels (the samples have theirs). The editor will use its own Avalonia UI instead.

Stage 1 runs in parallel with stages 2 and 3.
