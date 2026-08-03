using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemPresentationCategory
{
    SquadWeapon,
    CommanderWeapon,
    Armor,
    Accessory,
    Material,
    UnknownTest
}

[Serializable]
public sealed class ItemPresentationRecord
{
    [SerializeField] private string stableId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite previewSprite;
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] private ItemPresentationCategory category = ItemPresentationCategory.UnknownTest;
    [SerializeField, TextArea] private string developmentDescription;
    [SerializeField] private bool placeholder;
    [SerializeField] private BattleWeaponDefinition sourceWeapon;

    public string StableId => stableId;
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName)
        ? displayName
        : sourceWeapon != null ? sourceWeapon.weaponName : string.Empty;
    public Sprite PreviewSprite => previewSprite != null
        ? previewSprite
        : sourceWeapon != null ? sourceWeapon.icon : null;
    public GameObject ModelPrefab => modelPrefab != null
        ? modelPrefab
        : sourceWeapon != null ? sourceWeapon.weaponPrefab : null;
    public ItemPresentationCategory Category => category;
    public string DevelopmentDescription => !string.IsNullOrWhiteSpace(developmentDescription)
        ? developmentDescription
        : sourceWeapon != null ? sourceWeapon.description : string.Empty;
    public bool IsPlaceholder => placeholder;
    public BattleWeaponDefinition SourceWeapon => sourceWeapon;

#if UNITY_EDITOR
    public void ConfigureDevelopment(
        string id,
        string configuredName,
        Sprite configuredPreview,
        GameObject configuredModel,
        ItemPresentationCategory configuredCategory,
        string description,
        bool isPlaceholder,
        BattleWeaponDefinition configuredSourceWeapon = null)
    {
        stableId = id;
        displayName = configuredName;
        previewSprite = configuredPreview;
        modelPrefab = configuredModel;
        category = configuredCategory;
        developmentDescription = description;
        placeholder = isPlaceholder;
        sourceWeapon = configuredSourceWeapon;
    }
#endif
}

[CreateAssetMenu(fileName = "ItemPresentationCatalog", menuName = "Game/UI/Item Presentation Catalog")]
public sealed class ItemPresentationCatalog : ScriptableObject
{
    [SerializeField] private List<ItemPresentationRecord> entries = new List<ItemPresentationRecord>();

    public IReadOnlyList<ItemPresentationRecord> Entries => entries;

    public bool TryGetById(string stableId, out ItemPresentationRecord entry)
    {
        if (!string.IsNullOrWhiteSpace(stableId))
        {
            foreach (ItemPresentationRecord candidate in entries)
            {
                if (candidate != null && candidate.StableId == stableId)
                {
                    entry = candidate;
                    return true;
                }
            }
        }

        entry = null;
        return false;
    }

#if UNITY_EDITOR
    public void ReplaceDevelopmentEntries(List<ItemPresentationRecord> replacements)
    {
        entries = replacements ?? new List<ItemPresentationRecord>();
    }
#endif
}
