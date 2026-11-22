# Porting FlickerVoid from Unreal

Decided with the owner on 2026-11-20: everything in the Unreal project moves to NGN. Generic systems (the Unreal plugins) become engine modules, game rules and content logic go into the FlickerVoid project (`Projects/FlickerVoid`), and AngelScript classes become entity classes and systems in that project. The ontology tree (Game -> Interface, Gameplay, Presentation) and the functional layers carry over. The entity model is Arch-first (owner, 2026-11-20; design doc "Entities, data assets and flow graphs"): struct components, systems with lifecycle hooks, generated handles per entity class.

## Layers

| Unreal layer | NGN layer | Where |
| --- | --- | --- |
| Engine features (GAS, Mover, StateTree, CommonUI, Enhanced Input) | engine modules, ours or from libraries | `Source/Engine/<Module>` |
| FVFramework and the FV*System plugins | engine modules, generic, no game rules | `Source/Engine/<Module>` |
| Game module C++ (`Source/FV*`): setup, shapes, hosts | project code: components, definitions, systems | `Projects/FlickerVoid/Source/<Module>/<ontology path>` |
| AngelScript (`Script/`) | project entity classes and systems | `Projects/FlickerVoid/Source/<Module>/<ontology path>` |
| Content: data assets, Blueprints for looks, widgets | `.fvdata`, `.fvtable`, `.fventity`, `.fvflow`, scenes, UI files | `Projects/FlickerVoid/Content` |

The project's modules follow the ontology's first level: `Gameplay` (Protagonist, World, Narrative), `Interface` (menus, HUD, hub), `Presentation` (animation, camera, looks), plus `Launcher`, which holds what sits at the ontology's root (game modes, the player controller). Engine modules never name the project; the project composes them.

## Engine modules

| Unreal | NGN module | What moves | Needs first |
| --- | --- | --- | --- |
| FVFramework: definitions, fragments, facts, conditions, effects, scales, compare, identity | `Gameplay` | done in stage 1; identity component and spawner join the entity model | entity model |
| FVFramework: relevance (distance bands), world clock, easing, saveable | `Runtime` (`Relevance`, `Time`, `Saves`) | relevance system and bands, world clock, saves of components and facts | entity model |
| FVFramework: input config, gestures, input component | `Platform` (`Input`) | input actions mapped from keys and pads, gestures (hold, double tap) | |
| FVFramework: Flow nodes, flow context; Flow plugin | `Flow` | graph runtime, core nodes, `.fvflow`, then the graph editor | data assets |
| FVFramework: UI layout, HUD, activatable widgets (CommonUI) | `Ui` | game UI layer: Avalonia (AXAML and MVVM, like the editor) drawn into a texture over the scene (owner, 2026-11-21) | Hosting |
| FVFramework: debug HUD, fact and relevance debug | `Debug` | ImGui panels | |
| FVFramework: editor customizations, MaxEntries validator | `Editor` and `Builder` | inspector views, build-time validation | data assets |
| FVFramework: highlight component | `Rendering` | outline pass | |
| FVFramework: cinematics, StateTree nodes | later | sequences and AI behaviour, after Flow and AI | |
| FVAttributeSystem (on GAS) | `Attributes` | attribute definitions, attribute sets as components, modifiers in place of Gameplay Effects, checks, conditions | entity model, data assets |
| GAS in FVGameplay (ability set, costs, tag relationships) | `Abilities` | ability definitions, activation with tags that block and cancel, costs; game abilities stay in the project | attributes |
| FVInteractionSystem | `Interaction` | interactor, interactable, offers and focus, lock, toggle response, definitions, settings, conditions; compile rules become build-time checks of entity templates | entity model, input |
| FVInventorySystem | `Inventory` | item definitions and fragments, stack data, inventory with weight and grid, equipment slots for outfits, pickup, item receiver, loot tables, conditions and effects | data assets |
| FVDialogueSystem | `Dialogue` | participants, dialogue Flow nodes (line, choice, end, bark), dialogue service for UI | Flow |
| FVStorySystem | `Story` | quests and quest Flow nodes, knowledge; its save system moves to `Runtime` | Flow |
| FVNavigationSystem | `Maps` (not "Navigation", kept for AI paths) | map and marker definitions, markers, discovery, waypoint, tracking, minimap, world map and compass logic; widgets go to `Ui`; the map capture tool goes to the editor | Ui |
| FVCharacter: movement handlers, traversal (Mover) | `Characters` | kinematic character on BepuPhysics, movement modes, traversal | input |
| FVCharacter: anim instance, rig units, locomotion data | `Rendering` (`Animation`) | locomotion blending, procedural rig units | Characters |
| FVAI: perception, StateTree AI; NavMesh | `Ai` | perception, behaviour states, paths through DotRecast (zlib) | Characters |
| FVCore: Flow triggers, physical material tags | `Physics` and `Flow` | trigger volumes, trigger Flow nodes, surface tags | Flow |
| GameplayMessageRouter | `Runtime` | struct event queues of the entity model | entity model |
| ToneMapFX | `Rendering` | done: tonemappers | |

