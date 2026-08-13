using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PreBattlePreparationView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup inputBlocker;

    [Header("Available squads")]
    [SerializeField] private RectTransform squadListContent;
    [SerializeField] private PreBattleSquadCardView squadCardTemplate;
    [SerializeField] private TMP_Text emptyRosterLabel;

    [Header("Selected squad")]
    [SerializeField] private Image selectedPortrait;
    [SerializeField] private TMP_Text selectedTitle;
    [SerializeField] private TMP_Text selectedCommander;
    [SerializeField] private TMP_Text selectedComposition;
    [SerializeField] private TMP_Text selectedStats;
    [SerializeField] private TMP_Text equipmentSummary;

    [Header("Equipment v2")]
    [SerializeField] private EquipmentSlotView squadWeaponSlot;
    [SerializeField] private EquipmentSlotView commanderWeaponSlot;
    [SerializeField] private EquipmentSlotView armorSlot;
    [SerializeField] private EquipmentSlotView accessorySlot;
    [SerializeField] private RectTransform equipmentItemContent;
    [SerializeField] private ItemPreviewCardView equipmentItemTemplate;
    [SerializeField] private TMP_Text equipmentDetails;
    [SerializeField] private Button unequipButton;

    [Header("Encounter")]
    [SerializeField] private TMP_Text encounterSummary;
    [SerializeField] private TMP_Text validationStatus;

    [Header("Actions")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private readonly List<PreBattleSquadCardView> cards =
        new List<PreBattleSquadCardView>();
    private readonly List<ItemPreviewCardView> equipmentCards =
        new List<ItemPreviewCardView>();
    private bool listenersBound;

    public event Action<string> SquadSelected;
    public event Action ConfirmRequested;
    public event Action CancelRequested;
    public event Action<EquipmentSlotKind> EquipmentSlotSelected;
    public event Action<string> EquipmentItemSelected;
    public event Action UnequipRequested;

    public bool IsVisible => panelRoot != null && panelRoot.activeInHierarchy;
    public int CardCount => cards.Count;
    public Button ConfirmButton => confirmButton;
    public Button CancelButton => cancelButton;
    public string ValidationMessage => validationStatus != null ? validationStatus.text : string.Empty;
    public int EquipmentCardCount => equipmentCards.Count;

    public void Configure(
        GameObject root,
        CanvasGroup blocker,
        RectTransform content,
        PreBattleSquadCardView cardTemplate,
        TMP_Text emptyRoster,
        Image portrait,
        TMP_Text title,
        TMP_Text commander,
        TMP_Text composition,
        TMP_Text stats,
        TMP_Text equipment,
        TMP_Text encounter,
        TMP_Text validation,
        Button confirm,
        Button cancel)
    {
        UnbindListeners();
        panelRoot = root;
        inputBlocker = blocker;
        squadListContent = content;
        squadCardTemplate = cardTemplate;
        emptyRosterLabel = emptyRoster;
        selectedPortrait = portrait;
        selectedTitle = title;
        selectedCommander = commander;
        selectedComposition = composition;
        selectedStats = stats;
        equipmentSummary = equipment;
        encounterSummary = encounter;
        validationStatus = validation;
        confirmButton = confirm;
        cancelButton = cancel;
        BindListeners();
        Hide();
    }

    public void ConfigureEquipment(EquipmentSlotView configuredSquadWeaponSlot,
        EquipmentSlotView configuredCommanderWeaponSlot,
        EquipmentSlotView configuredArmorSlot,
        EquipmentSlotView configuredAccessorySlot,
        RectTransform itemContent,
        ItemPreviewCardView itemTemplate,
        TMP_Text details,
        Button configuredUnequipButton)
    {
        UnbindListeners();
        squadWeaponSlot = configuredSquadWeaponSlot;
        commanderWeaponSlot = configuredCommanderWeaponSlot;
        armorSlot = configuredArmorSlot;
        accessorySlot = configuredAccessorySlot;
        equipmentItemContent = itemContent;
        equipmentItemTemplate = itemTemplate;
        equipmentDetails = details;
        unequipButton = configuredUnequipButton;
        if (equipmentSummary != null)
            equipmentSummary.gameObject.SetActive(false);
        BindListeners();
    }

    public void Show(
        IReadOnlyList<PreBattleSquadOption> options,
        Func<string, Sprite> portraitResolver,
        string encounterText)
    {
        ClearCards();
        if (squadCardTemplate != null)
            squadCardTemplate.gameObject.SetActive(false);

        for (int i = 0; options != null && i < options.Count; i++)
        {
            PreBattleSquadOption option = options[i];
            PreBattleSquadCardView card = Instantiate(squadCardTemplate, squadListContent);
            card.name = $"SquadCard_{option.SquadId}";
            card.gameObject.SetActive(true);
            card.Bind(option, portraitResolver?.Invoke(option.PortraitId), false, HandleSquadSelected);
            cards.Add(card);
        }

        if (emptyRosterLabel != null)
            emptyRosterLabel.gameObject.SetActive(cards.Count == 0);
        if (encounterSummary != null)
            encounterSummary.text = encounterText ?? "Encounter details unavailable.";
        SetSelected(null, null);
        ClearEquipmentCards();
        SetValidation(cards.Count == 0
            ? "No persistent squads are available. Create or load a battle-ready squad first."
            : "Select one battle-ready squad.", false);
        if (panelRoot != null)
            panelRoot.SetActive(true);
        if (inputBlocker != null)
        {
            inputBlocker.alpha = 1f;
            inputBlocker.interactable = true;
            inputBlocker.blocksRaycasts = true;
        }
    }

    public void RenderEquipment(SquadData squad, EquipmentDefinitionCatalog catalog,
        SquadEquipmentService service, EquipmentSlotKind selectedSlot)
    {
        ClearEquipmentCards();
        BindSlot(squadWeaponSlot, squad, service, EquipmentSlotKind.SquadWeapon,
            "Squad Weapon", selectedSlot);
        BindSlot(commanderWeaponSlot, squad, service, EquipmentSlotKind.CommanderWeapon,
            "Commander Weapon", selectedSlot);
        BindSlot(armorSlot, squad, service, EquipmentSlotKind.Armor,
            "Armor", selectedSlot);
        BindSlot(accessorySlot, squad, service, EquipmentSlotKind.Accessory,
            "Accessory", selectedSlot);

        if (equipmentItemTemplate != null)
            equipmentItemTemplate.gameObject.SetActive(false);
        if (squad?.Equipment?.OwnedItems != null && equipmentItemTemplate != null &&
            equipmentItemContent != null)
        {
            string selectedInstance = squad.Equipment.GetEquippedInstanceId(selectedSlot);
            for (int i = 0; i < squad.Equipment.OwnedItems.Count; i++)
            {
                EquipmentItemInstance item = squad.Equipment.OwnedItems[i];
                EquipmentItemDefinition definition = null;
                catalog?.TryGetDefinition(item?.DefinitionId, out definition);
                bool compatible = definition != null && definition.SupportsSlot(selectedSlot);
                if (!compatible)
                    continue;
                ItemPreviewCardView card = Instantiate(equipmentItemTemplate,
                    equipmentItemContent);
                card.gameObject.name = $"EquipmentItem_{item?.InstanceId}";
                card.gameObject.SetActive(true);
                card.BindEquipment(item, definition,
                    new ItemPreviewCardState(item?.InstanceId == selectedInstance,
                        IsEquipped(squad, item?.InstanceId), !compatible),
                    HandleEquipmentItemSelected);
                equipmentCards.Add(card);
            }
        }

        EquipmentItemDefinition selectedDefinition =
            service?.ResolveEquippedDefinition(squad, selectedSlot);
        Weapon current = selectedDefinition as Weapon;
        if (equipmentDetails != null)
        {
            equipmentDetails.text = selectedDefinition is not Weapon
                ? BuildEquipmentDetails(selectedDefinition)
                : current == null
                ? $"{selectedSlot}\nNo compatible item equipped."
                : $"{current.DisplayName} · {current.Class}\n" +
                  $"Damage +{current.BaseDamageBonus}  Scaling +{current.PrimaryScalingBonus:0.##}\n" +
                  $"STR +{current.StrengthBonus:0.##}  ACC +{current.AccuracyBonus:P0}  " +
                  $"CRIT +{current.CriticalChanceBonus:P0}";
        }
        if (unequipButton != null)
            unequipButton.interactable = squad != null &&
                !string.IsNullOrWhiteSpace(squad.Equipment.GetEquippedInstanceId(selectedSlot));
    }

    public void SetSelected(PreBattleSquadOption option, Sprite portrait)
    {
        for (int i = 0; i < cards.Count; i++)
            cards[i].SetSelected(option != null && cards[i].SquadId == option.SquadId);

        if (selectedPortrait != null)
        {
            selectedPortrait.sprite = portrait;
            selectedPortrait.enabled = portrait != null;
        }
        if (selectedTitle != null)
            selectedTitle.text = option == null ? "No squad selected" : $"Squad {option.SquadId}";
        if (selectedCommander != null)
        {
            selectedCommander.text = option == null
                ? "Commander —"
                : $"Commander {option.CommanderId} · {option.Race}";
        }
        if (selectedComposition != null)
        {
            selectedComposition.text = option == null
                ? "Warriors —"
                : $"Warriors {option.LivingWarriors}/{option.MaximumWarriors} · {option.Status}";
        }
        if (selectedStats != null)
        {
            SquadCalculatedStats stats = option?.CalculatedStats ?? default;
            selectedStats.text = option == null
                ? "Select a squad to inspect calculated battle values."
                : $"HP {stats.MaxHP}    AP {stats.ActionPoints}\n" +
                  $"Initiative {stats.Initiative:0.#}    Strength {stats.Strength:0.#}\n" +
                  $"Dexterity {stats.Dexterity:0.#}    Accuracy {stats.Accuracy:P0}\n" +
                  $"Evasion {stats.Evasion:P0}    Armor {stats.PhysicalArmor:P0}\n" +
                  $"Morale {stats.Morale:0.#}    Resolve {stats.Resolve:0.#}";
        }
        if (equipmentSummary != null)
        {
            equipmentSummary.text = option == null
                ? "Equipment —"
                : "Select an equipment slot, then choose one owned item.";
        }
        if (confirmButton != null)
            confirmButton.interactable = option != null && option.IsAvailable;
    }

    public void SetValidation(string message, bool isError)
    {
        if (validationStatus == null)
            return;
        validationStatus.text = message ?? string.Empty;
        validationStatus.color = isError
            ? new Color32(210, 92, 82, 255)
            : new Color32(188, 174, 130, 255);
    }

    public void Hide()
    {
        if (inputBlocker != null)
        {
            inputBlocker.interactable = false;
            inputBlocker.blocksRaycasts = false;
        }
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void HandleSquadSelected(string squadId) => SquadSelected?.Invoke(squadId);
    private void HandleConfirm() => ConfirmRequested?.Invoke();
    private void HandleCancel() => CancelRequested?.Invoke();
    private void HandleEquipmentSlotSelected(EquipmentSlotKind slot) =>
        EquipmentSlotSelected?.Invoke(slot);
    private void HandleEquipmentItemSelected(string instanceId) =>
        EquipmentItemSelected?.Invoke(instanceId);
    private void HandleUnequip() => UnequipRequested?.Invoke();

    private void BindListeners()
    {
        if (listenersBound)
            return;
        confirmButton?.onClick.AddListener(HandleConfirm);
        cancelButton?.onClick.AddListener(HandleCancel);
        unequipButton?.onClick.AddListener(HandleUnequip);
        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!listenersBound)
            return;
        confirmButton?.onClick.RemoveListener(HandleConfirm);
        cancelButton?.onClick.RemoveListener(HandleCancel);
        unequipButton?.onClick.RemoveListener(HandleUnequip);
        listenersBound = false;
    }

    private void ClearCards()
    {
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            if (cards[i] == null)
                continue;
            if (Application.isPlaying)
                Destroy(cards[i].gameObject);
            else
                DestroyImmediate(cards[i].gameObject);
        }
        cards.Clear();
    }

    private void BindSlot(EquipmentSlotView view, SquadData squad,
        SquadEquipmentService service, EquipmentSlotKind slot, string label,
        EquipmentSlotKind selectedSlot)
    {
        if (view == null) return;
        EquipmentItemDefinition definition = service?.ResolveEquippedDefinition(squad, slot);
        view.Bind(slot, new EquipmentSlotPresentationModel
        {
            slotId = slot.ToString(),
            kind = slot,
            label = selectedSlot == slot ? $"> {label}" : label,
            icon = definition?.PreviewSprite,
            occupied = definition != null,
            interactable = squad != null
        }, HandleEquipmentSlotSelected);
    }

    private static string BuildEquipmentDetails(EquipmentItemDefinition definition)
    {
        if (definition is ArmorDefinition armor)
            return $"{armor.DisplayName} - Armor\n" +
                   $"Physical Armor +{armor.PhysicalArmorModifier:P0}\n" +
                   $"Magical Resistance +{armor.MagicalResistanceModifier:P0}";
        if (definition is AccessoryDefinition accessory)
            return $"{accessory.DisplayName} - Accessory\n" +
                   $"Resolve +{accessory.ResolveModifier:0.##}  " +
                   $"Initiative +{accessory.InitiativeModifier:0.##}\n" +
                   $"Accuracy +{accessory.AccuracyModifier:P0}  " +
                   $"Critical +{accessory.CriticalChanceModifier:P0}";
        return "No compatible item equipped.";
    }

    private static bool IsEquipped(SquadData squad, string instanceId)
    {
        if (squad == null || string.IsNullOrWhiteSpace(instanceId)) return false;
        foreach (EquipmentSlotKind slot in new[] { EquipmentSlotKind.SquadWeapon,
                     EquipmentSlotKind.CommanderWeapon, EquipmentSlotKind.Armor,
                     EquipmentSlotKind.Accessory })
            if (squad.Equipment.GetEquippedInstanceId(slot) == instanceId) return true;
        return false;
    }

    private void ClearEquipmentCards()
    {
        for (int i = equipmentCards.Count - 1; i >= 0; i--)
        {
            if (equipmentCards[i] == null) continue;
            if (Application.isPlaying) Destroy(equipmentCards[i].gameObject);
            else DestroyImmediate(equipmentCards[i].gameObject);
        }
        equipmentCards.Clear();
    }

    private void OnEnable() => BindListeners();
    private void OnDisable() => UnbindListeners();
    private void OnDestroy()
    {
        UnbindListeners();
        ClearCards();
        ClearEquipmentCards();
    }
}
