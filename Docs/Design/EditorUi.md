# Editor UI rules

Rules for every new piece of editor interface (owner brief and approval, 2026-11-26): maximum working area, thin borders, frequent actions as small overlay buttons on the viewport, warm colours (no neutral grey), things in categories, never a long list to scroll. Godot is never a reference for the look. The design, mockups and palettes are in the design document "Editor redesign: plan and design"; read it before designing UI.

The picture comes first, hands stay on the picture, the look is warm and calm, categories replace lists, and each thing has one place.

## Space

- **R1. The picture comes first.** A feature never takes viewport area for good: it lives in the side column, in a dock that closes, or as an overlay button. The viewport keeps at least 79% of a 1920 x 1080 window in the default layout.
- **R2. Hairlines only.** Borders are 1 px in the Line colour. No frames, gaps between panels, shadows on docked panels or boxes inside boxes. Group with spacing: 2 px inside a group, 8 px between groups.
- **R3. Sizes come from the kit.** 22 px rows, 30, 26 and 24 px bars, 3, 4 and 6 px corners. A test fails on a size written into a view.
- **R4. Nothing is cut off.** At the default layout labels and numbers show in full; a long name is shortened in the middle and has a tooltip.

## Placement

- **R5. Frequency decides the place.** Many times a minute: an overlay button on the viewport with a shortcut. Many times an hour: the side column. Less often: the command palette or a menu.
- **R6. Everything opens in place.** A form is a tab in the context panel, a canvas is a document tab, anything with a timeline goes in the bottom dock, a creation opens in the creator with a breadcrumb. No new window; no modal dialog except to confirm losing work.
- **R7. Only what is relevant.** The context panel shows the tabs that belong to the selection, brought to the front.
- **R8. One place for each value.** A value is edited in one place; anywhere else links there.

## Organisation

- **R9. No long lists.** A flat list never passes 12 rows. Group into named sections, tabs or a rail, at most 7 groups.
- **R10. What can grow can be searched.** Assets, entities, components, settings and nodes each have a search box.
- **R11. Categories use the owner's words** (Sun, Sky, Fog, Grade), not the code's structure.
- **R12. Folded sections speak.** A closed section says what it holds in one line. Common values first; rare ones under More.

## Colour and type

- **R13. Colours come from the theme.** No colour is written into a view; a test fails on one.
- **R14. Warm and quiet.** Surfaces stay nearly colourless and warm. One accent marks selection, focus, unsaved work and the single primary action on screen.
- **R15. A colour means one thing.** Accent, green (running, done), yellow (warning, in progress), red (error, delete), blue (reference), X/Y/Z (axes only). A kind of asset may carry a tint beside an icon and a name; colour is never the only signal.
- **R16. Contrast.** 7:1 for primary text, 4.5:1 for all other text, 3:1 for icons and control edges. A test measures the palette.
- **R17. One font, one icon family.** Inter at four sizes (13, 12, 11, 10.5 caps), numbers with equal widths, outline icons drawn in the text colour.

## Behaviour

- **R18. Everything has a name.** Every action has a tooltip with its shortcut and an entry in the command palette; anything clickable can be reached from the keyboard.
- **R19. Changes are live and undoable.** They apply and show at once and come back with Ctrl Z. No Apply buttons.
- **R20. Escape goes one level out** and never loses work.
- **R21. One way to save.** A dot on what is unsaved, Ctrl S for this document, Ctrl Shift S for all. No editor has its own Save button.
- **R22. Numbers behave alike.** They drag, show unit and range, mark a value changed from its default and can reset it: use the kit's number field.
- **R23. Nothing blocks, nothing is blank.** Long work shows progress in the bottom strip and can be cancelled; an empty panel says what to do next; an error says what happened and what to do, where it happened.
- **R24. Verbs.** Buttons and menu items say what they do ("Bake probes", not "Probes").
- **R25. One kit for editing tools** (owner, 2026-11-27). A tool is a name, an icon, a key, options and, when it builds something, a preview that waits for Apply. It goes in the tool rail down the viewport's left edge (lit while chosen or while its key is held), its options in the Tool tab in the inspector's place, and Apply (Enter) and Cancel (Escape) in the confirm bar at the bottom of the view; never a bar of numbers along the bottom. The level and every creator fill the same rail, and a creator draws on a dark background without sky.
- **R26. One card for every section** (owner, 2026-11-27). A panel is built from panel cards, as the inspector shows a component: a 24 px header with a chevron, the design's icon, the title, the card's actions and its More menu, over rows whose labels share one column, 96 px or as wide as the card's longest label. Values follow the panel's width; as it narrows, labels give way first and read whole in their tooltip. No view builds a section of its own; a test fails on one.
- **R27. Refusals are toasts** (owner, 2026-11-28). When a tool can't do what was asked (stairs with no storey above, an opening on no wall), a toast at the top of the viewport says why and what to do instead, in a sentence, and goes after a few seconds or on a click; at most three show. Nothing is refused silently, and a refusal never opens a dialog. A tool that works says so in its own tags, not in a toast.
- **R28. One timeline kit for everything with time** (owner, 2026-11-28). A ruler, lanes, a transport, a fader or a meter is the kit's (`Shell/Controls/Timelines`, `Shell/Controls/Mix`, over `Core/Timelines`), whatever it counts (bars, hours, seconds); a view never draws its own. One ruler with the position chip, lanes whose items are drawn by registered painters, one overlay for bands and the position line, one transport bar, one channel strip; tabs and filters are a button group, long lists an item list, filters with counts a pill filter. A test fails on a ruler, transport, fader or meter defined outside the kit. The look is the mockups' (`Design/Timeline.md`), measure for measure.

