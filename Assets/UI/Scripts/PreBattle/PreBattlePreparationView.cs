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

    [Header("Encounter")]
    [SerializeField] private TMP_Text encounterSummary;
    [SerializeField] private TMP_Text validationStatus;

    [Header("Actions")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private readonly List<PreBattleSquadCardView> cards =
        new List<PreBattleSquadCardView>();
    private bool listenersBound;

    public event Action<string> SquadSelected;
    public event Action ConfirmRequested;
    public event Action CancelRequested;

    public bool IsVisible => panelRoot != null && panelRoot.activeInHierarchy;
    public int CardCount => cards.Count;
    public Button ConfirmButton => confirmButton;
    public Button CancelButton => cancelButton;
    public string ValidationMessage => validationStatus != null ? validationStatus.text : string.Empty;

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
                : "Equipment\nWeapon: Not equipped\nArmor: Not equipped\nAccessory: Not equipped";
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

    private void BindListeners()
    {
        if (listenersBound)
            return;
        confirmButton?.onClick.AddListener(HandleConfirm);
        cancelButton?.onClick.AddListener(HandleCancel);
        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (!listenersBound)
            return;
        confirmButton?.onClick.RemoveListener(HandleConfirm);
        cancelButton?.onClick.RemoveListener(HandleCancel);
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

    private void OnEnable() => BindListeners();
    private void OnDisable() => UnbindListeners();
    private void OnDestroy()
    {
        UnbindListeners();
        ClearCards();
    }
}
