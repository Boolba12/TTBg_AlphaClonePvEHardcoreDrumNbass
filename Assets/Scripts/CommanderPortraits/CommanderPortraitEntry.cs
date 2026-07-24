using System;
using UnityEngine;

[Serializable]
public sealed class CommanderPortraitEntry
{
    [SerializeField] private string id;
    [SerializeField] private Sprite sprite;
    [SerializeField] private CommanderRace race;
    [SerializeField] private string resourceName;

    public string Id => id;
    public Sprite Sprite => sprite;
    public CommanderRace Race => race;
    public string ResourceName => resourceName;

    public CommanderPortraitEntry(string id, Sprite sprite, CommanderRace race, string resourceName)
    {
        SetData(id, sprite, race, resourceName);
    }

    public void SetData(string newId, Sprite newSprite, CommanderRace newRace, string newResourceName)
    {
        id = newId;
        sprite = newSprite;
        race = newRace;
        resourceName = newResourceName;
    }
}
