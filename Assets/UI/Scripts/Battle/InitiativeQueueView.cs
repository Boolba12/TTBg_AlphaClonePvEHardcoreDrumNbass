using System.Collections.Generic;
using UnityEngine;

public sealed class InitiativeQueueView : MonoBehaviour
{
    [SerializeField] private RectTransform entryContainer;
    [SerializeField] private InitiativeEntryView entryPrefab;
    [SerializeField] private GameObject emptyStateRoot;

    private readonly List<InitiativeEntryView> spawnedEntries =
        new List<InitiativeEntryView>();

    public int DisplayedCount { get; private set; }
    public int RenderCount { get; private set; }
    public IReadOnlyList<InitiativeEntryView> SpawnedEntries => spawnedEntries;

    public void Configure(
        RectTransform configuredContainer,
        InitiativeEntryView configuredEntryPrefab,
        GameObject configuredEmptyStateRoot)
    {
        entryContainer = configuredContainer;
        entryPrefab = configuredEntryPrefab;
        emptyStateRoot = configuredEmptyStateRoot;
    }

    public void SetEntries(IReadOnlyList<InitiativeEntryModel> models)
    {
        int count = models?.Count ?? 0;
        EnsureEntryCount(count);
        for (int i = 0; i < spawnedEntries.Count; i++)
        {
            bool active = i < count;
            spawnedEntries[i].gameObject.SetActive(active);
            if (active)
                spawnedEntries[i].Render(models[i]);
        }

        DisplayedCount = count;
        RenderCount++;
        if (emptyStateRoot != null)
            emptyStateRoot.SetActive(count == 0);
    }

    public void ShowEmpty()
    {
        SetEntries(null);
    }

    private void EnsureEntryCount(int required)
    {
        if (entryPrefab == null || entryContainer == null)
            return;
        while (spawnedEntries.Count < required)
        {
            InitiativeEntryView entry = Instantiate(entryPrefab, entryContainer);
            entry.name = $"InitiativeEntry_{spawnedEntries.Count + 1}";
            entry.gameObject.SetActive(true);
            spawnedEntries.Add(entry);
        }
    }
}
