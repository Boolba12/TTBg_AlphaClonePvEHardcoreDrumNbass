using UnityEngine;
using UnityEngine.Serialization;

public enum EquipmentItemCategory
{
    Weapon,
    Armor,
    Accessory
}

/// <summary>
/// Canonical definition contract shared by persistent equipment ownership,
/// Pre-Battle, Squad Management and immutable battle snapshots.
/// </summary>
public abstract class EquipmentItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string stableId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private bool developmentOnly = true;

    [Header("Presentation")]
    [SerializeField] private Sprite previewSprite;
    [FormerlySerializedAs("weaponPrefab")]
    [SerializeField] private GameObject modelPrefab;

    public string StableId => stableId;
    public string DisplayName => displayName;
    public string Description => description;
    public bool DevelopmentOnly => developmentOnly;
    public Sprite PreviewSprite => previewSprite;
    public GameObject ModelPrefab => modelPrefab;
    public abstract EquipmentItemCategory Category { get; }

    public abstract bool SupportsSlot(EquipmentSlotKind slot);
    public abstract SquadStatModifiers CreateStatModifiers();

    public bool Validate(out string reason)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            reason = $"{GetType().Name} stable ID is missing.";
        else if (string.IsNullOrWhiteSpace(displayName))
            reason = $"Equipment '{stableId}' display name is missing.";
        else
            reason = ValidateDefinition();
        return reason == null;
    }

    protected abstract string ValidateDefinition();

#if UNITY_EDITOR
    protected void ConfigureCore(string id, string label, string details,
        Sprite preview, GameObject model)
    {
        stableId = id;
        displayName = label;
        description = details;
        developmentOnly = true;
        previewSprite = preview;
        modelPrefab = model;
    }
#endif
}
