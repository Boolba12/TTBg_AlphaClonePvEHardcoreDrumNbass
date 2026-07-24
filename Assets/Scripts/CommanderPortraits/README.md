# Commander portrait runtime integration

## Setup

1. Rebuild the database from `Tools/Commander Portraits/Rebuild Database`.
2. Add `CommanderPortraitSaveParticipant` to the same scene composition object
   used for saving.
3. Assign `Assets/Art/CommanderPortraits/CommanderPortraitDatabase.asset`.
4. Add the participant to `SaveSystemBehaviour.participants`.

Access the runtime API through the participant's `Service` property. This keeps
the database independent of scenes and UI.

```csharp
[System.Serializable]
public sealed class SquadData : ICommanderPortraitTarget
{
    [SerializeField] private string commanderPortraitId;
    public string CommanderPortraitId
    {
        get => commanderPortraitId;
        set => commanderPortraitId = value;
    }
}

CommanderPortraitEntry portrait =
    portraitParticipant.Service.AssignPortraitIfMissing(squadData, CommanderRace.Human);

image.sprite = portrait != null
    ? portrait.Sprite
    : portraitParticipant.Service.GetDisplaySprite(squadData.CommanderPortraitId);
```

`AssignPortraitIfMissing` preserves an existing valid ID. Call
`GetRandomPortrait(race)` only when explicit reassignment is intended.

## Save behavior

The participant saves the shuffle-bag state through the existing `ISaveable`
pipeline. Each squad's own persistent model must save its portrait GUID as part
of that squad's data. No squad model currently exists in the project, so this
module deliberately provides the interface without inventing one.

On restore, deleted IDs and duplicates are removed. New database IDs join the
current remaining pool without resetting already-used portraits. A squad whose
saved ID was deleted receives a warning and a new same-race portrait the next
time `AssignPortraitIfMissing` is called.
