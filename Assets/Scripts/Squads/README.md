# Tactical squads

## Architecture

- `SquadData`, `CommanderData`, and `WarriorData` are persistent data.
- `SquadBaseStats` stores the commander's unmodified values.
- Each warrior stores only HP, Strength, and Dexterity.
- `SquadStatModifiers` stores persistent or temporary additive changes.
- `SquadStatsCalculator` recomputes `SquadCalculatedStats`; calculated values are
  never the source of truth.
- `SquadBattleState` stores current individual HP, AP, morale, effects, logical
  cell, turn, and initiative state.
- `SquadBattleRuntime` owns battle behavior and emits presentation-neutral events.
- `SquadDamageResolver` distributes already-finalized damage. Armor, resistance,
  penetration, hit, and critical formulas belong before this service.

Core formulas for living members:

```text
MaxHP     = CommanderHP       + sum(WarriorMaxHP)    + HP modifiers
Strength  = CommanderStrength + sum(WarriorStrength) + Strength modifiers
Dexterity = CommanderDexterity + sum(WarriorDexterity) + Dexterity modifiers
```

Single-target damage hits the first living formation warrior and discards
overflow. Area damage carries overflow through formation order and reaches the
commander only after every warrior is defeated.

## Scene integration

`Raw_Alpha_BattleMode` uses `BattleCombatMode.Squads`.
`BattleMapBootstrap` explicitly calls `SquadBattleBootstrap.InitializeSquads`
after `MapGenerator.Generate` and `MapRenderer.RenderMap` have completed and two
different playable cells have been resolved.

`SquadBattleBootstrap` owns creation. It instantiates exactly one
`SquadBattle.prefab` for the player and one for the enemy under
`SquadBattleCompositionRoot/SpawnedSquads`. Each prefab contains:

- one `SquadBattleController`;
- one lightweight `SquadGridAnchor`;
- one `SquadFormationView`;
- one commander slot and eight warrior slots;
- configured commander and warrior model prefab references.

It does not contain `PlayerController`, `EnemyController`, or `UnitStats`.
All members share the root grid anchor and therefore occupy one logical cell.
Member models never enter initiative independently.

The current scene is an explicit development configuration. On normal Play,
`BattleMapBootstrap.enableDevelopmentSquadAutoConfirm` submits the existing
`BattleContextMenuUI.ConfirmBattleSetup` pathway exactly once. The field defaults
to disabled in code and only succeeds when squad mode is active and the
Inspector development fallback is valid and is the actual selected source.
Any supplied scene selection or saved roster prevents development auto-confirm,
so invalid production data is not masked. The Play Mode smoke waits for this
same pathway and never calls confirm or squad initialization directly.

Each spawned controller receives two separate, read-only battle-context values:

- `BattleSide.Player` plus `SquadControlType.Human` for the player squad;
- `BattleSide.Enemy` plus `SquadControlType.AI` for the enemy squad.

The bootstrap assigns these once before runtime initialization. Neither value
is persisted in `SquadData`, inferred from object names, nor derived from list
or spawn order.

Before the scene transition, production selection can call:

```csharp
BattleSquadSelectionContext.SetSelection(selectedPlayerSquads, selectedEnemySquads);
```

Data-source priority is:

1. valid, distinct squads in `BattleSquadSelectionContext`;
2. valid, distinct squads in `SquadSaveParticipant`;
3. the explicitly enabled Inspector development fallback.

An incomplete or invalid production context does not silently fall through to
development data. Bootstrap enters `Failed`, logs a controlled error, and does
not start a legacy battle. `FailureReason` retains the diagnostic, partial
controllers, initiative entries, and repository runtime registrations are
cleared, and `ResetFailedStateForRetry` permits one controlled retry after the
source is repaired. Initialization remains rejected after success.

`BattleSquadSelectionContext` is read and validated first. It is consumed only
after both battle participants have been created and registered successfully.
Failed validation leaves it available for repair/retry, while a successful
battle cannot leak stale selection into the next battle.

Initiative ordering is deterministic:

1. higher calculated initiative;
2. lower one-time battle registration sequence;
3. ordinal `SquadId` as the final fallback.

`Resort` uses the same comparator, and duplicate squad registration is rejected.

The canonical legacy pair is the scene root `player` and scene root `enemy`;
these are also the controllers used by legacy initialization. The additional
root objects named `PlayerController` and `EnemyController` are retained as
obsolete duplicates and always remain inactive. Squad mode disables the
canonical pair too. To restore the old prototype, switch `combatMode` to
`LegacyUnits`; exactly the canonical player/enemy pair becomes active.

The temporary
`Assets/Prefabs/Squads/DevelopmentSquadMemberPlaceholder.prefab` is one
universal scene-safe visual. Replace the two model prefab references on
`SquadBattle.prefab/SquadFormationView` when real models are available; no
domain or runtime code needs to change.

## Save integration

Add `SquadSaveParticipant` to `SaveSystemBehaviour.participants`. Register active
runtime instances through `RegisterRuntime`. Persistent squad data is saved.
`saveActiveBattleState` is disabled in this integration because production
mid-battle restore is deferred.

Commander portraits are stored as the stable `CommanderPortraitId` string on
`SquadData`, compatible with `CommanderPortraitService.AssignPortraitIfMissing`.
`CommanderPortraitSaveParticipant` and `SquadSaveParticipant` are both explicitly
registered in the battle scene's existing `SaveSystemBehaviour`.

## Manual verification

1. Open `Assets/Scenes/Raw_Alpha_BattleMode.unity`.
2. Select `BattleModeManager`; confirm combat mode is `Squads`, the squad
   bootstrap and setup UI references are assigned, development auto-confirm is
   enabled for this scene, the canonical legacy pair is assigned, and two
   obsolete legacy roots are listed.
3. Select `SquadBattleCompositionRoot`; confirm the squad prefab, spawned-squads
   container, repository, enabled development fallback, and two valid fallback
   squads are assigned.
4. Open `Assets/Prefabs/Squads/SquadBattle.prefab`; confirm one controller, one
   grid anchor, one formation view, commander slot, eight warrior slots, and the
   two placeholder references.
5. Enter Play Mode. Confirm the Console records the one-time development
   auto-confirm reason; no test adapter or manual confirm is needed.
6. After map generation, inspect `SpawnedSquads`: exactly
   `PlayerSquad_dev-player-squad` and `EnemySquad_dev-enemy-squad` should exist.
7. Each should contain one active commander model and four active warrior models.
8. In the Console, confirm two source/id/warrior/cell/initiative messages and one
   completion message reporting two initiative entries.
9. Confirm the canonical and obsolete legacy player/enemy roots are inactive and there is
   no active `PlayerController`, `EnemyController`, or `UnitStats` on either
   spawned squad.
10. Stop Play Mode. To test real data, populate
    `BattleSquadSelectionContext` before scene load; it takes priority over both
    saved and development data.

## Deferred design decisions

- weapon damage, accuracy, armor, penetration, critical, and resistance formulas;
- how AP costs map to the existing movement/attack input;
- a full multi-squad turn scheduler replacing the current two-controller loop;
- commander survival probability and the permanent-debuff catalog;
- post-battle casualty persistence/recovery rules;
- prefab/model selection and exact formation geometry;
- replacing the development fallback with the squad-selection UI;
- ability definitions and when they call `TryIncreaseUsedPrimaryStat`;
- morale consequences such as panic or retreat.
