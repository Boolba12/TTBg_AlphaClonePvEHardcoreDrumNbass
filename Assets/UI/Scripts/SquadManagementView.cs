using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SquadManagementView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup inputBlocker;

    [Header("Roster")]
    [SerializeField] private RectTransform squadListContent;
    [SerializeField] private PreBattleSquadCardView squadCardTemplate;
    [SerializeField] private TMP_Text emptyRosterLabel;

    [Header("Commander and composition")]
    [SerializeField] private Image commanderPortrait;
    [SerializeField] private TMP_Text squadTitle;
    [SerializeField] private TMP_Text commanderSummary;
    [SerializeField] private TMP_Text statusSummary;
    [SerializeField] private TMP_Text calculatedStats;
    [SerializeField] private TMP_Text compositionSummary;
    [SerializeField] private TMP_Text debuffSummary;

    [Header("Equipment")]
    [SerializeField] private EquipmentSlotView squadWeaponSlot;
    [SerializeField] private EquipmentSlotView commanderWeaponSlot;
    [SerializeField] private EquipmentSlotView armorSlot;
    [SerializeField] private EquipmentSlotView accessorySlot;

    [Header("Inventory")]
    [SerializeField] private RectTransform inventoryContent;
    [SerializeField] private ItemPreviewCardView inventoryItemTemplate;
    [SerializeField] private Button allFilterButton;
    [SerializeField] private Button weaponsFilterButton;
    [SerializeField] private Button armorFilterButton;
    [SerializeField] private Button accessoriesFilterButton;
    [SerializeField] private TMP_Text itemDetails;
    [SerializeField] private TMP_Text statComparison;

    [Header("Actions")]
    [SerializeField] private Button equipButton;
    [SerializeField] private Button unequipButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text operationStatus;

    private readonly List<PreBattleSquadCardView> squadCards = new();
    private readonly List<ItemPreviewCardView> inventoryCards = new();
    private bool listenersBound;

    public event Action<string> SquadSelected;
    public event Action<EquipmentSlotKind> EquipmentSlotSelected;
    public event Action<string> InventoryItemSelected;
    public event Action<SquadManagementInventoryFilter> FilterSelected;
    public event Action EquipRequested;
    public event Action UnequipRequested;
    public event Action SaveRequested;
    public event Action CloseRequested;

    public bool IsVisible => panelRoot != null && panelRoot.activeInHierarchy;
    public int SquadCardCount => squadCards.Count;
    public int InventoryCardCount => inventoryCards.Count;
    public Button CloseButton => closeButton;
    public Button EquipButton => equipButton;
    public Button SaveButton => saveButton;
    public string OperationMessage => operationStatus != null ? operationStatus.text : string.Empty;

    public void Show(IReadOnlyList<PreBattleSquadOption> options,
        Func<string, Sprite> portraitResolver)
    {
        ClearSquadCards();
        if (squadCardTemplate != null)
            squadCardTemplate.gameObject.SetActive(false);
        for (int i = 0; options != null && i < options.Count; i++)
        {
            PreBattleSquadOption option = options[i];
            PreBattleSquadCardView card = Instantiate(squadCardTemplate, squadListContent);
            card.name = $"ManagementSquad_{option.SquadId}";
            card.gameObject.SetActive(true);
            card.Bind(option, portraitResolver?.Invoke(option.PortraitId), false,
                HandleSquadSelected, true);
            squadCards.Add(card);
        }
        if (emptyRosterLabel != null)
            emptyRosterLabel.gameObject.SetActive(squadCards.Count == 0);
        panelRoot?.SetActive(true);
        if (inputBlocker != null)
        {
            inputBlocker.alpha = 1f;
            inputBlocker.interactable = true;
            inputBlocker.blocksRaycasts = true;
        }
    }

    public void Hide()
    {
        if (inputBlocker != null)
        {
            inputBlocker.interactable = false;
            inputBlocker.blocksRaycasts = false;
        }
        panelRoot?.SetActive(false);
    }

    public void RenderDetails(SquadManagementDetails details, Sprite portrait,
        EquipmentItemDefinition squadWeapon, EquipmentItemDefinition commanderWeapon,
        EquipmentItemDefinition armor, EquipmentItemDefinition accessory,
        EquipmentSlotKind selectedSlot)
    {
        for (int i = 0; i < squadCards.Count; i++)
            squadCards[i].SetSelected(details != null &&
                squadCards[i].SquadId == details.SquadId);
        if (commanderPortrait != null)
        {
            commanderPortrait.sprite = portrait;
            commanderPortrait.enabled = portrait != null;
        }
        if (squadTitle != null)
            squadTitle.text = details == null ? "No squad selected" : details.SquadId;
        if (commanderSummary != null)
            commanderSummary.text = details == null ? "Commander -"
                : $"Commander {details.CommanderId}\n{details.Race} - Level not tracked";
        if (statusSummary != null)
            statusSummary.text = details == null ? "Status -" : $"Status: {details.Status}";
        if (calculatedStats != null)
            calculatedStats.text = details == null ? "Calculated stats unavailable."
                : FormatStats(details.Stats);
        if (compositionSummary != null)
            compositionSummary.text = BuildComposition(details);
        if (debuffSummary != null)
            debuffSummary.text = BuildDebuffs(details);

        BindSlot(squadWeaponSlot, EquipmentSlotKind.SquadWeapon, "Squad Weapon",
            squadWeapon, selectedSlot);
        BindSlot(commanderWeaponSlot, EquipmentSlotKind.CommanderWeapon,
            "Commander Weapon", commanderWeapon, selectedSlot);
        BindSlot(armorSlot, EquipmentSlotKind.Armor, "Armor", armor, selectedSlot);
        BindSlot(accessorySlot, EquipmentSlotKind.Accessory, "Accessory", accessory,
            selectedSlot);
    }

    public void RenderInventory(IReadOnlyList<SquadManagementInventoryEntry> entries,
        string selectedInstanceId)
    {
        ClearInventoryCards();
        if (inventoryItemTemplate != null)
            inventoryItemTemplate.gameObject.SetActive(false);
        for (int i = 0; entries != null && i < entries.Count; i++)
        {
            SquadManagementInventoryEntry entry = entries[i];
            ItemPreviewCardView card = Instantiate(inventoryItemTemplate, inventoryContent);
            card.name = $"ManagementItem_{entry.Instance.InstanceId}";
            card.gameObject.SetActive(true);
            card.BindEquipment(entry.Instance, entry.Definition,
                new ItemPreviewCardState(entry.Instance.InstanceId == selectedInstanceId,
                    entry.Equipped, !entry.Compatible), HandleInventorySelected);
            inventoryCards.Add(card);
        }
        if (equipButton != null)
            equipButton.interactable = !string.IsNullOrWhiteSpace(selectedInstanceId);
    }

    public void RenderItemPreview(EquipmentItemDefinition definition,
        EquipmentStatComparison? comparison)
    {
        if (itemDetails != null)
            itemDetails.text = definition == null ? "Select an owned item."
                : $"{definition.DisplayName}\n{definition.Category}\n{definition.Description}";
        if (statComparison != null)
            statComparison.text = comparison.HasValue
                ? FormatComparison(comparison.Value)
                : "Select a compatible item to compare calculated stats.";
    }

    public void SetFilter(SquadManagementInventoryFilter filter)
    {
        SetFilterState(allFilterButton, filter == SquadManagementInventoryFilter.All);
        SetFilterState(weaponsFilterButton, filter == SquadManagementInventoryFilter.Weapons);
        SetFilterState(armorFilterButton, filter == SquadManagementInventoryFilter.Armor);
        SetFilterState(accessoriesFilterButton,
            filter == SquadManagementInventoryFilter.Accessories);
    }

    public void SetOperationStatus(string message, bool error)
    {
        if (operationStatus == null) return;
        operationStatus.text = message ?? string.Empty;
        operationStatus.color = error
            ? new Color32(210, 92, 82, 255)
            : new Color32(188, 174, 130, 255);
    }

    public void SetUnequipAvailable(bool available)
    {
        if (unequipButton != null) unequipButton.interactable = available;
    }

    private void BindSlot(EquipmentSlotView view, EquipmentSlotKind slot, string label,
        EquipmentItemDefinition definition, EquipmentSlotKind selectedSlot)
    {
        view?.Bind(slot, new EquipmentSlotPresentationModel
        {
            slotId = slot.ToString(),
            kind = slot,
            label = slot == selectedSlot ? $"> {label}" : label,
            icon = definition?.PreviewSprite,
            occupied = definition != null,
            interactable = true
        }, HandleSlotSelected);
    }

    private static string FormatStats(SquadCalculatedStats stats) =>
        $"HP {stats.MaxHP}    AP {stats.ActionPoints}\n" +
        $"STR {stats.Strength:0.#}    DEX {stats.Dexterity:0.#}\n" +
        $"Initiative {stats.Initiative:0.#}    Resolve {stats.Resolve:0.#}\n" +
        $"Accuracy {stats.Accuracy:P0}    Critical {stats.CriticalChance:P0}\n" +
        $"Physical Armor {stats.PhysicalArmor:P0}\n" +
        $"Magical Resistance {stats.MagicalResistance:P0}";

    private static string BuildComposition(SquadManagementDetails details)
    {
        if (details == null) return "COMPOSITION\nNo squad selected.";
        string value = $"COMPOSITION (read-only)\nCommander: {details.CommanderId}";
        for (int i = 0; i < SquadData.MaximumWarriors; i++)
        {
            if (details.Warriors != null && i < details.Warriors.Count &&
                details.Warriors[i] != null)
            {
                WarriorData warrior = details.Warriors[i];
                value += $"\nW{i + 1}: {warrior.id} - HP {warrior.maxHP} - " +
                         $"STR {warrior.strength:0.#} - DEX {warrior.dexterity:0.#}";
            }
            else
                value += $"\nW{i + 1}: Empty";
        }
        return value;
    }

    private static string BuildDebuffs(SquadManagementDetails details)
    {
        if (details?.Debuffs == null || details.Debuffs.Count == 0)
            return "PERSISTENT DEBUFFS\nNone";
        string value = "PERSISTENT DEBUFFS";
        for (int i = 0; i < details.Debuffs.Count; i++)
        {
            SquadManagementDebuffEntry debuff = details.Debuffs[i];
            value += $"\n{debuff.DisplayName}: {debuff.Description}";
            if (!string.IsNullOrWhiteSpace(debuff.SourceBattleId))
                value += $"\nSource: {debuff.SourceBattleId}";
        }
        return value;
    }

    private static string FormatComparison(EquipmentStatComparison comparison)
    {
        SquadCalculatedStats before = comparison.CurrentStats;
        SquadCalculatedStats after = comparison.CandidateStats;
        return "CALCULATED STAT COMPARISON\n" +
               $"HP {before.MaxHP} -> {after.MaxHP}    AP {before.ActionPoints} -> {after.ActionPoints}\n" +
               $"STR {before.Strength:0.#} -> {after.Strength:0.#}\n" +
               $"Initiative {before.Initiative:0.#} -> {after.Initiative:0.#}\n" +
               $"Resolve {before.Resolve:0.#} -> {after.Resolve:0.#}\n" +
               $"Accuracy {before.Accuracy:P0} -> {after.Accuracy:P0}\n" +
               $"Critical {before.CriticalChance:P0} -> {after.CriticalChance:P0}\n" +
               $"Armor {before.PhysicalArmor:P0} -> {after.PhysicalArmor:P0}\n" +
               $"Magic Resist {before.MagicalResistance:P0} -> {after.MagicalResistance:P0}";
    }

    private static void SetFilterState(Button button, bool selected)
    {
        if (button?.targetGraphic != null)
            button.targetGraphic.color = selected
                ? new Color32(89, 124, 105, 255)
                : Color.white;
    }

    private void HandleSquadSelected(string id) => SquadSelected?.Invoke(id);
    private void HandleSlotSelected(EquipmentSlotKind slot) => EquipmentSlotSelected?.Invoke(slot);
    private void HandleInventorySelected(string id) => InventoryItemSelected?.Invoke(id);
    private void HandleAll() => FilterSelected?.Invoke(SquadManagementInventoryFilter.All);
    private void HandleWeapons() => FilterSelected?.Invoke(SquadManagementInventoryFilter.Weapons);
    private void HandleArmor() => FilterSelected?.Invoke(SquadManagementInventoryFilter.Armor);
    private void HandleAccessories() => FilterSelected?.Invoke(SquadManagementInventoryFilter.Accessories);
    private void HandleEquip() => EquipRequested?.Invoke();
    private void HandleUnequip() => UnequipRequested?.Invoke();
    private void HandleSave() => SaveRequested?.Invoke();
    private void HandleClose() => CloseRequested?.Invoke();

    private void BindListeners()
    {
        if (listenersBound) return;
        allFilterButton?.onClick.AddListener(HandleAll);
        weaponsFilterButton?.onClick.AddListener(HandleWeapons);
        armorFilterButton?.onClick.AddListener(HandleArmor);
        accessoriesFilterButton?.onClick.AddListener(HandleAccessories);
        equipButton?.onClick.AddListener(HandleEquip);
        unequipButton?.onClick.AddListener(HandleUnequip);
        saveButton?.onClick.AddListener(HandleSave);
        closeButton?.onClick.AddListener(HandleClose);
        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!listenersBound) return;
        allFilterButton?.onClick.RemoveListener(HandleAll);
        weaponsFilterButton?.onClick.RemoveListener(HandleWeapons);
        armorFilterButton?.onClick.RemoveListener(HandleArmor);
        accessoriesFilterButton?.onClick.RemoveListener(HandleAccessories);
        equipButton?.onClick.RemoveListener(HandleEquip);
        unequipButton?.onClick.RemoveListener(HandleUnequip);
        saveButton?.onClick.RemoveListener(HandleSave);
        closeButton?.onClick.RemoveListener(HandleClose);
        listenersBound = false;
    }

    private void ClearSquadCards()
    {
        DestroyViews(squadCards);
        squadCards.Clear();
    }

    private void ClearInventoryCards()
    {
        DestroyViews(inventoryCards);
        inventoryCards.Clear();
    }

    private static void DestroyViews<T>(List<T> views) where T : Component
    {
        for (int i = views.Count - 1; i >= 0; i--)
        {
            if (views[i] == null) continue;
            if (Application.isPlaying) Destroy(views[i].gameObject);
            else DestroyImmediate(views[i].gameObject);
        }
    }

    private void OnEnable() => BindListeners();
    private void OnDisable() => UnbindListeners();
    private void OnDestroy()
    {
        UnbindListeners();
        ClearSquadCards();
        ClearInventoryCards();
    }
}