## Project code

| Unreal | NGN project path |
| --- | --- |
| `Source/FVGameplay/Social`, `Script/Gameplay/Protagonist/Identity/Social` | `Gameplay/Protagonist/Identity/Social` (done in stage 1 and 2) |
| `Script/Gameplay/Protagonist/Identity/Personality` (skill attribute set) | `Gameplay/Protagonist/Identity/Personality` |
| `Script/Gameplay/Protagonist/State/Physical` (physical attribute set), `FVIntoxicationComponent` | `Gameplay/Protagonist/State/Physical` |
| `Source/FVGameplay/Items`, `Script/Items`, `Script/Items/Abilities`, `Script/Items/Trade` | `Gameplay/Protagonist/Resources/Possessions` (held, used, worn, given, thrown items; trade) and `Gameplay/World/Objects/Items` (world items, loot containers, money, readables) |
| `Source/FVGameplay/Scanner` | `Gameplay/Protagonist/Resources/Knowledge/Scanner` |
| `Source/FVGameplay/Abilities`, `Script/Abilities` (examine, talk, lockpick) | `Gameplay/Protagonist` abilities, beside what they act on |
| `Script/World/Doors`, `Source/FVWorld`, `Script/Environment` | `Gameplay/World/Objects` (Door entity class and system, foliage) |
| `Script/World/FVFaction.as` | `Gameplay/World/Society/Factions` |
| `Source/FVAI`, `Script/AI`, `Script/Characters` | `Gameplay/World/Society/Residents` (NPC entity classes, their facts and behaviour) |
| `Script/Narrative`, `Source/FVNarrative` | `Gameplay/Narrative/Discovery` (dialogue tasks, objectives, call-outs) |
| `Script/Interaction/Requirements` | conditions beside what they check (tags, attributes) |
| `Script/Core/Player`, `Script/Core/Movement`, `Script/Core/Camera`, `Source/FlickerVoidGame` | `Launcher` (game modes, player controller, settings) and `Gameplay/Protagonist` (the protagonist entity class and its movement setup) |
| `Script/Animation` | `Presentation/Animation` |
| `Script/UI/*`, `Source/FVUI` | `Interface` (main menu, pause, options, HUD, hub pages, dialogue, interaction prompts, map screen) |

## Order

Each step is one pull request or a short stack, and ends in something to try in the editor or the game.

1. **Foundation:** explicit components and the generator, systems with lifecycle hooks and events, entity classes and `.fventity` templates, the editor loading project code.
2. **Data:** data assets, tables, typed references, the asset registry.
3. **Flow:** graph runtime and core nodes, then the graph editor.
4. **First playable slice:** input actions, the `Characters` module with a third-person camera, `Interaction`, the lock and the door. Gate: walk the protagonist to a locked door and open it.
5. **Possessions:** `Inventory`, pickups, keys, outfits.
6. **Story:** `Dialogue` and `Story` on Flow, knowledge, facts in saves.
7. **Rules:** `Attributes` and `Abilities`, then the game's abilities and items.
8. **Interface:** the `Ui` module, HUD and hub pages.
9. **World:** `Maps`, `Ai` with residents, the scanner.
10. **Saves** across every module, then the stage 7 gate: the basic story loop from the Unreal milestone plays in NGN.