## Before a new feature is built

1. Write its flow as numbered steps, today and proposed.
2. Draw it from the kit's components; if none fits, the new component joins the kit first.
3. Check it against R1 to R28 and write down any exception with its reason; the owner approves the design.
4. The pull request carries before and after screenshots.

Rules the code enforces today are tests; the rest are checked in review. Rules not yet enforced (R3, R17, R18) become tests in the theme and kit steps of the redesign.

## The kit's shared controls

What every editor view builds from (rule R28); the measures are in `Design/Timeline.md`.

| Control | Where | What it is |
| --- | --- | --- |
| `TimeRange` | `Core/Timelines` | The times a view shows across its width: pan, zoom about a point, frame, map between time and pixels. Shared by everything drawn over the same times. |
| `ITimeScale` | `Core/Timelines` | What a ruler counts: `BarScale` (bars and beats, phrases as sections), `HourScale` (the day, repeating), `SecondScale`. Gives the ticks, the snap step and the position's text. |
| `TimelineViewModel` | `Core/Timelines` | A range, a scale, lanes of items, regions, the cursor and the playhead; `TimelineEdit` turns the pointer and keys into moves, resizes, creates and deletes, which an `ITimelineEditor` applies. |
| `TimeRuler` | `Controls/Timelines` | 24 px: ticks, labels, sections bold, region tabs and the position chip; pressed or dragged it moves the cursor (snapped unless Alt), wheel zooms, Shift pans. |
| `TimelineLanes` | `Controls/Timelines` | The lanes' rows with the scale's grid; each item drawn by the `ILaneItemPainter` registered for its kind (`ClipPainter`, `FillPainter`, `MarkerPainter`, `KeyPainter`). |
| `TimelineOverlay` | `Controls/Timelines` | Over the lanes, not hit: region bands (live, queued, idle), the draft and the position line. |
| `LaneHeader` | `Controls/Timelines` | A lane's header: a 3 px edge in its tint, icon, title, note, buttons and its own rows; a sub lane indents 22 px. |
| `Transport` | `Controls/Timelines` | The 34 px bar over an `ITransport`: play or pause, stop, loop, the position (15 px semibold), the clock time, then the editor's fields (`transportField`: 22 px, its name faint) and tools. |
| `TriggerButton` | `Controls/Timelines` | A 24 px trigger for a cue or a shot: a 16 px key box, the icon, the name and a note; washed while idle, filled while live, dashed and glowing while queued. |
| `BeatPulse` | `Controls/Timelines` | The one clock queued things pulse on. |
| `ChannelStrip` | `Controls/Mix` | An 88 px mixer strip: the name under a 3 px tint, fader, two meters and the dB scale, the route, gain, mute and solo; a bus strip in bold. `Fader`, `LevelMeter`, `FaderScale` and `GainBar` are its parts. |
| `ButtonGroup` | `Controls/Timelines` | Joined tabs on a 26 px bar, the chosen one raised with an accent bar at its foot; with `chips`, rounded filters. |
| `ItemList` | `Controls/Lists` | A list with Ctrl and Shift clicks, Ctrl+A and a right-click that keeps the selection (`ListSelection<T>`), with tick and dash marks in menus (`SelectionMarks`). |
| `PillFilter` | `Controls/Lists` | A wrapping row of 20 px pills with counts, one or several chosen, and an add pill. |
