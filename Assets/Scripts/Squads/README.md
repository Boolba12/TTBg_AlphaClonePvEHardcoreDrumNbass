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

1. Keep the existing `PlayerController` and `EnemyController` as the logical
   cell/movement controllers.
2. Add one `SquadBattleController` beside each logical controller and assign that
   controller in `movementController`.
3. Add `SquadFormationView`; assign one commander model, up to eight warrior
   models, and local slot transforms. All remain children of the one logical root.
4. Add `SquadBattleBootstrap`, configure matching controller lists, and assign it
   to the optional field on `BattleMapBootstrap`.
5. Before the existing scene transition, call:

```csharp
BattleSquadSelectionContext.SetSelection(selectedPlayerSquads, selectedEnemySquads);
```

`BattleMapBootstrap` still generates the map and places the existing logical
controllers. It then initializes only selected squads. Each initialized squad is
registered once in `SquadInitiativeOrder`.

## Save integration

Add `SquadSaveParticipant` to `SaveSystemBehaviour.participants`. Register active
runtime instances through `RegisterRuntime`. Persistent squad data is always
saved; individual battle HP and other runtime fields are also saved when
`saveActiveBattleState` is enabled.

Commander portraits are stored as the stable `CommanderPortraitId` string on
`SquadData`, compatible with `CommanderPortraitService.AssignPortraitIfMissing`.

## Deferred design decisions

- weapon damage, accuracy, armor, penetration, critical, and resistance formulas;
- how AP costs map to the existing movement/attack input;
- a full multi-squad turn scheduler replacing the current two-controller loop;
- commander survival probability and the permanent-debuff catalog;
- post-battle casualty persistence/recovery rules;
- prefab/model selection and exact formation geometry;
- ability definitions and when they call `TryIncreaseUsedPrimaryStat`;
- morale consequences such as panic or retreat.
