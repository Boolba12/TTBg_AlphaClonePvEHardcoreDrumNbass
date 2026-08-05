# Purgatory battle UI foundation and visual pass

## Technology and ownership

The production UI foundation uses UGUI with TextMeshPro. `PurgatoryUITheme` is
the single visual token asset. Runtime views never mutate the theme and never
own battle state.

`SquadBattleRuntime` remains the only source of HP, AP, morale, composition and
defeat state. `BattleSquadStatusPresenter` listens to its public events and maps
the state into an immutable `BattleSquadStatusModel`. The UI does not write to
`SquadData`.

The initial battle selection contract is the one initialized controller whose
`BattleSide` is `Player`. No current-turn system exists yet, so the HUD does not
invent active-turn state. `InitiativeQueuePresenter` renders
`SquadInitiativeOrder.Entries` as supplied and never sorts a second copy.

## Scene separation

`Raw_Alpha_BattleMode` contains one production `BattleUIRoot` prefab instance.
Its hierarchy contains `HUDLayer`, `TooltipLayer`, and `ModalLayer`. The older
`BattleContextMenu`/`MenuContainer` Canvas remains a separate setup prototype
and is hidden by the existing confirm pathway. The scene retains exactly one
EventSystem.

`DebugHUD` in `first_try` and `GameStartUI` in `alpha_game` are legacy/prototype
UI. They are not dependencies of the Battle HUD. The main menu was audited but
not redesigned because there is no approved final artwork set or stable menu
flow in this phase.

## Battle HUD visual pass

The second-stage HUD keeps the original `BattleHUDController`, presenters,
runtime event subscriptions, and portrait lookup. The four major anchor zones
were reduced from a documented combined normalized area of `0.5431` to
approximately `0.4011` (about `26%`). This uses anchors, padding, layout
elements, and section spacing; neither the Canvas nor `BattleUIRoot` is scaled.

Readable HP/AP/Morale values, commander and initiative portraits, tooltip text,
and the 48-pixel minimum button hit area are deliberately preserved. The bottom
bar is one outer frame with three vertical separators. Disabled actions expose
an icon placeholder, label, state, hotkey area, AP-cost area, and tooltip, but
have no gameplay listeners.

## Development assets and replacement points

`Assets/UI/Art/DEV/DevelopmentUISprites.asset` contains editor-generated
monolith outer/inset/header surfaces, bronze separators, selected frames,
normal/hover/pressed/disabled buttons, portrait/initiative/equipment frames,
empty-slot and icon placeholders, plus a commander silhouette. They contain no
third-party material. All surface, side, text, overlay, portrait, initiative,
button, padding, and spacing references remain centralized in
`PurgatoryUITheme.asset`.

The existing `CommanderPortraitDatabase` receives the DEV fallback sprite and
the builder scans both the recommended `Assets/Art/CommanderPortraits/*`
folders and the actual imported Human/Elf folders under
`Assets/Scripts/CommanderPortraits`. Folder mapping controls race; battle side
does not. Portrait IDs are stable Unity asset GUIDs and are never reassigned by
the HUD.

## Item presentation boundary

`DevelopmentItemPresentationCatalog.asset` is presentation-only. A record may
adapt an existing `BattleWeaponDefinition` for display without duplicating its
combat data. The current project has no verified Blender item model or matching
item preview image; the one legacy Unity primitive weapon is therefore exposed
as an explicit `UnknownTest` placeholder. The old tree/cactus voxel models and
their 256x1 palette textures are not treated as items or previews.

Use `Tools > Purgatory UI > Open Item Preview Gallery` to inspect catalog
records without adding any scene instance, inventory ownership, equipment, or
runtime model viewer. `ItemPreviewCard.prefab` provides the reusable future UI
surface and a controlled empty state.

## Deliberate empty states

Action, commander perk, consumable, ability-detail, equipment, and minimap
elements are disabled presentation placeholders. They have no gameplay
listeners. Presentation contracts reserve weapon, armor and accessory slot
kinds without implementing inventory or equipment logic.

Tooltip anchors use one `TooltipController` under `TooltipLayer`, with delayed
show, bounds clamping, and non-blocking rendering. UI graphics block map pointer
input through the existing EventSystem; the legacy `PlayerController` also
guards its map raycast when the pointer is over UI.

## Protected editor installation

`BattleUIInstaller` is editor-only and split into three explicit paths:

- `Validate Existing Battle HUD` is read-only;
- `Wire Existing Battle HUD Into Raw Scene` uses the existing prefab and does
  not rebuild visual assets;
- `Rebuild DEV Visual Assets And Battle HUD (Destructive)` requires an Editor
  confirmation dialog.

Automation refuses the destructive rebuild unless the command line includes
`-purgatoryUiConfirmDestructiveRebuild`. Rebuilding the prefab does not silently
replace the Raw scene instance. No UI decoration is generated at runtime, and
smoke verification never invokes the installer.
