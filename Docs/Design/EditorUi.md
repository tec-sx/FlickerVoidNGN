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
- **R26. Refusals are toasts** (owner, 2026-11-28). When a tool can't do what was asked (stairs with no storey above, an opening on no wall), a toast at the top of the viewport says why and what to do instead, in a sentence, and goes after a few seconds or on a click; at most three show. Nothing is refused silently, and a refusal never opens a dialog. A tool that works says so in its own tags, not in a toast.

## Before a new feature is built

1. Write its flow as numbered steps, today and proposed.
2. Draw it from the kit's components; if none fits, the new component joins the kit first.
3. Check it against R1 to R26 and write down any exception with its reason; the owner approves the design.
4. The pull request carries before and after screenshots.

Rules the code enforces today are tests; the rest are checked in review. Rules not yet enforced (R3, R13, R16, R17, R18) become tests in the theme and kit steps of the redesign.
