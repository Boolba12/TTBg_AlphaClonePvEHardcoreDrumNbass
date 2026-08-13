using UnityEngine;

[CreateAssetMenu(
    fileName = "PersistentDebuffDefinition",
    menuName = "Game/Battle/Persistent Debuff")]
public sealed class PersistentDebuffDefinition : ScriptableObject
{
    [SerializeField] private string stableId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private float resolveModifier = -1f;
    [SerializeField] private bool persistent = true;
    [SerializeField] private bool stackable;

    public string StableId => stableId;
    public string DisplayName => displayName;
    public string Description => description;
    public float ResolveModifier => resolveModifier;
    public bool Persistent => persistent;
    public bool Stackable => stackable;

    public bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(stableId))
        {
            error = "Persistent debuff stable ID is missing.";
            return false;
        }
        if (!persistent)
        {
            error = "Post-battle debuff must be persistent.";
            return false;
        }
        error = null;
        return true;
    }

#if UNITY_EDITOR
    public void ConfigureDevelopment(
        string id,
        string label,
        string details,
        float configuredResolveModifier)
    {
        stableId = id;
        displayName = label;
        description = details;
        resolveModifier = configuredResolveModifier;
        persistent = true;
        stackable = false;
    }
#endif
}
