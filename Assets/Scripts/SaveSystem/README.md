# Save system

## Scene setup

1. Add `SaveSystemBehaviour` to a scene-owned composition object.
2. Add `OverworldSaveParticipant` to the overworld scene and assign its existing
   `MapGenerator`, `MapRenderer`, `MapRockPlacer`, `PlayerController`, and
   `EnemyController`.
3. Add that participant to `SaveSystemBehaviour.participants`.
4. Put an equivalently configured `SaveSystemBehaviour` in every scene that can
   be restored. The pending-load context follows the same explicit static-context
   style already used by `BattleEncounterContext` and `BattleSetupContext`.

No Unity object references are serialized. Saves are UTF-8 JSON under:

`Application.persistentDataPath/Saves/<slot>.json`

The previous valid file is `<slot>.json.bak`; writes use `<slot>.json.tmp`.

## Public calls

`NewGame()`, `SaveGame()`, `Autosave()`, `LoadGame()`, `DeleteSave()`, and
`HasSave()` can be connected directly to UI buttons. String overloads of save
and load provide the extension point for multiple slots.

## Adding another game system

Implement `ISaveable` on a scene component and add it to the participant list:

```csharp
[System.Serializable]
public class QuestState { public int completedCount; }

public class QuestSaveParticipant : MonoBehaviour, ISaveable
{
    public string SaveKey => "quests";
    public int completedCount;

    public string CaptureState()
    {
        return JsonUtility.ToJson(new QuestState { completedCount = completedCount });
    }

    public void RestoreState(string json)
    {
        QuestState state = JsonUtility.FromJson<QuestState>(json);
        completedCount = state != null ? state.completedCount : 0;
    }
}
```

Keys must be stable and unique. Missing payloads are ignored, so newly added
optional systems remain compatible with older saves.

## Extension points and assumptions

- Add ordered `ISaveMigration` implementations for future format versions.
- Add slot-selection UI using the existing string overloads.
- Add more `ISaveable` participants without changing the file layer.
- Autosave is deliberately an explicit call; connect it to a confirmed game
  event rather than `Update`.
- Current overworld restoration regenerates the deterministic map from its seed,
  renders it, then restores unit cells.
- Battle outcome/progression is not saved because no persistent battle-result
  model currently exists.
- Scene names stored in a save must remain present in Build Settings.
