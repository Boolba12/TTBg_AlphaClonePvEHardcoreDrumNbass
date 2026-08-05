using UnityEngine;

[CreateAssetMenu(
    fileName = "SquadFormationPresentation",
    menuName = "Game/Squads/Formation Presentation")]
public sealed class SquadFormationPresentation : ScriptableObject
{
    [SerializeField] private string stableId;
    [SerializeField] private GameObject commanderPrefab;
    [SerializeField] private GameObject warriorPrefab;

    public string StableId => stableId;
    public GameObject CommanderPrefab => commanderPrefab;
    public GameObject WarriorPrefab => warriorPrefab;
    public bool IsValid => commanderPrefab != null && warriorPrefab != null;

#if UNITY_EDITOR
    public void ConfigureDevelopment(
        string id,
        GameObject configuredCommander,
        GameObject configuredWarrior)
    {
        stableId = id;
        commanderPrefab = configuredCommander;
        warriorPrefab = configuredWarrior;
    }
#endif
}
