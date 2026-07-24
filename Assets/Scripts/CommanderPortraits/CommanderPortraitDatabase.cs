using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CommanderPortraitDatabase", menuName = "Game/Commander Portrait Database")]
public sealed class CommanderPortraitDatabase : ScriptableObject
{
    [SerializeField] private Sprite fallbackPortrait;
    [SerializeField] private List<CommanderPortraitEntry> entries = new List<CommanderPortraitEntry>();

    private Dictionary<string, CommanderPortraitEntry> byId;

    public Sprite FallbackPortrait => fallbackPortrait;
    public IReadOnlyList<CommanderPortraitEntry> Entries => entries;

    public bool TryGetById(string id, out CommanderPortraitEntry entry)
    {
        EnsureLookup();
        if (!string.IsNullOrWhiteSpace(id))
            return byId.TryGetValue(id, out entry);

        entry = null;
        return false;
    }

    public List<CommanderPortraitEntry> GetEntries(CommanderRace race)
    {
        List<CommanderPortraitEntry> result = new List<CommanderPortraitEntry>();
        foreach (CommanderPortraitEntry entry in entries)
        {
            if (entry != null && entry.Race == race && !string.IsNullOrWhiteSpace(entry.Id))
                result.Add(entry);
        }
        return result;
    }

    public void ReplaceEntries(List<CommanderPortraitEntry> newEntries)
    {
        entries = newEntries ?? new List<CommanderPortraitEntry>();
        byId = null;
    }

    private void OnEnable()
    {
        byId = null;
    }

    private void OnValidate()
    {
        byId = null;
    }

    private void EnsureLookup()
    {
        if (byId != null)
            return;

        byId = new Dictionary<string, CommanderPortraitEntry>();
        foreach (CommanderPortraitEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || byId.ContainsKey(entry.Id))
                continue;
            byId.Add(entry.Id, entry);
        }
    }
}
